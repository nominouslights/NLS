using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record AddPassengerCommand(
    Guid TripId,
    string Name,
    string? ContactInfo,
    int? SeatNumber,
    PassengerPaymentStatus PaymentStatus,
    string? Phone = null,
    string? Email = null,
    bool IsAddedAfterDeparture = false) : ICommand<AddPassengerResult>;

public sealed record AddPassengerResult(Guid PassengerId);
