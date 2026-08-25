using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Read side of preventative maintenance — folds rm_pm_completions to the latest row per
/// (code, kind) and runs <see cref="PmSchedule.Compute"/> per plan line, in C#. The fold
/// deliberately stays client-side: it reads only the narrow columns due math needs
/// (<see cref="CompletionFacts"/>, never whole rows), and fleet sizes make 250 items ×
/// fleet size trivial — but it does transfer every completion row of the queried vehicles,
/// so if the ledger ever grows to many thousands of rows per vehicle, revisit with a
/// server-side DISTINCT ON fold. One loader (<see cref="LoadPmBatchAsync"/>) assembles the
/// per-vehicle inputs for the single-vehicle views AND the fleet dashboard, so the
/// fold/projection can never drift between them. The vehicle's current odometer comes from
/// rm_vehicles; "today" is <see cref="PmSchedule.TodayUtc"/> — the single source every PM
/// computation shares. Completions are folded per vehicle across plan switches: codes are
/// the identity, so a reassignment does not erase the history of lines both plans share.
/// Disposed vehicles: per-vehicle views stay readable (auditing a sold unit is legitimate);
/// plan assigned-counts and the fleet dashboard exclude them — see <see cref="IPmReadService"/>.
/// </summary>
internal sealed class PmReadService(FleetDbContext context) : IPmReadService
{
    public async Task<IReadOnlyList<MaintenancePlanSummaryResponse>> GetPlansAsync(
        CancellationToken cancellationToken = default)
    {
        // Line counts are computed server-side (jsonb_array_length) — the plan documents
        // themselves never leave the database for the list view.
        var plans = await context.MaintenancePlanReadModels
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.VehicleModel,
                p.ServiceClass,
                p.Notes,
                ItemCount = p.Items.Count,
                OverhaulCount = p.Overhauls.Count,
                p.CreatedAtUtc,
                p.UpdatedAtUtc,
            })
            .ToListAsync(cancellationToken);

        // Disposed vehicles drop out of the count — a plan "assigned to 3 vehicles" means
        // three units still in the fleet, matching the fleet dashboard's scope.
        var assignedCounts = await context.PlanAssignmentReadModels
            .AsNoTracking()
            .Where(a => context.VehicleReadModels.Any(v => v.Id == a.VehicleId && v.DisposedAtUtc == null))
            .GroupBy(a => a.PlanId)
            .Select(g => new { PlanId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.PlanId, g => g.Count, cancellationToken);

        return plans
            .Select(p => new MaintenancePlanSummaryResponse(
                p.Id,
                p.Name,
                p.VehicleModel,
                p.ServiceClass,
                p.Notes,
                p.ItemCount,
                p.OverhaulCount,
                assignedCounts.GetValueOrDefault(p.Id),
                p.CreatedAtUtc,
                p.UpdatedAtUtc))
            .ToList();
    }

    public async Task<MaintenancePlanResponse?> GetPlanByIdAsync(
        Guid planId, CancellationToken cancellationToken = default)
    {
        var plan = await context.MaintenancePlanReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

        return plan is null ? null : ToPlanResponse(plan);
    }

    public async Task<VehiclePmStatusResponse?> GetVehicleStatusAsync(
        Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var pm = await LoadVehiclePmAsync(vehicleId, cancellationToken);
        if (pm is null)
        {
            return null;
        }

        if (pm.Plan is null)
        {
            return new VehiclePmStatusResponse(false, null, null, null, pm.OdometerKm, []);
        }

        return new VehiclePmStatusResponse(
            true,
            pm.Plan.Id,
            pm.Plan.Name,
            pm.AssignedAtUtc,
            pm.OdometerKm,
            ComputeEntries(pm, PmSchedule.TodayUtc()));
    }

    public async Task<PmDueResponse?> GetDueAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var pm = await LoadVehiclePmAsync(vehicleId, cancellationToken);
        if (pm is null)
        {
            return null;
        }

        if (pm.Plan is null)
        {
            return new PmDueResponse(false, null, null, pm.OdometerKm, 0, [], []);
        }

        var entries = ComputeEntries(pm, PmSchedule.TodayUtc());

        var due = DueOnly(entries);

        // Preserve plan order inside each group; groups follow the plan's system order,
        // with the synthetic Overhauls group naturally last (overhauls are computed last).
        var groups = due
            .GroupBy(e => e.System)
            .Select(g => new PmDueGroupResponse(g.Key, [.. g]))
            .ToList();

        var notYetRecorded = entries
            .Where(e => e.State == nameof(PmDueState.NotYetRecorded))
            .ToList();

        return new PmDueResponse(
            true,
            pm.Plan.Id,
            pm.Plan.Name,
            pm.OdometerKm,
            due.Sum(e => e.ShopMinutes),
            groups,
            notYetRecorded);
    }

    public async Task<PmOverhaulsResponse?> GetOverhaulsAsync(
        Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var pm = await LoadVehiclePmAsync(vehicleId, cancellationToken);
        if (pm is null)
        {
            return null;
        }

        if (pm.Plan is null)
        {
            return new PmOverhaulsResponse(false, null, null, pm.OdometerKm, []);
        }

        var itemsByCode = pm.Plan.Items.ToDictionary(i => i.Code, StringComparer.Ordinal);

        // The computed half comes from the same ComputeEntries output every other view uses
        // — never a private re-run of the due loop; the mapper joins it to the spec fields.
        var overhaulEntriesByCode = ComputeEntries(pm, PmSchedule.TodayUtc())
            .Where(e => e.Kind == nameof(PmEntryKind.Overhaul))
            .ToDictionary(e => e.Code, StringComparer.Ordinal);

        var overhauls = pm.Plan.Overhauls.Select(overhaul =>
        {
            var relatedMeasurements = overhaul.RelatedItemCodes.Select(code =>
            {
                var latest = LatestFor(pm.LatestCompletions, code, PmEntryKind.Item);
                return new RelatedMeasurementResponse(
                    code,
                    itemsByCode.TryGetValue(code, out var item) ? item.Component : code,
                    latest?.Measurement,
                    latest?.PerformedAt,
                    latest?.OdometerKm);
            }).ToList();

            return MaintenanceResponseMapper.ToOverhaulStatus(
                overhaul, overhaulEntriesByCode[overhaul.Code], relatedMeasurements);
        }).ToList();

        return new PmOverhaulsResponse(true, pm.Plan.Id, pm.Plan.Name, pm.OdometerKm, overhauls);
    }

    public async Task<IReadOnlyList<PmCompletionResponse>?> GetHistoryAsync(
        Guid vehicleId, int limit = IPmReadService.DefaultHistoryLimit, CancellationToken cancellationToken = default)
    {
        // An unknown vehicle is a 404, same as the sibling vehicle-scoped views — probe
        // existence only, never the whole row.
        var vehicleExists = await context.VehicleReadModels
            .AsNoTracking()
            .AnyAsync(v => v.Id == vehicleId, cancellationToken);

        if (!vehicleExists)
        {
            return null;
        }

        var completions = await context.PmCompletionReadModels
            .AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .OrderByDescending(c => c.PerformedAt)
            .ThenByDescending(c => c.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, IPmReadService.MaxHistoryLimit))
            .ToListAsync(cancellationToken);

        return completions
            .Select(c => new PmCompletionResponse(
                c.Id,
                c.VehicleId,
                c.PlanId,
                c.ItemCode,
                c.Kind,
                c.PerformedAt,
                c.OdometerKm,
                c.PerformedBy,
                c.WorkOrderId,
                c.Measurement,
                c.Notes,
                c.CreatedAtUtc))
            .ToList();
    }

    public async Task<FleetPmDueResponse> GetFleetDueAsync(CancellationToken cancellationToken = default)
    {
        // Live (non-disposed) vehicles first, so a dead vehicle's assignment and completion
        // ledger are never fetched at all; the shared batch loader does the rest.
        var vehicles = await context.VehicleReadModels
            .AsNoTracking()
            .Where(v => v.DisposedAtUtc == null)
            .Select(v => new { v.Id, v.UnitNumber, v.OdometerKm })
            .ToListAsync(cancellationToken);

        if (vehicles.Count == 0)
        {
            return new FleetPmDueResponse([]);
        }

        var pmByVehicle = await LoadPmBatchAsync(
            vehicles.Select(v => new VehicleOdometer(v.Id, v.OdometerKm)).ToList(), cancellationToken);

        var today = PmSchedule.TodayUtc();
        var rows = new List<FleetVehiclePmDueResponse>();
        foreach (var vehicle in vehicles)
        {
            // No plan resolved = unassigned (not on the dashboard), or the plan row lagging
            // its projection — reads as "not on the dashboard yet" rather than erroring.
            var pm = pmByVehicle[vehicle.Id];
            if (pm.Plan is null)
            {
                continue;
            }

            var entries = ComputeEntries(pm, today);
            var due = DueOnly(entries);

            rows.Add(new FleetVehiclePmDueResponse(
                vehicle.Id,
                vehicle.UnitNumber,
                vehicle.OdometerKm,
                pm.Plan.Id,
                pm.Plan.Name,
                due.Count(e => e.State == nameof(PmDueState.DueSoon)),
                due.Count(e => e.State == nameof(PmDueState.Overdue)),
                entries.Count(e => e.State == nameof(PmDueState.NotYetRecorded)),
                due));
        }

        return new FleetPmDueResponse(FleetPmDueResponse.OrderByUrgency(rows));
    }

    /// <summary>The DueSoon/Overdue subset of a computed entry list, plan order preserved.</summary>
    private static List<PmEntryStatusResponse> DueOnly(IEnumerable<PmEntryStatusResponse> entries) =>
        [.. entries.Where(e => e.State is nameof(PmDueState.DueSoon) or nameof(PmDueState.Overdue))];

    /// <summary>
    /// Loads everything one vehicle's PM computations need — the odometer probe plus the
    /// shared batch loader for a single id. Null when the vehicle does not exist;
    /// <c>Plan</c> null when it has no (resolvable) assignment.
    /// </summary>
    private async Task<VehiclePm?> LoadVehiclePmAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await context.VehicleReadModels
            .AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => new { v.OdometerKm })
            .FirstOrDefaultAsync(cancellationToken);

        if (vehicle is null)
        {
            return null;
        }

        var pmByVehicle = await LoadPmBatchAsync(
            [new VehicleOdometer(vehicleId, vehicle.OdometerKm)], cancellationToken);

        return pmByVehicle[vehicleId];
    }

    /// <summary>
    /// The one assembly path for <see cref="VehiclePm"/> — used by the single-vehicle views
    /// and the fleet dashboard alike, so the fold and column projection can never drift
    /// between them. Three deliberately simple queries (assignments, plans by id, folded
    /// completion columns): each is a plain single-table scan, never a correlated
    /// jsonb-entity subquery, so translation holds on real Postgres. Completions are
    /// fetched only for vehicles that actually have an assignment. Every input vehicle gets
    /// a result entry; <c>Plan</c> is null when it has no assignment or the plan row lags
    /// its projection.
    /// </summary>
    private async Task<Dictionary<Guid, VehiclePm>> LoadPmBatchAsync(
        IReadOnlyList<VehicleOdometer> vehicles, CancellationToken cancellationToken)
    {
        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        var assignments = await context.PlanAssignmentReadModels
            .AsNoTracking()
            .Where(a => vehicleIds.Contains(a.VehicleId))
            .Select(a => new AssignmentFacts(a.VehicleId, a.PlanId, a.AssignedAtUtc))
            .ToListAsync(cancellationToken);

        // TryAdd, not ToDictionary: the write side guarantees one assignment per vehicle,
        // but the projection table carries no unique index — tolerate a transient double.
        var assignmentsByVehicle = new Dictionary<Guid, AssignmentFacts>();
        foreach (var assignment in assignments)
        {
            assignmentsByVehicle.TryAdd(assignment.VehicleId, assignment);
        }

        var planIds = assignmentsByVehicle.Values.Select(a => a.PlanId).Distinct().ToList();
        var plansById = planIds.Count == 0
            ? new Dictionary<Guid, MaintenancePlanReadModel>()
            : await context.MaintenancePlanReadModels
                .AsNoTracking()
                .Where(p => planIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

        // Only assigned vehicles have due math to feed — unassigned ledgers are not fetched.
        var assignedVehicleIds = assignmentsByVehicle.Keys.ToList();
        var completionsByVehicle = assignedVehicleIds.Count == 0
            ? []
            : (await context.PmCompletionReadModels
                .AsNoTracking()
                .Where(c => assignedVehicleIds.Contains(c.VehicleId))
                .Select(c => new VehicleCompletionFacts(
                    c.VehicleId,
                    new CompletionFacts(c.ItemCode, c.Kind, c.PerformedAt, c.CreatedAtUtc, c.OdometerKm, c.Measurement)))
                .ToListAsync(cancellationToken))
                .GroupBy(c => c.VehicleId)
                .ToDictionary(g => g.Key, g => g.Select(c => c.Facts));

        var result = new Dictionary<Guid, VehiclePm>(vehicles.Count);
        foreach (var vehicle in vehicles)
        {
            var assignment = assignmentsByVehicle.GetValueOrDefault(vehicle.Id);
            var plan = assignment is null ? null : plansById.GetValueOrDefault(assignment.PlanId);
            var latestCompletions = assignment is null
                ? []
                : FoldLatest(completionsByVehicle.GetValueOrDefault(vehicle.Id) ?? []);

            result[vehicle.Id] = new VehiclePm(
                vehicle.OdometerKm, assignment?.AssignedAtUtc, plan, latestCompletions);
        }

        return result;
    }

    /// <summary>
    /// Folds the append-only completion rows to the latest per (code, kind) — latest by
    /// performed date, entry order (CreatedAtUtc) breaking same-day ties.
    /// </summary>
    private static Dictionary<(string Code, string Kind), CompletionFacts> FoldLatest(
        IEnumerable<CompletionFacts> completions)
    {
        var latest = new Dictionary<(string, string), CompletionFacts>();
        foreach (var completion in completions)
        {
            var key = (completion.ItemCode, completion.Kind);
            if (!latest.TryGetValue(key, out var current)
                || completion.PerformedAt > current.PerformedAt
                || (completion.PerformedAt == current.PerformedAt && completion.CreatedAtUtc > current.CreatedAtUtc))
            {
                latest[key] = completion;
            }
        }

        return latest;
    }

    private static CompletionFacts? LatestFor(
        Dictionary<(string Code, string Kind), CompletionFacts> latest, string code, PmEntryKind kind) =>
        latest.GetValueOrDefault((code, kind.ToString()));

    /// <summary>
    /// The one lastDone→Compute pipeline every view shares: resolves the latest completion
    /// for a code and runs <see cref="PmSchedule.Compute"/> on it.
    /// </summary>
    private static (CompletionFacts? LastDone, PmDueStatus Status) ComputeLine(
        VehiclePm pm,
        DateOnly today,
        string code,
        PmEntryKind kind,
        int? intervalKm,
        int? intervalMonths,
        int? leadKm,
        int? leadDays)
    {
        var lastDone = LatestFor(pm.LatestCompletions, code, kind);
        var status = PmSchedule.Compute(
            intervalKm,
            intervalMonths,
            lastDone is null ? null : new PmLastDone(lastDone.OdometerKm, lastDone.PerformedAt),
            pm.OdometerKm,
            today,
            leadKm,
            leadDays);
        return (lastDone, status);
    }

    /// <summary>Computes the status entry list — every item, then every overhaul.</summary>
    private static List<PmEntryStatusResponse> ComputeEntries(VehiclePm pm, DateOnly today)
    {
        var entries = new List<PmEntryStatusResponse>(pm.Plan!.Items.Count + pm.Plan.Overhauls.Count);

        foreach (var item in pm.Plan.Items)
        {
            var (lastDone, status) = ComputeLine(
                pm, today, item.Code, PmEntryKind.Item,
                item.IntervalKm, item.IntervalMonths, item.LeadKm, item.LeadDays);

            entries.Add(MaintenanceResponseMapper.ToEntryStatus(
                item, lastDone?.OdometerKm, lastDone?.PerformedAt, status));
        }

        foreach (var overhaul in pm.Plan.Overhauls)
        {
            var (lastDone, status) = ComputeLine(
                pm, today, overhaul.Code, PmEntryKind.Overhaul,
                overhaul.IntervalKm, overhaul.IntervalMonths, overhaul.LeadKm, overhaul.LeadDays);

            entries.Add(MaintenanceResponseMapper.ToEntryStatus(
                overhaul, lastDone?.OdometerKm, lastDone?.PerformedAt, status));
        }

        return entries;
    }

    private static MaintenancePlanResponse ToPlanResponse(MaintenancePlanReadModel plan) => new(
        plan.Id,
        plan.Name,
        plan.VehicleModel,
        plan.ServiceClass,
        plan.Notes,
        plan.Items.Select(MaintenanceResponseMapper.ToResponse).ToList(),
        plan.Overhauls.Select(MaintenanceResponseMapper.ToResponse).ToList(),
        plan.CreatedAtUtc,
        plan.UpdatedAtUtc);

    /// <summary>One vehicle's id + current odometer — the caller-supplied half of <see cref="VehiclePm"/>.</summary>
    private sealed record VehicleOdometer(Guid Id, int OdometerKm);

    /// <summary>The assignment columns the loader needs — never the whole row.</summary>
    private sealed record AssignmentFacts(Guid VehicleId, Guid PlanId, DateTimeOffset AssignedAtUtc);

    /// <summary>One vehicle's PM inputs: odometer, assignment time (+plan), folded completions.</summary>
    private sealed record VehiclePm(
        int OdometerKm,
        DateTimeOffset? AssignedAtUtc,
        MaintenancePlanReadModel? Plan,
        Dictionary<(string Code, string Kind), CompletionFacts> LatestCompletions);

    /// <summary>The completion columns the fold and due math need — never the whole row.</summary>
    private sealed record CompletionFacts(
        string ItemCode,
        string Kind,
        DateOnly PerformedAt,
        DateTimeOffset CreatedAtUtc,
        int OdometerKm,
        string? Measurement);

    /// <summary>A <see cref="CompletionFacts"/> tagged with its vehicle, for the batch fold.</summary>
    private sealed record VehicleCompletionFacts(Guid VehicleId, CompletionFacts Facts);
}
