namespace NorthernLink.Drivers.Application.Credentials;

/// <summary>
/// The Drivers module's public representation of a driver credential. The expiry status
/// chip (valid / expiring / expired) is derived by the frontend from <paramref name="Expiry"/>.
/// <paramref name="HasImage"/> indicates whether a photo/scan is attached; the image itself
/// is fetched via a separate proxied GET endpoint to keep object-storage access inside the
/// API's tenant isolation boundary. ImageKey and ImageContentType are internal-only (not exposed
/// in the public API contract) but included here for endpoint implementation convenience.
/// </summary>
public sealed record DriverCredentialResponse(
    Guid Id,
    Guid DriverId,
    string Type,
    string Label,
    DateOnly Issued,
    DateOnly? Expiry,
    bool Optional,
    string? Note,
    bool HasImage,
    string? ImageKey = null,
    string? ImageContentType = null);
