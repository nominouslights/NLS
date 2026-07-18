using NorthernLink.Shared.Messaging;
using NorthernLink.Trips.Domain.Manifests;

namespace NorthernLink.Trips.Application.Manifests.Create;

/// <summary>
/// Records a completed trip manifest — the full NL-TM-01 payload. Used by the Manual
/// Trip Entry screen (Paper source) today and the Driver Field App (App source) later.
/// <paramref name="TenantId"/> is stamped by the endpoint layer from the ambient tenant
/// context; <paramref name="CertifiedAt"/> defaults to now when omitted. Returns the
/// new manifest's id.
/// </summary>
public sealed record CreateTripManifestCommand(
    Guid TenantId,
    DateOnly TripDate,
    string TripNumber,
    string Route,
    TripDirection? Direction,
    string? Client,
    string Unit,
    string DriverName,
    string? DriverLicenceNo,
    string? LicencePlate,
    int? OdometerStartKm,
    FuelLevel? FuelLevel,
    IReadOnlyList<PreTripChecklistItem> PreTrip,
    IReadOnlyList<WeatherCondition> Weather,
    string? TemperatureC,
    IReadOnlyList<RoadCondition> RoadConditions,
    VisibilityLevel? Visibility,
    string? RoadAdvisories,
    IReadOnlyList<ManifestPassenger> Passengers,
    bool AllSeatbeltsVerified,
    IReadOnlyList<ManifestCargoItem> Cargo,
    CargoSecuredStatus? AllCargoSecured,
    IReadOnlyList<string> Issues,
    bool NoIssues,
    string? DepartureTime,
    string? ArrivalTime,
    int? OdometerEndKm,
    int? TotalKm,
    bool FuelAdded,
    decimal? FuelLitres,
    decimal? FuelCostCad,
    IReadOnlyList<PostTripChecklistItem> PostTrip,
    IReadOnlyList<bool> Attestations,
    string DriverSignatureName,
    DateTimeOffset? CertifiedAt,
    ManifestSource Source,
    string? EnteredBy) : ICommand<Guid>;
