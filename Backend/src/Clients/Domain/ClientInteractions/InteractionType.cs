namespace NorthernLink.Clients.Domain.ClientInteractions;

/// <summary>
/// The kind of touchpoint an interaction records. Declared per-module (never shared); persisted
/// as the enum name string. The wire form of <see cref="SiteVisit"/> is "Site Visit" (with a
/// space) — that display/wire mapping happens at the API DTO boundary, never in the DB.
/// </summary>
public enum InteractionType
{
    Call,
    Meeting,
    Email,
    SiteVisit,
    Other,
}
