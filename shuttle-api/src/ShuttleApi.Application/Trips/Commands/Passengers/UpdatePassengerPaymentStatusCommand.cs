using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record UpdatePassengerPaymentStatusCommand(
    Guid TripId,
    Guid PassengerId,
    PassengerPaymentStatus PaymentStatus) : ICommand;
