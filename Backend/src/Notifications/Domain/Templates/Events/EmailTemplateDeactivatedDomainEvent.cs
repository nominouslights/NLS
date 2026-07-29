using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Domain.Templates.Events;

/// <summary>Raised when an email template is deactivated (the platform's delete).</summary>
public sealed record EmailTemplateDeactivatedDomainEvent(Guid TemplateId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
