using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Domain.Templates.Events;

/// <summary>Raised when an email template's content is edited.</summary>
public sealed record EmailTemplateUpdatedDomainEvent(Guid TemplateId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
