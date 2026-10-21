namespace NorthernLink.Trips.Application.Trips.GetActivity;

/// <summary>
/// One entry in a trip's activity (audit) timeline — a single journaled domain event on
/// the trip itself (<c>AggregateType == "trip"</c>) or on its attached manifest
/// (<c>AggregateType == "trip-manifest"</c>). <see cref="Source"/> is the provenance carried by
/// manifest edit events (App vs Dispatcher) and is null for trip events and for the
/// manifest-created event. <see cref="EnteredBy"/> is who made the change, read from the
/// journaled payload of a manifest edit event or of any trip event that records it (today
/// <c>trip-driver-corrected</c>, taken from the signed token server-side); null otherwise.
/// <para>
/// <see cref="Reason"/> and <see cref="PreviousReason"/> are read generically from any trip
/// event's journaled payload — its <c>reason</c> and <c>clearedReason</c> fields — not from one
/// event type in particular (deliberately: other trip events rely on the same reading). Today
/// <c>trip-driver-corrected</c> carries a reason, and <c>trip-reopened</c> carries both: the
/// dispatcher's required reopen reason, and the cancel or close-without-billing reason the
/// reopen cleared off the trip. Null for every entry whose payload lacks them, and always null
/// on manifest entries.
/// </para>
/// <para>
/// <see cref="Detail"/> is a short server-composed summary of what changed — for
/// <c>trip-driver-corrected</c>, "Driver: {previous ?? none} → {new}"; null for every other
/// entry. <see cref="Reason"/>, <see cref="Detail"/> and <see cref="PreviousReason"/> are
/// additive and nullable, so older clients ignore them.
/// </para>
/// <para>
/// <b>Redaction:</b> for a caller without a dispatch role (a driver reading their own trip),
/// <see cref="Reason"/>, <see cref="PreviousReason"/>, <see cref="Detail"/> and
/// <see cref="EnteredBy"/> are always null on trip events — the entry and its
/// <see cref="EventType"/> still appear. Manifest entries are unaffected.
/// </para>
/// </summary>
public sealed record TripActivityEntryResponse(
    DateTimeOffset OccurredAtUtc,
    string AggregateType,
    string EventType,
    string? Source,
    string? EnteredBy,
    string? Reason = null,
    string? Detail = null,
    string? PreviousReason = null);
