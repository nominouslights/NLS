using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.IntegrationEvents.Trips;

/// <summary>
/// Published when a driver (or a dispatcher transcribing a paper form) completes a
/// Trip Manifest &amp; Vehicle Inspection Report — routing key
/// <c>trips.trip-manifest-completed</c>. Carries what Fleet needs to materialize the
/// pre- and post-trip vehicle inspection records without a library reference: the
/// vehicle unit, both checklists, and provenance. Statuses and severities travel as
/// strings: integration events are the module's public contract and never reference
/// Trips' internal enums. <see cref="TenantId"/> is part of the payload because
/// handlers run outside any HTTP request — the consuming module takes its tenant from
/// the event, not from ITenantContext.
/// </summary>
public sealed record TripManifestCompletedIntegrationEvent(
    Guid ManifestId,
    Guid TenantId,
    string TripNumber,
    string Unit,
    string DriverName,
    string Source,
    string? EnteredBy,
    DateOnly TripDate,
    DateTimeOffset PerformedAt,
    int? OdometerStartKm,
    int? OdometerEndKm,
    IReadOnlyList<TripManifestPreTripItem> PreTripItems,
    IReadOnlyList<TripManifestPostTripItem> PostTripItems) : IntegrationEvent;

/// <summary>One §3 pre-trip checklist row. Status is "Ok" or "Fail"; a Fail carries a
/// severity ("Minor", "Major", or "OutOfService") and the driver's note.</summary>
public sealed record TripManifestPreTripItem(
    string Group,
    string Item,
    string Status,
    string? Severity,
    string? Note);

/// <summary>One §9 post-trip checklist row.</summary>
public sealed record TripManifestPostTripItem(string Item, bool Ok);
