using Microsoft.Extensions.Logging;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Domain.Inspections;

namespace NorthernLink.Fleet.Application.Inspections;

/// <summary>
/// The I/O half of duplicate defects, shared by the manual resolve, work-order completion and
/// the duplicates preview so the three cannot disagree: resolve the vehicle scope, load the
/// candidates through the ONE repository query, and hand them to the domain rule in
/// <see cref="DuplicateDefects"/>.
///
/// <para><b>Phase 2 of a two-phase save.</b> The caller first saves its own primary write (the
/// addressed defect, or the work-order completion with its own lines) exactly as before; only
/// then does it call <see cref="ClearForSourceAsync"/> / <see cref="ClearForVehicleAsync"/>,
/// which commit in a SEPARATE save. Every inspection's Version is a concurrency token, so a
/// duplicate on an inspection someone else is editing at that moment must not be able to fail —
/// or roll back — the primary write. On a version conflict the tracked state is dropped, the
/// candidates are re-read fresh and the rule re-applied, up to <see cref="MaxAttempts"/> times;
/// after that, or on any other failure, the duplicates are left OPEN (the safe direction), a
/// warning naming the inspections is logged, and the call still succeeds. Clearing returns only
/// what was actually committed.</para>
///
/// <para>Each save goes through the module's normal audit pipeline: the first save journals and
/// snapshots the primary write and clears its domain events; the second sees only the
/// duplicates' inspections as Modified (each raising its own
/// <c>VehicleInspectionDefectsResolvedDomainEvent</c>), so neither save is eventless and nothing
/// is journaled twice. A save with nothing to clear is skipped entirely.</para>
/// </summary>
public sealed class DuplicateDefectCascade(
    IVehicleInspectionRepository inspections,
    IVehicleRepository vehicles,
    ILogger<DuplicateDefectCascade> logger)
{
    /// <summary>How many times the cascade re-reads and retries after losing a version race.</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// The panel's vehicle scope for <paramref name="source"/> (see <see cref="DefectVehicleScope"/>):
    /// its linked vehicle when it has one; otherwise the vehicle whose CURRENT unit number is its
    /// unit text, or — no such vehicle — the unit-only inspections carrying that text.
    /// </summary>
    public async Task<DefectVehicleScope> ScopeForAsync(VehicleInspection source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.VehicleId is { } vehicleId)
        {
            return await ScopeForVehicleAsync(vehicleId, cancellationToken);
        }

        var byUnit = await vehicles.GetByUnitNumberAsync(source.Unit, cancellationToken);
        return byUnit is null
            ? DefectVehicleScope.UnitOnly(source.Unit)
            : DefectVehicleScope.ForVehicle(byUnit.Id, byUnit.UnitNumber);
    }

    /// <summary>The panel's scope for a known vehicle — its id, plus legacy DVIRs under its current unit number.</summary>
    public async Task<DefectVehicleScope> ScopeForVehicleAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetByIdAsync(vehicleId, cancellationToken);
        return DefectVehicleScope.ForVehicle(vehicleId, vehicle?.UnitNumber);
    }

    /// <summary>
    /// Read-only: the open duplicates of <paramref name="source"/>'s defect
    /// <paramref name="item"/> that a repair recorded at <paramref name="reportedBefore"/> would
    /// clear, oldest report first. The same scope, query and rule as the cascade.
    /// </summary>
    public async Task<IReadOnlyList<DefectOnInspection>> FindAsync(
        VehicleInspection source,
        string item,
        DateTimeOffset reportedBefore,
        CancellationToken cancellationToken)
    {
        var scope = await ScopeForAsync(source, cancellationToken);
        var candidates = await inspections.GetWithOpenDefectsAsync(scope, cancellationToken);
        return DuplicateDefects.Find(source, item, scope, reportedBefore, candidates);
    }

    /// <summary>
    /// Phase 2 for ONE repaired defect (the manual resolve): clears its duplicates in the source's
    /// own vehicle scope, narrowed by <paramref name="include"/>. A <paramref name="repaired"/>
    /// whose reason does not clear duplicates clears nothing. Never throws for a cascade failure.
    /// </summary>
    public Task<IReadOnlyList<DefectOnInspection>> ClearForSourceAsync(
        DefectOnInspection repaired,
        DateTimeOffset reportedBefore,
        Func<DefectOnInspection, bool>? include,
        CancellationToken cancellationToken) =>
        ClearAsync(ct => ScopeForAsync(repaired.Inspection, ct), [repaired], reportedBefore, include, cancellationToken);

    /// <summary>
    /// Phase 2 for every defect a work order repaired: ONE scope (the work order's vehicle, which
    /// every line shares), ONE candidate query for all of them, each item applied in memory, ONE
    /// save. Sources whose reason does not clear duplicates are skipped. Never throws for a
    /// cascade failure.
    /// </summary>
    public Task<IReadOnlyList<DefectOnInspection>> ClearForVehicleAsync(
        Guid vehicleId,
        IReadOnlyList<DefectOnInspection> repaired,
        DateTimeOffset reportedBefore,
        Func<DefectOnInspection, bool>? include,
        CancellationToken cancellationToken) =>
        ClearAsync(ct => ScopeForVehicleAsync(vehicleId, ct), repaired, reportedBefore, include, cancellationToken);

    private async Task<IReadOnlyList<DefectOnInspection>> ClearAsync(
        Func<CancellationToken, Task<DefectVehicleScope>> scopeFor,
        IReadOnlyList<DefectOnInspection> repaired,
        DateTimeOffset reportedBefore,
        Func<DefectOnInspection, bool>? include,
        CancellationToken cancellationToken)
    {
        // F1: only a repair cascades — the rule lives on the reason, in the domain.
        var sources = repaired
            .Where(r => r.Defect.ResolutionReason is { } reason && reason.ClearsDuplicates())
            .ToList();

        if (sources.Count == 0)
        {
            return [];
        }

        var sourceIds = sources.Select(s => s.Inspection.Id).Distinct().ToList();
        IReadOnlyList<Guid> lastAttempted = [];

        try
        {
            var scope = await scopeFor(cancellationToken);

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // Fresh on every attempt: after a conflict the tracker was cleared, so this
                // re-reads every candidate at its current version.
                var candidates = await inspections.GetWithOpenDefectsAsync(scope, cancellationToken);

                var cleared = new List<DefectOnInspection>();
                foreach (var source in sources)
                {
                    // Each source's own (already saved) stamp is copied; a duplicate cleared for
                    // an earlier source is resolved by now, so a later one never re-stamps it.
                    cleared.AddRange(DuplicateDefects.Resolve(
                        source.Inspection, source.Defect.Item, scope, reportedBefore, candidates, include));
                }

                if (cleared.Count == 0)
                {
                    return [];
                }

                lastAttempted = [.. cleared.Select(c => c.Inspection.Id).Distinct()];

                if (await inspections.TrySaveChangesAsync(cancellationToken))
                {
                    return cleared;
                }
            }

            logger.LogWarning(
                "Duplicate defects of inspection(s) {SourceInspectionIds} were left open: the duplicates on inspection(s) {DuplicateInspectionIds} lost {Attempts} version conflicts in a row. The primary resolution was saved; the duplicates stay open for a later resolve.",
                sourceIds,
                lastAttempted,
                MaxAttempts);
            return [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The primary write is already committed; nothing here may turn that success into an
            // error. Drop whatever this phase staged so no later save in the scope can commit it.
            inspections.DiscardChanges();
            logger.LogError(
                ex,
                "Clearing duplicate defects of inspection(s) {SourceInspectionIds} failed; the duplicates on inspection(s) {DuplicateInspectionIds} were left open. The primary resolution was saved.",
                sourceIds,
                lastAttempted);
            return [];
        }
    }
}
