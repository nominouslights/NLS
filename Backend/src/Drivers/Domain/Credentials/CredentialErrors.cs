using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Credentials;

/// <summary>All domain errors the DriverCredential aggregate (and its handlers) can produce.</summary>
public static class CredentialErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Drivers.Credential.NotFound", "The credential was not found.");

    public static readonly Error TypeRequired = Error.Validation(
        "Drivers.Credential.TypeRequired", "A credential type is required.");

    public static readonly Error LabelRequired = Error.Validation(
        "Drivers.Credential.LabelRequired", "A credential label is required.");

    public static readonly Error ExpiryBeforeIssued = Error.Validation(
        "Drivers.Credential.ExpiryBeforeIssued", "A credential cannot expire before it was issued.");
}
