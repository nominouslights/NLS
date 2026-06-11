using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record SendPassengerConfirmationCommand(
    Guid TripId,
    Guid PassengerId,
    string Direction) : ICommand;
