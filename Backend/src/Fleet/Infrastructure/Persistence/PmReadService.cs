using Microsoft.EntityFrameworkCore;
using NorthernLink.Fleet.Application.Abstractions;
using NorthernLink.Fleet.Application.Maintenance;
using NorthernLink.Fleet.Domain.Maintenance;
using NorthernLink.Fleet.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Fleet.Infrastructure.Persistence;

/// <summary>
/// Read side of preventative maintenance — folds rm_pm_completions to the latest row per
/// (code, kind) and runs <see cref="PmSchedule.Compute"/> per plan line, in C# (250 items ×
/// fleet size is trivial, and it keeps the one-calculator rule); the fold reads only the
/// columns due math needs, never whole completion rows. The vehicle's current odometer
/// (rm_vehicles) and today's UTC date are resolved in one place here — the single source
/// every PM computation shares. Completions are folded per vehicle across plan switches:
/// codes are the identity, so a reassignment does not erase the history of lines both plans
/// share.
/// </summary>
internal sealed class PmReadService(FleetDbContext context) : IPmReadService
{
    /// <summary>The system heading overhaul entries are grouped under (items carry their own).</summary>
    private const string OverhaulsSystem = "Overhauls";

    /// <summary>Hard ceiling on a caller-supplied history limit.</summary>
    private const int MaxHistoryLimit = 1000;

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

        var assignedCounts = await context.PlanAssignmentReadModels
            .AsNoTracking()
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
            ComputeEntries(pm, Today()));
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

        var entries = ComputeEntries(pm, Today());

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
        var today = Today();

        var overhauls = pm.Plan.Overhauls.Select(overhaul =>
        {
            var (lastDone, status) = ComputeLine(
                pm, today, overhaul.Code, PmEntryKind.Overhaul,
                overhaul.IntervalKm, overhaul.IntervalMonths, overhaul.LeadKm, overhaul.LeadDays);

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

            return new OverhaulStatusResponse(
                overhaul.Code,
                overhaul.Component,
                overhaul.IntervalKm,
                overhaul.IntervalMonths,
                overhaul.LabourHours,
                overhaul.PartsCad,
                overhaul.Scope,
                overhaul.ConditionTriggers,
                lastDone?.OdometerKm,
                lastDone?.PerformedAt,
                status.NextDueKm,
                status.NextDueDate,
                status.KmRemaining,
                status.DaysRemaining,
                status.State.ToString(),
                relatedMeasurements);
        }).ToList();

        return new PmOverhaulsResponse(true, pm.Plan.Id, pm.Plan.Name, pm.OdometerKm, overhauls);
    }

    public async Task<IReadOnlyList<PmCompletionResponse>?> GetHistoryAsync(
        Guid vehicleId, int limit = 200, CancellationToken cancellationToken = default)
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
            .Take(Math.Clamp(limit, 1, MaxHistoryLimit))
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
        // One query per table; the fold and due math run in C# (fleet sizes are small).
        var assignments = await context.PlanAssignmentReadModels
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return new FleetPmDueResponse([]);
        }

        var vehicles = await context.VehicleReadModels
            .AsNoTracking()
            .Where(v => v.DisposedAtUtc == null)
            .Select(v => new { v.Id, v.UnitNumber, v.OdometerKm })
            .ToListAsync(cancellationToken);
        var vehiclesById = vehicles.ToDictionary(v => v.Id);

        var planIds = assignments.Select(a => a.PlanId).Distinct().ToList();
        var plansById = await context.MaintenancePlanReadModels
            .AsNoTracking()
            .Where(p => planIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var assignedVehicleIds = assignments.Select(a => a.VehicleId).ToList();
        var completionsByVehicle = (await context.PmCompletionReadModels
            .AsNoTracking()
            .Where(c => assignedVehicleIds.Contains(c.VehicleId))
            .Select(c => new VehicleCompletionFacts(
                c.VehicleId,
                new CompletionFacts(c.ItemCode, c.Kind, c.PerformedAt, c.CreatedAtUtc, c.OdometerKm, c.Measurement)))
            .ToListAsync(cancellationToken))
            .GroupBy(c => c.VehicleId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Facts));

        var today = Today();
        var rows = new List<FleetVehiclePmDueResponse>(assignments.Count);
        foreach (var assignment in assignments)
        {
            // A disposed vehicle drops out here; a missing vehicle or plan row is
            // projection lag and reads as "not on the dashboard yet" rather than erroring.
            if (!vehiclesById.TryGetValue(assignment.VehicleId, out var vehicle)
                || !plansById.TryGetValue(assignment.PlanId, out var plan))
            {
                continue;
            }

            var pm = new VehiclePm(
                vehicle.OdometerKm,
                assignment.AssignedAtUtc,
                plan,
                FoldLatest(completionsByVehicle.GetValueOrDefault(assignment.VehicleId) ?? []));

            var due = DueOnly(ComputeEntries(pm, today));

            rows.Add(new FleetVehiclePmDueResponse(
                vehicle.Id,
                vehicle.UnitNumber,
                vehicle.OdometerKm,
                plan.Id,
                plan.Name,
                due.Count(e => e.State == nameof(PmDueState.DueSoon)),
                due.Count(e => e.State == nameof(PmDueState.Overdue)),
                due));
        }

        return new FleetPmDueResponse(rows
            .OrderByDescending(r => r.OverdueCount)
            .ThenByDescending(r => r.DueSoonCount)
            .ThenBy(r => r.UnitNumber, StringComparer.Ordinal)
            .ToList());
    }

    /// <summary>The single source of "today" every due computation uses.</summary>
    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>The DueSoon/Overdue subset of a computed entry list, plan order preserved.</summary>
    private static List<PmEntryStatusResponse> DueOnly(IEnumerable<PmEntryStatusResponse> entries) =>
        [.. entries.Where(e => e.State is nameof(PmDueState.DueSoon) or nameof(PmDueState.Overdue))];

    /// <summary>
    /// Loads everything one vehicle's PM computations need — three round trips: the odometer,
    /// the assignment left-joined to its plan (tolerant of the plan row lagging the
    /// projection), and the folded completion columns. Null when the vehicle does not exist;
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

        // Assignment and plan in one query: a correlated FirstOrDefault translates to a
        // left join, so a projection-lag gap (assignment landed, plan row not yet) reads as
        // unassigned for a moment rather than erroring.
        var assignment = await context.PlanAssignmentReadModels
            .AsNoTracking()
            .Where(a => a.VehicleId == vehicleId)
            .Select(a => new
            {
                a.AssignedAtUtc,
                Plan = context.MaintenancePlanReadModels.FirstOrDefault(p => p.Id == a.PlanId),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // The completions query does not depend on the plan having resolved — only on an
        // assignment existing at all (no assignment means no due math to feed).
        var latestCompletions = assignment is null
            ? []
            : FoldLatest(await context.PmCompletionReadModels
                .AsNoTracking()
                .Where(c => c.VehicleId == vehicleId)
                .Select(c => new CompletionFacts(
                    c.ItemCode, c.Kind, c.PerformedAt, c.CreatedAtUtc, c.OdometerKm, c.Measurement))
                .ToListAsync(cancellationToken));

        return new VehiclePm(vehicle.OdometerKm, assignment?.AssignedAtUtc, assignment?.Plan, latestCompletions);
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

            entries.Add(new PmEntryStatusResponse(
                item.Code,
                nameof(PmEntryKind.Item),
                item.System,
                item.Component,
                item.Tier.ToString(),
                item.Task.ToString(),
                item.IntervalKm,
                item.IntervalMonths,
                item.ShopMinutes,
                lastDone?.OdometerKm,
                lastDone?.PerformedAt,
                status.NextDueKm,
                status.NextDueDate,
                status.KmRemaining,
                status.DaysRemaining,
                status.State.ToString()));
        }

        foreach (var overhaul in pm.Plan.Overhauls)
        {
            var (lastDone, status) = ComputeLine(
                pm, today, overhaul.Code, PmEntryKind.Overhaul,
                overhaul.IntervalKm, overhaul.IntervalMonths, overhaul.LeadKm, overhaul.LeadDays);

            entries.Add(new PmEntryStatusResponse(
                overhaul.Code,
                nameof(PmEntryKind.Overhaul),
                OverhaulsSystem,
                overhaul.Component,
                Tier: null,
                Task: null,
                overhaul.IntervalKm,
                overhaul.IntervalMonths,
                OverhaulShopMinutes(overhaul.LabourHours),
                lastDone?.OdometerKm,
                lastDone?.PerformedAt,
                status.NextDueKm,
                status.NextDueDate,
                status.KmRemaining,
                status.DaysRemaining,
                status.State.ToString()));
        }

        return entries;
    }

    /// <summary>An overhaul's contribution to shop time — its labour hours in minutes.</summary>
    private static int OverhaulShopMinutes(decimal labourHours) =>
        (int)Math.Round(labourHours * 60m, MidpointRounding.AwayFromZero);

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

    /// <summary>A <see cref="CompletionFacts"/> tagged with its vehicle, for the fleet-wide fold.</summary>
    private sealed record VehicleCompletionFacts(Guid VehicleId, CompletionFacts Facts);
}
