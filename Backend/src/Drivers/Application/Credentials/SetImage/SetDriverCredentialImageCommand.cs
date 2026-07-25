using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Credentials.SetImage;

public sealed record SetDriverCredentialImageCommand(
    Guid TenantId,
    Guid CredentialId,
    Guid DriverId,
    string ImageKey,
    string ImageContentType) : ICommand;
