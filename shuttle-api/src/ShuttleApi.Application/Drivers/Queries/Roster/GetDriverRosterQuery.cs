using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Queries.Roster;

public sealed record GetDriverRosterQuery(Guid DriverId, DateOnly RangeStart, DateOnly RangeEnd)
    : IQuery<IReadOnlyList<RosterEntryResult>>;

public sealed record RosterEntryResult(
    Guid Id,
    DateOnly EntryDate,
    string Status,
    TimeOnly? ShiftStart,
    TimeOnly? ShiftEnd);
