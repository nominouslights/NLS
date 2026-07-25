using NorthernLink.Drivers.Domain.Credentials.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Credentials;

/// <summary>
/// A compliance credential held by a driver — licence class card, Police Record Check,
/// Drug &amp; Alcohol, First Aid, Work Permit, …. <see cref="Type"/> stays a free string so
/// dispatch can track partner-specific credentials without a schema change. Expiry status
/// (valid / expiring / expired) is derived by the frontend from <see cref="Expiry"/>, so
/// it is never stored.
///
/// Its own aggregate (like Fleet's VehicleDocument): added and removed whole, with one
/// deliberate exception — the image reference (<see cref="ImageKey"/>) can be set/replaced
/// independently without a full credential edit, so a photo can be attached or replaced
/// after creation without requiring a general-purpose credential-update feature.
/// </summary>
public sealed class DriverCredential : AggregateRoot, ITenantScoped
{
    private DriverCredential()
    {
        // EF Core materialization only.
        Type = null!;
        Label = null!;
    }

    public Guid TenantId { get; private set; }
    public Guid DriverId { get; private set; }
    public string Type { get; private set; }
    public string Label { get; private set; }
    public DateOnly Issued { get; private set; }
    public DateOnly? Expiry { get; private set; }
    public bool Optional { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string? ImageKey { get; private set; }
    public string? ImageContentType { get; private set; }

    public static Result<DriverCredential> Add(
        Guid tenantId,
        Guid driverId,
        string type,
        string label,
        DateOnly issued,
        DateOnly? expiry,
        bool optional,
        string? note)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return Result.Failure<DriverCredential>(CredentialErrors.TypeRequired);
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return Result.Failure<DriverCredential>(CredentialErrors.LabelRequired);
        }

        if (expiry is { } expiryDate && expiryDate < issued)
        {
            return Result.Failure<DriverCredential>(CredentialErrors.ExpiryBeforeIssued);
        }

        var credential = new DriverCredential
        {
            TenantId = tenantId,
            DriverId = driverId,
            Type = type.Trim(),
            Label = label.Trim(),
            Issued = issued,
            Expiry = expiry,
            Optional = optional,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        credential.Raise(new DriverCredentialAddedDomainEvent(credential.Id, driverId, tenantId));

        return Result.Success(credential);
    }

    public void SetImage(string imageKey, string imageContentType)
    {
        ImageKey = imageKey;
        ImageContentType = imageContentType;

        Raise(new DriverCredentialImageSetDomainEvent(Id, DriverId, TenantId));
    }
}
