using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Queries.Roster;

public sealed record GetFleetRosterQuery(DateOnly RangeStart, DateOnly RangeEnd)
    : IQuery<IReadOnlyList<DriverRosterSummaryResult>>;

public sealed record DriverRosterSummaryResult(
    Guid DriverId,
    string EmployeeId,
    string FullName,
    IReadOnlyList<RosterEntryResult> Entries);
