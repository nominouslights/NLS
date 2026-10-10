using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.CostCentres.Events;

/// <summary>
/// Raised when a cost centre's descriptive details (name, description, owner, parent) actually
/// change. The code never does. Not raised for an edit that changes nothing.
/// </summary>
public sealed record CostCentreUpdatedDomainEvent(Guid CostCentreId, Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
