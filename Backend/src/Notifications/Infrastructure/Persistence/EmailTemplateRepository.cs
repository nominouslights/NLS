using Microsoft.EntityFrameworkCore;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="NotificationsDbContext"/> (tenant-filtered).</summary>
internal sealed class EmailTemplateRepository(NotificationsDbContext context) : IEmailTemplateRepository
{
    public Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<EmailTemplate?> GetActiveByServiceTypeAsync(
        Guid tenantId,
        NotificationServiceType serviceType,
        CancellationToken cancellationToken = default) =>
        context.EmailTemplates
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId
                && t.ServiceType == serviceType
                && t.IsActive
                && t.ClientId == null)
            .OrderByDescending(t => t.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public void Add(EmailTemplate template) => context.EmailTemplates.Add(template);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
