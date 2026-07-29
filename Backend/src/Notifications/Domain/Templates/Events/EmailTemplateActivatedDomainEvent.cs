using NorthernLink.Shared.Kernel;

namespace NorthernLink.Notifications.Domain.Templates.Events;

/// <summary>Raised when an email template is (re)activated.</summary>
public sealed record EmailTemplateActivatedDomainEvent(Guid TemplateId, Guid TenantId) : IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
