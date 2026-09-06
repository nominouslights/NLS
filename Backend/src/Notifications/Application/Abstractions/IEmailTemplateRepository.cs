using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>Write-side persistence for the EmailTemplate aggregate (tenant-scoped).</summary>
public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The newest active, client-unpinned template of a service type — how event-driven
    /// sends (which have no dispatcher picking a template) resolve their override. Null
    /// when none exists; callers fall back to their built-in body. Takes the tenant
    /// explicitly and bypasses the query filter (the consumer-path shape — the DbContext
    /// may have been constructed before the handler pushed the event's tenant).
    /// </summary>
    Task<EmailTemplate?> GetActiveByServiceTypeAsync(
        Guid tenantId,
        NotificationServiceType serviceType,
        CancellationToken cancellationToken = default);

    void Add(EmailTemplate template);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
