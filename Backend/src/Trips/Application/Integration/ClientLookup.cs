namespace NorthernLink.Trips.Application.Integration;

/// <summary>
/// Trips' replica of a client organization, maintained by upserting
/// <c>ClientChangedIntegrationEvent</c>s — never joined from the Clients module (domain
/// libraries never reference each other). A plain keyed row (not an aggregate), used to
/// snapshot client identity onto trips and schedule templates. Type and ServiceType
/// travel as the event's string form; ServiceType is null for VendorPartner clients.
/// </summary>
public sealed class ClientLookup
{
    public Guid ClientId { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? ServiceType { get; set; }
    public string Tag { get; set; } = null!;
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
