namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// One checklist row of an inspection. <see cref="Group"/> is the manifest's §3 group
/// for pre-trip items and null for post-trip items. Persisted as jsonb.
/// </summary>
public sealed record InspectionChecklistItem
{
    public string? Group { get; init; }
    public required string Item { get; init; }
    public bool Passed { get; init; }
}
