using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Trips;

namespace ShuttleApi.Application.Trips;

public sealed record GetTripsQuery(
    TripStatus? Status,
    Guid? ClientId,
    Guid? DriverId,
    Guid? VehicleId,
    TripServiceType? ServiceType) : IQuery<IReadOnlyList<TripListItemResult>>;

public sealed record TripListItemResult(
    Guid Id,
    Guid? ClientId,
    Guid? VehicleId,
    Guid? DriverId,
    string ServiceType,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    string? VehicleType,
    DateTime ScheduledAt,
    string Status,
    string? Notes,
    DateTime CreatedAt,
    int StopCount,
    string? FirstStopLocation,
    string? LastStopLocation,
    int? SeatCapacity,
    int PassengerCount);
