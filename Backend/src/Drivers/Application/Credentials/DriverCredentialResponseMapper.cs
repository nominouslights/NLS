using NorthernLink.Drivers.Domain.Credentials;

namespace NorthernLink.Drivers.Application.Credentials;

/// <summary>Maps the DriverCredential aggregate to the module's public response contract.</summary>
public static class DriverCredentialResponseMapper
{
    public static DriverCredentialResponse ToResponse(DriverCredential credential) => new(
        credential.Id,
        credential.DriverId,
        credential.Type,
        credential.Label,
        credential.Issued,
        credential.Expiry,
        credential.Optional,
        credential.Note,
        credential.ImageKey is not null,
        credential.ImageKey,
        credential.ImageContentType);
}
