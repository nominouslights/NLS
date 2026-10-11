using NorthernLink.Drivers.Domain.Hos;

namespace NorthernLink.Drivers.Application.Abstractions;

/// <summary>
/// Write-side persistence for the <see cref="DutyDay"/> aggregate (tenant-scoped: EF query
/// filter + Postgres RLS).
/// <para>
/// The range loads serve the <b>queries</b> too, deliberately. The rule engine's figures must
/// include the event a driver appended a second ago — a tablet that logs "Driving" and then
/// shows yesterday's remaining hours for a projection poll reads as broken, and an
/// offline-first queue reconciles against exactly this read after each drain. <c>rm_duty_days</c>
/// is the roster's and the audit's copy, not the figure's source.
/// </para>
/// </summary>
public interface IDutyDayRepository
{
    /// <summary>The driver's day, tracked for mutation, or null when none exists yet.</summary>
    Task<DutyDay?> GetAsync(Guid driverId, DateOnly localDate, CancellationToken cancellationToken = default);

    /// <summary>The driver's days with <c>LocalDate</c> in [from, to], untracked, ascending.</summary>
    Task<IReadOnlyList<DutyDay>> LoadRangeAsync(Guid driverId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Every driver's days with <c>LocalDate</c> in [from, to] for the current tenant, untracked.</summary>
    Task<IReadOnlyList<DutyDay>> LoadRangeForAllDriversAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>
    /// The first day the driver has any record for — the ledger's <c>KnownFrom</c> until the
    /// prior-records attestation exists. Null when the driver has no ledger at all.
    /// </summary>
    Task<DateOnly?> EarliestLocalDateAsync(Guid driverId, CancellationToken cancellationToken = default);

    /// <summary>The same, for every driver in the tenant with at least one day.</summary>
    Task<IReadOnlyDictionary<Guid, DateOnly>> EarliestLocalDatesAsync(CancellationToken cancellationToken = default);

    /// <summary>True when the driver exists for the current tenant — the referential guard.</summary>
    Task<bool> DriverExistsAsync(Guid driverId, CancellationToken cancellationToken = default);

    void Add(DutyDay day);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
