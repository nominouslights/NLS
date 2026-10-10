using NorthernLink.Shared.Kernel;

namespace NorthernLink.Budgeting.Domain.CostCentres.Events;

/// <summary>
/// Raised when a cost centre is retired or brought back. A flag flip, never a delete: budget
/// codes in earlier periods carry the cost-centre string and must keep resolving to an entry.
/// Not raised when the cost centre is already in the requested state.
/// </summary>
public sealed record CostCentreActivationChangedDomainEvent(
    Guid CostCentreId,
    bool IsActive,
    Guid? ActorId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
