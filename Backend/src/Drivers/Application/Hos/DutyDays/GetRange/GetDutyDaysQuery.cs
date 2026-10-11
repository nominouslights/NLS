using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Hos.DutyDays.GetRange;

/// <summary>
/// Day summaries for [<paramref name="From"/>, <paramref name="To"/>], ascending, one per
/// calendar day whether or not a record exists (a missing day is reported, never skipped — the
/// regulation reads an unknown day as records incomplete). Capped at today; a range over 92
/// days is refused.
/// </summary>
public sealed record GetDutyDaysQuery(Guid TenantId, Guid DriverId, DateOnly From, DateOnly To)
    : IQuery<IReadOnlyList<DutyDaySummaryResponse>>;
