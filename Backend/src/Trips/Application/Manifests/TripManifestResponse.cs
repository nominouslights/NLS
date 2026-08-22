namespace NorthernLink.Trips.Application.Manifests;

/// <summary>
/// The Trips module's public representation of a passenger + cargo manifest — the payload
/// every frontend consumes. Enum-typed fields travel as their enum names ("App",
/// "Dispatcher", "Outbound", "NotApplicable", …). Passengers carry snapshot references to
/// the trip's route stops, and — on community runs, which collect from riders rather than
/// invoicing a client — the fare each one paid, with the run's rollup on the manifest itself.
/// Nothing reconciles those fares to QuickBooks yet; they record what was written down.
/// </summary>
public sealed record TripManifestResponse(
    Guid Id,
    DateOnly TripDate,
    string TripNumber,
    string Route,
    string? Direction,
    string? Client,
    IReadOnlyList<PassengerResponse> Passengers,
    bool AllSeatbeltsVerified,
    IReadOnlyList<CargoItemResponse> Cargo,
    string? AllCargoSecured,
    string Source,
    string? EnteredBy,
    DateTimeOffset? EnteredAt,
    DateTimeOffset CreatedAtUtc,
    decimal FaresCollectedCad,
    int FaresPaidCount,
    int FaresWaivedCount);

/// <summary>§5 row. Pickup/dropoff are snapshot references to the trip's route stops.</summary>
public sealed record PassengerResponse(
    string Name,
    string? Email,
    string? Phone,
    Guid? PickupStopId,
    string? PickupStopName,
    Guid? DropoffStopId,
    string? DropoffStopName,
    bool IdVerified,
    bool BoardedOn,
    bool BoardedOff,
    decimal? FareAmountCad,
    string? FarePaymentMethod,
    DateTimeOffset? FarePaidAtUtc);

/// <summary>§6 row.</summary>
public sealed record CargoItemResponse(
    string Description,
    string? OwnerRecipient,
    decimal? WeightKg,
    decimal? ChargeCad,
    bool Hazmat,
    bool Secured);
