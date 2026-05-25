using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Drivers;

namespace ShuttleApi.Application.Drivers.Commands.Roster;

public sealed record UpsertRosterEntryCommand(
    Guid DriverId,
    DateOnly EntryDate,
    RosterStatus Status,
    TimeOnly? ShiftStart,
    TimeOnly? ShiftEnd) : ICommand<UpsertRosterEntryResult>;

public sealed record UpsertRosterEntryResult(Guid EntryId);
