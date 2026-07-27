namespace NorthernLink.Billing.Application.BillableTrips;

/// <summary>A billable-trip replica row as the API exposes it. <c>InvoiceId</c> null means
/// the trip is still uninvoiced (available to a draft or a manual line).</summary>
public sealed record BillableTripResponse(
    Guid Id,
    string TripNumber,
    Guid? ClientId,
    string? ClientName,
    string ServiceType,
    string RouteName,
    string Origin,
    string Destination,
    int DistanceKm,
    DateOnly ServiceDate,
    string? RoundTripKey,
    string? Direction,
    string? PoNumber,
    DateTimeOffset CompletedAtUtc,
    Guid? InvoiceId);
