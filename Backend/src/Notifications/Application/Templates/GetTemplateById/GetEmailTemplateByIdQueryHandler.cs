using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Domain.Templates;

namespace NorthernLink.Notifications.Application.Templates.GetTemplateById;

/// <summary>Handles <see cref="GetEmailTemplateByIdQuery"/>.</summary>
public sealed class GetEmailTemplateByIdQueryHandler(IEmailTemplateReadService readService)
    : IQueryHandler<GetEmailTemplateByIdQuery, EmailTemplateResponse>
{
    public async Task<Result<EmailTemplateResponse>> Handle(
        GetEmailTemplateByIdQuery query,
        CancellationToken cancellationToken)
    {
        var template = await readService.GetTemplateAsync(query.TemplateId, cancellationToken);
        return template is null
            ? Result.Failure<EmailTemplateResponse>(EmailTemplateErrors.NotFound)
            : Result.Success(template);
    }
}
