using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.Qbo.Events;

/// <summary>
/// Raised when a tenant connects a QuickBooks company, and again on every reconnect of that same
/// company (<paramref name="IsReconnect"/>). Carries no token material: the event lands in
/// <c>event_journal</c>, which is an audit record, not a vault.
/// </summary>
public sealed record QboConnectedDomainEvent(
    Guid ConnectionId,
    Guid TenantId,
    string RealmId,
    QboEnvironment Environment,
    Guid ConnectedBy,
    bool IsReconnect) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
