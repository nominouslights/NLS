using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record RemoveCargoItemCommand(Guid TripId, Guid CargoItemId) : ICommand;
