namespace NorthernLink.Trips.Application.Trips;

/// <summary>
/// Public contract for a trip (list and detail — same shape). Enum-typed fields travel
/// as their string names; "open — needs coverage" and "empty leg" chips are frontend
/// derivations from <see cref="Status"/> + <see cref="DriverId"/> / <see cref="IsEmptyLeg"/>.
/// </summary>
public sealed record TripResponse(
    Guid Id,
    string TripNumber,
    DateOnly ServiceDate,
    TimeOnly WindowStart,
    TimeOnly? WindowEnd,
    string ServiceType,
    Guid? RouteId,
    string RouteName,
    string Origin,
    string Destination,
    IReadOnlyList<TripStopResponse> Stops,
    int DistanceKm,
    Guid? ScheduleTemplateId,
    string? RoundTripKey,
    string? Direction,
    bool IsEmptyLeg,
    Guid? ClientId,
    string? ClientName,
    string? PoNumber,
    Guid? DriverId,
    string? DriverName,
    Guid? VehicleId,
    string? VehicleUnit,
    int? SeatsCapacity,
    int SeatsConfirmed,
    int? SeatsMinimum,
    bool DemandGuaranteed,
    string Status,
    Guid? ManifestId,
    bool HasPostTripInspection,
    DateTimeOffset? CompletedAtUtc,
    string? CancelledReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>One ordered stop on a trip's route snapshot.</summary>
public sealed record TripStopResponse(string Name, int Order);
