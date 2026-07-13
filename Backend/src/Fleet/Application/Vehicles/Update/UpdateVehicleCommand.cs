using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Vehicles.Update;

/// <summary>Updates a vehicle's registration details (not status or odometer).</summary>
public sealed record UpdateVehicleCommand(
    Guid TenantId,
    Guid VehicleId,
    string UnitNumber,
    string Vin,
    string Make,
    string Model,
    int Year,
    int SeatingCapacity,
    string LicencePlate,
    string RequiredLicenceClass,
    decimal AcquisitionCostCad,
    int EndOfLifeKm) : ICommand;
