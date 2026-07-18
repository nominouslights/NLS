namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>One §9 post-trip checklist row (six on the form). Persisted as jsonb.</summary>
public sealed record PostTripChecklistItem
{
    public required string Item { get; init; }
    public bool Ok { get; init; }
}
