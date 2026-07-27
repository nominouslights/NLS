namespace NorthernLink.Clients.Application.ClientInteractions;

/// <summary>
/// The Clients module's public representation of a client interaction — the shape every frontend
/// consumes. <see cref="Type"/> is the wire string: "Call", "Meeting", "Email", "Site Visit",
/// "Other" (note SiteVisit renders with a space). ParticipantContactIds references
/// <c>client_contacts</c> rows; it may be empty.
/// </summary>
public sealed record ClientInteractionResponse(
    Guid Id,
    Guid ClientId,
    string Type,
    DateOnly OccurredOn,
    string Summary,
    IReadOnlyList<Guid> ParticipantContactIds,
    DateOnly? FollowUpDate,
    string? FollowUpNote,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
