using NorthernLink.Notifications.Application.Templates;
using NorthernLink.Notifications.Domain;

namespace NorthernLink.Notifications.Application.Abstractions;

/// <summary>Read side for template queries — returns response DTOs directly (tenant-scoped).</summary>
public interface IEmailTemplateReadService
{
    /// <summary>
    /// Lists templates, optionally filtered by service type and/or client. Inactive
    /// templates are excluded unless <paramref name="includeInactive"/> is set.
    /// </summary>
    Task<IReadOnlyList<EmailTemplateResponse>> GetTemplatesAsync(
        NotificationServiceType? serviceType,
        Guid? clientId,
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<EmailTemplateResponse?> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default);
}
