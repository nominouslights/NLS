using NorthernLink.Shared.Messaging;

namespace NorthernLink.Fleet.Application.Vehicles.Register;

/// <summary>
/// Registers a new vehicle. <paramref name="TenantId"/> is stamped by the endpoint layer
/// from the ambient tenant context. Returns the new vehicle's id.
/// </summary>
public sealed record RegisterVehicleCommand(
    Guid TenantId,
    string UnitNumber,
    string Vin,
    string Make,
    string Model,
    int Year,
    int SeatingCapacity,
    string LicencePlate,
    string RequiredLicenceClass,
    int OdometerKm,
    decimal AcquisitionCostCad,
    int EndOfLifeKm) : ICommand<Guid>;
