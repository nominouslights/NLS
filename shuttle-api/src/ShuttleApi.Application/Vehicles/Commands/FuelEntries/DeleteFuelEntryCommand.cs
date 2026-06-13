using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Commands.FuelEntries;

public sealed record DeleteFuelEntryCommand(Guid VehicleId, Guid EntryId) : ICommand;
