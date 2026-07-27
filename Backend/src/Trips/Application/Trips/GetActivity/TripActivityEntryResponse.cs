namespace NorthernLink.Trips.Application.Trips.GetActivity;

/// <summary>
/// One entry in a trip's activity (audit) timeline — a single journaled domain event on
/// the trip itself (<c>AggregateType == "trip"</c>) or on its attached manifest
/// (<c>AggregateType == "trip-manifest"</c>). <see cref="Source"/> and
/// <see cref="EnteredBy"/> are the provenance carried by manifest edit events
/// (App vs Dispatcher, and who), and are null for trip lifecycle events and for the
/// manifest-created event (which carries no provenance).
/// </summary>
public sealed record TripActivityEntryResponse(
    DateTimeOffset OccurredAtUtc,
    string AggregateType,
    string EventType,
    string? Source,
    string? EnteredBy);
