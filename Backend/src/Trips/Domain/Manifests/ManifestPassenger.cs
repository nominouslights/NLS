namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>One §5 passenger row (the form has space for eight). Persisted as jsonb.</summary>
public sealed record ManifestPassenger
{
    public required string Name { get; init; }
    public string? Contact { get; init; }
    public string? Pickup { get; init; }
    public string? Dropoff { get; init; }
    public bool IdVerified { get; init; }
    public bool BoardedOn { get; init; }
    public bool BoardedOff { get; init; }
}
