using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record UpdatePassengerBoardingStatusCommand(
    Guid TripId,
    Guid PassengerId,
    PassengerBoardingStatus BoardingStatus) : ICommand;
