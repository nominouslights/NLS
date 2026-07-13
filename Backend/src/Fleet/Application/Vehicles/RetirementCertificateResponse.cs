namespace NorthernLink.Fleet.Application.Vehicles;

/// <summary>
/// A vehicle's retirement certificate — snapshot data rendered by the Dispatcher as a
/// printable certificate view. Certificate numbers follow <c>RC-{year}-{sequence}</c>.
/// </summary>
public sealed record RetirementCertificateResponse(
    Guid Id,
    Guid VehicleId,
    string CertificateNumber,
    string Vin,
    string UnitNumber,
    string Make,
    string Model,
    int Year,
    int FinalOdometerKm,
    string RetirementReason,
    DateTimeOffset RetiredAtUtc,
    DateTimeOffset IssuedAtUtc);
