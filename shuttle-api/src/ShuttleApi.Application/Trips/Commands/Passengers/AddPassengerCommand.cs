using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record AddPassengerCommand(
    Guid TripId,
    string Name,
    string? ContactInfo,
    int? SeatNumber,
    PassengerPaymentStatus PaymentStatus) : ICommand<AddPassengerResult>;

public sealed record AddPassengerResult(Guid PassengerId);
