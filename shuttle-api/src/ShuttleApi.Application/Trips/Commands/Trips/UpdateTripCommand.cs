using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record UpdateTripCommand(
    Guid TripId,
    Guid? VehicleId,
    string? PurchaseOrderNumber,
    string? VehicleType,
    DateTime ScheduledAt,
    string? Notes,
    IReadOnlyList<StopDto> Stops,
    int? SeatCapacity,
    decimal? PricePerSeat) : ICommand;
