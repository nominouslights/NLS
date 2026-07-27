using NorthernLink.Shared.Messaging;

namespace NorthernLink.Clients.Application.ClientContacts.SetPrimary;

/// <summary>
/// Promotes one contact to primary for its client, demoting whichever contact was primary
/// before. No-op success when the target is already primary.
/// </summary>
public sealed record SetPrimaryClientContactCommand(Guid TenantId, Guid ClientId, Guid ContactId) : ICommand;
