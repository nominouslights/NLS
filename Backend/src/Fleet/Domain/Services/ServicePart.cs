namespace NorthernLink.Fleet.Domain.Services;

/// <summary>A part consumed during a service (SKU + quantity). Persisted as jsonb.</summary>
public sealed record ServicePart
{
    public required string Sku { get; init; }
    public int Qty { get; init; }
}
