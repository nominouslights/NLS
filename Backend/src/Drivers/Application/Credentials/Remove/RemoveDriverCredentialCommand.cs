using NorthernLink.Shared.Messaging;

namespace NorthernLink.Drivers.Application.Credentials.Remove;

/// <summary>Removes a credential (hard delete — the audit journal keeps history).</summary>
public sealed record RemoveDriverCredentialCommand(Guid TenantId, Guid CredentialId) : ICommand;
