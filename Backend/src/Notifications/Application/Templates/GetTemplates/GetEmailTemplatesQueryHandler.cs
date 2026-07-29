using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Application.Templates.GetTemplates;

/// <summary>Handles <see cref="GetEmailTemplatesQuery"/>.</summary>
public sealed class GetEmailTemplatesQueryHandler(IEmailTemplateReadService readService)
    : IQueryHandler<GetEmailTemplatesQuery, IReadOnlyList<EmailTemplateResponse>>
{
    public async Task<Result<IReadOnlyList<EmailTemplateResponse>>> Handle(
        GetEmailTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var templates = await readService.GetTemplatesAsync(
            query.ServiceType, query.ClientId, query.IncludeInactive, cancellationToken);
        return Result.Success(templates);
    }
}
