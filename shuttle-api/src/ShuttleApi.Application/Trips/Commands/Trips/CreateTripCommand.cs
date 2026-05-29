using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record CreateTripCommand(
    TripServiceType ServiceType,
    Guid? ClientId,
    Guid VehicleId,
    string? PurchaseOrderNumber,
    string? VehicleType,
    DateTime ScheduledAt,
    string? Notes,
    IReadOnlyList<StopDto> Stops,
    int? SeatCapacity,
    decimal? PricePerSeat) : ICommand<CreateTripResult>;

public sealed record CreateTripResult(Guid Id);
