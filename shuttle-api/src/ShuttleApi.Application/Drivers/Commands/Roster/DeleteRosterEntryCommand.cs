using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Drivers.Commands.Roster;

public sealed record DeleteRosterEntryCommand(Guid DriverId, Guid EntryId) : ICommand;
