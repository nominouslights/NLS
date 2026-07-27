using NorthernLink.Shared.Kernel;

namespace NorthernLink.Clients.Domain.ClientContacts.Events;

/// <summary>
/// Raised on BOTH the demoted and the promoted contact when the primary contact for a client
/// changes. Every aggregate write must raise an event — the projection worker polls
/// <c>event_journal</c>, and an eventless write produces no journal row, leaving the read model
/// silently stale; both rows must raise so both <c>rm_client_contacts</c> rows re-project.
/// Internal to the module: <c>ClientsIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record ClientContactPrimaryChangedDomainEvent(Guid ContactId, Guid ClientId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
