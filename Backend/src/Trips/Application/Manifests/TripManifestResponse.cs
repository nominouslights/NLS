namespace NorthernLink.Trips.Application.Manifests;

/// <summary>
/// The Trips module's public representation of a completed NL-TM-01 manifest — the full
/// form payload every frontend (including the browser PDF generator) consumes. Enum-typed
/// fields travel as their enum names ("Paper", "ThreeQuarters", "OutOfService", …);
/// multi-select sets (§4) travel as string lists.
/// </summary>
public sealed record TripManifestResponse(
    Guid Id,
    DateOnly TripDate,
    string TripNumber,
    string Route,
    string? Direction,
    string? Client,
    string Unit,
    string DriverName,
    string? DriverLicenceNo,
    string? LicencePlate,
    int? OdometerStartKm,
    string? FuelLevel,
    IReadOnlyList<PreTripItemResponse> PreTrip,
    IReadOnlyList<string> Weather,
    string? TemperatureC,
    IReadOnlyList<string> RoadConditions,
    string? Visibility,
    string? RoadAdvisories,
    IReadOnlyList<PassengerResponse> Passengers,
    bool AllSeatbeltsVerified,
    IReadOnlyList<CargoItemResponse> Cargo,
    string? AllCargoSecured,
    IReadOnlyList<string> Issues,
    bool NoIssues,
    string? DepartureTime,
    string? ArrivalTime,
    int? OdometerEndKm,
    int? TotalKm,
    bool FuelAdded,
    decimal? FuelLitres,
    decimal? FuelCostCad,
    IReadOnlyList<PostTripItemResponse> PostTrip,
    IReadOnlyList<bool> Attestations,
    string DriverSignatureName,
    DateTimeOffset CertifiedAt,
    string Source,
    string? EnteredBy,
    DateTimeOffset? EnteredAt,
    DateTimeOffset CreatedAtUtc);

/// <summary>§3 row. Status is "Ok" or "Fail"; Severity is "Minor", "Major", or "OutOfService".</summary>
public sealed record PreTripItemResponse(
    string Group,
    string Item,
    string Status,
    string? Severity,
    string? Note);

/// <summary>§5 row.</summary>
public sealed record PassengerResponse(
    string Name,
    string? Contact,
    string? Pickup,
    string? Dropoff,
    bool IdVerified,
    bool BoardedOn,
    bool BoardedOff);

/// <summary>§6 row.</summary>
public sealed record CargoItemResponse(
    string Description,
    string? OwnerRecipient,
    decimal? WeightKg,
    decimal? ChargeCad,
    bool Hazmat,
    bool Secured);

/// <summary>§9 row.</summary>
public sealed record PostTripItemResponse(string Item, bool Ok);
