using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Application.Vehicles.Queries.Vehicles;

public sealed record GetVehiclesQuery : IQuery<IReadOnlyList<VehicleListItemResult>>;

public sealed record VehicleListItemResult(
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
    bool IsRegistrationExpiringSoon,
    bool IsInsuranceExpiringSoon,
    int ReadinessScore,
    IReadOnlyList<string> Alerts,
    bool IsActive);
