using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Queries.Vehicles;

public sealed record GetArchivedVehiclesQuery : IQuery<IReadOnlyList<ArchivedVehicleResult>>;

public sealed record ArchivedVehicleResult(
    Guid Id,
    string UnitCode,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string VehicleType,
    string Status,
    string? StatusNote,
    int PassengerCapacity,
    int CurrentOdometerKm,
    bool IsActive,
    DateTime? DeletedAt);
