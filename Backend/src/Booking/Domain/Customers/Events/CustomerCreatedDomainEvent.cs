using NorthernLink.Shared.Kernel;

namespace NorthernLink.Booking.Domain.Customers.Events;

/// <summary>Raised when a customer is created. Internal only — Booking publishes nothing yet.</summary>
public sealed record CustomerCreatedDomainEvent(Guid CustomerId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
