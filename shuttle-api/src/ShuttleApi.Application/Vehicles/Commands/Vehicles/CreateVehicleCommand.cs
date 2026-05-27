using ShuttleApi.Application.Common.Interfaces;
using ShuttleApi.Domain.Vehicles;

namespace ShuttleApi.Application.Vehicles.Commands.Vehicles;

public sealed record CreateVehicleCommand(
    string UnitCode,
    string VIN,
    string Make,
    string Model,
    int Year,
    string Color,
    string LicensePlate,
    string Province,
    VehicleType VehicleType,
    int PassengerCapacity,
    int CurrentOdometerKm,
    DateTime AcquisitionDate,
    DateTime? RegistrationExpiry,
    string? InsuranceProvider,
    string? InsurancePolicyNumber,
    DateTime? InsuranceExpiry,
    string? Notes) : ICommand<CreateVehicleResult>;

public sealed record CreateVehicleResult(Guid Id);
