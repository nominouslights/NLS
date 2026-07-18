namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// One §6 cargo row (the form has space for four). <see cref="ChargeCad"/> is captured
/// here for the record; invoicing it against a Budget Code is a Billing-module follow-up.
/// Persisted as jsonb.
/// </summary>
public sealed record ManifestCargoItem
{
    public required string Description { get; init; }
    public string? OwnerRecipient { get; init; }
    public decimal? WeightKg { get; init; }
    public decimal? ChargeCad { get; init; }
    public bool Hazmat { get; init; }
    public bool Secured { get; init; }
}
