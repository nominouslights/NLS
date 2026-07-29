using Microsoft.EntityFrameworkCore;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Templates;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Infrastructure.Persistence.ReadModels;

namespace NorthernLink.Notifications.Infrastructure.Persistence;

/// <summary>Read side — queries notifications.rm_email_templates and maps to the public contract.</summary>
internal sealed class EmailTemplateReadService(NotificationsDbContext context) : IEmailTemplateReadService
{
    public async Task<IReadOnlyList<EmailTemplateResponse>> GetTemplatesAsync(
        NotificationServiceType? serviceType,
        Guid? clientId,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = context.EmailTemplateReadModels.AsNoTracking();

        if (serviceType is { } type)
        {
            var typeName = type.ToString();
            query = query.Where(t => t.ServiceType == typeName);
        }

        if (clientId is { } client)
        {
            query = query.Where(t => t.ClientId == client);
        }

        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }

        var templates = await query.OrderBy(t => t.Name).ToListAsync(cancellationToken);
        return templates.Select(ToResponse).ToList();
    }

    public async Task<EmailTemplateResponse?> GetTemplateAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await context.EmailTemplateReadModels
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        return template is null ? null : ToResponse(template);
    }

    private static EmailTemplateResponse ToResponse(EmailTemplateReadModel template) => new(
        template.Id,
        template.Name,
        template.ServiceType,
        template.ClientId,
        template.ClientName,
        template.Subject,
        template.HtmlBody,
        template.IsActive,
        template.CreatedAtUtc,
        template.UpdatedAtUtc);
}
