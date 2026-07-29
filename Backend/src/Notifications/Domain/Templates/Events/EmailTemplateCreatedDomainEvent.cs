using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Domain.Templates.Events;

/// <summary>Raised when an email template is first created.</summary>
public sealed record EmailTemplateCreatedDomainEvent(Guid TemplateId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
