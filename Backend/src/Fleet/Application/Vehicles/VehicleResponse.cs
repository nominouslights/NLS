namespace NorthernLink.Fleet.Application.Vehicles;

/// <summary>
/// The Fleet module's public representation of a vehicle — the shape every frontend
/// consumes. <paramref name="Status"/> is the <c>VehicleStatus</c> name as a string
/// (Active, InMaintenance, OutOfService, Retired, Sold, Recycled).
/// </summary>
public sealed record VehicleResponse(
    Guid Id,
    string UnitNumber,
    string Vin,
    string Make,
    string Model,
    int Year,
    int SeatingCapacity,
    string LicencePlate,
    string RequiredLicenceClass,
    string Status,
    string? StatusReason,
    int OdometerKm,
    decimal AcquisitionCostCad,
    int EndOfLifeKm,
    decimal CurrentValueCad,
    int RemainingKm,
    decimal LifeUsedPct,
    bool RequiresPeriodicInspection,
    decimal? SalePriceCad,
    DateTimeOffset? DisposedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
