using NorthernLink.Shared.Kernel;

namespace NorthernLink.Identity.Domain.Users.Events;

/// <summary>Raised when a user account is first created (dev seed or admin bootstrap).</summary>
public sealed record UserCreatedDomainEvent(Guid UserId, Guid TenantId, string Email, string Role) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
