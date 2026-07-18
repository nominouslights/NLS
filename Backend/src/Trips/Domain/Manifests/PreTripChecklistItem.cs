namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// One §3 pre-trip inspection row. <see cref="Group"/> and <see cref="Item"/> come from
/// the canonical <see cref="ManifestChecklist.PreTripGroups"/>; a Fail must carry a
/// severity and a note (enforced by <see cref="TripManifest.Create"/>, not here — the
/// row is a dumb value carrier persisted as jsonb).
/// </summary>
public sealed record PreTripChecklistItem
{
    public required string Group { get; init; }
    public required string Item { get; init; }
    public PreTripItemStatus Status { get; init; }
    public DefectSeverity? Severity { get; init; }
    public string? Note { get; init; }
}
