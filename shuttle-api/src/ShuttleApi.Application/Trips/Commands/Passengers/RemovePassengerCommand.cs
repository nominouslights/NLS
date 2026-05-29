using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record RemovePassengerCommand(Guid TripId, Guid PassengerId) : ICommand;
