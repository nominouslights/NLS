using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Trips;

public sealed record GetArchivedTripsQuery : IQuery<IReadOnlyList<ArchivedTripResult>>;

public sealed record ArchivedTripResult(
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
    int PassengerCount,
    DateTime? DeletedAt);
