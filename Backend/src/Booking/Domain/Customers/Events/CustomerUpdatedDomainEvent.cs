using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Customers.Events;

/// <summary>Raised when a customer is edited. Internal only — see <see cref="CustomerCreatedDomainEvent"/>.</summary>
public sealed record CustomerUpdatedDomainEvent(Guid CustomerId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
