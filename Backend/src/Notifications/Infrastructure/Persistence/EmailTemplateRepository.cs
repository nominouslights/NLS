using Microsoft.EntityFrameworkCore;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>Write-side repository over <see cref="NotificationsDbContext"/> (tenant-filtered).</summary>
internal sealed class EmailTemplateRepository(NotificationsDbContext context) : IEmailTemplateRepository
{
    public Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.EmailTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void Add(EmailTemplate template) => context.EmailTemplates.Add(template);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
