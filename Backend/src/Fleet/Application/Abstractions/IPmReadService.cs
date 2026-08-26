using NorthernLink.Fleet.Application.Maintenance;

namespace NorthernLink.Fleet.Application.Abstractions;

/// <summary>
/// Read side of preventative maintenance — every PM query runs here, over the
/// rm_maintenance_plans / rm_pm_plan_assignments / rm_pm_completions read models (plus
/// rm_vehicles for the current odometer). Due status is computed per call by
/// <c>PmSchedule</c> from the latest completion per (code, kind), the vehicle's current
/// odometer, and today's UTC date — never stored. The vehicle-scoped methods return null
/// when the vehicle does not exist; an existing vehicle with no plan returns the
/// "assigned: false" shape instead.
/// <para>
/// Disposed-vehicle posture: the per-vehicle views (status, due, overhauls, history) stay
/// readable for a disposed vehicle — auditing a sold or recycled unit's PM record is
/// legitimate. Only the aggregate views exclude disposed units: a plan's
/// <c>AssignedVehicleCount</c> counts non-disposed vehicles, and the fleet dashboard
/// (<see cref="GetFleetDueAsync"/>) lists non-disposed vehicles only.
/// </para>
/// </summary>
public interface IPmReadService
{
    /// <summary>Newest history entries returned when the caller supplies no limit.</summary>
    public const int DefaultHistoryLimit = 200;

    /// <summary>Hard ceiling on a caller-supplied history limit.</summary>
    public const int MaxHistoryLimit = 1000;

    Task<IReadOnlyList<MaintenancePlanSummaryResponse>> GetPlansAsync(CancellationToken cancellationToken = default);

    Task<MaintenancePlanResponse?> GetPlanByIdAsync(Guid planId, CancellationToken cancellationToken = default);

    /// <summary>The full computed schedule — every item and overhaul with its due status.</summary>
    Task<VehiclePmStatusResponse?> GetVehicleStatusAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>The shop-visit package — DueSoon/Overdue entries grouped by system, with total shop minutes.</summary>
    Task<PmDueResponse?> GetDueAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>The overhaul-early decision view — overhaul status plus related Test-item measurements.</summary>
    Task<PmOverhaulsResponse?> GetOverhaulsAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverse-chronological completion history for a vehicle, capped at
    /// <paramref name="limit"/> newest entries (empty when none, null when the vehicle does
    /// not exist — same contract as the sibling vehicle-scoped methods).
    /// </summary>
    Task<IReadOnlyList<PmCompletionResponse>?> GetHistoryAsync(
        Guid vehicleId, int limit = DefaultHistoryLimit, CancellationToken cancellationToken = default);

    /// <summary>
    /// The fleet-wide dashboard view — every assigned, non-disposed vehicle with its
    /// DueSoon/Overdue/NotYetRecorded counts and due entries, most urgent fleet first.
    /// </summary>
    Task<FleetPmDueResponse> GetFleetDueAsync(CancellationToken cancellationToken = default);
}
