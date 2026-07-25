using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Credentials.Add;

/// <summary>Attaches a credential to a driver. Returns the new credential's id.</summary>
public sealed record AddDriverCredentialCommand(
    Guid TenantId,
    Guid DriverId,
    string Type,
    string Label,
    DateOnly Issued,
    DateOnly? Expiry,
    bool Optional,
    string? Note) : ICommand<Guid>;
