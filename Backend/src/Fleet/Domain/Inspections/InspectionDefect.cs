namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>One defect found during an inspection. Persisted as jsonb.</summary>
public sealed record InspectionDefect
{
    public required string Item { get; init; }
    public InspectionDefectSeverity Severity { get; init; }
    public string? Note { get; init; }
}
