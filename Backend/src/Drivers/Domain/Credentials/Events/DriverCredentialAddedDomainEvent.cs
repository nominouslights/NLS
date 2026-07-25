using NorthernLink.Shared.Kernel;

namespace NorthernLink.Drivers.Domain.Credentials.Events;

/// <summary>
/// Raised when a credential is added to a driver. Every aggregate write must raise an
/// event — the projection worker polls <c>event_journal</c>, and an eventless write
/// produces no journal row, leaving the read model silently stale. Stays internal to
/// the module: <c>DriversIntegrationEventMapper</c> maps it to null.
/// </summary>
public sealed record DriverCredentialAddedDomainEvent(Guid CredentialId, Guid DriverId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
