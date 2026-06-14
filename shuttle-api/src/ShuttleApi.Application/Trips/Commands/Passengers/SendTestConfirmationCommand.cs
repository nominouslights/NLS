using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record SendTestConfirmationCommand(
    Guid TripId,
    Guid PassengerId,
    string Direction,
    string TestEmailAddress) : ICommand;
