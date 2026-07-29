using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>Write-side persistence for the EmailTemplate aggregate (tenant-scoped).</summary>
public interface IEmailTemplateRepository
{
    Task<EmailTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    void Add(EmailTemplate template);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
