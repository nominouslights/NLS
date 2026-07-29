using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Rendering;

namespace NorthernLink.Notifications.Application.Templates.Preview;

/// <summary>Handles <see cref="PreviewEmailTemplateQuery"/>.</summary>
public sealed class PreviewEmailTemplateQueryHandler
    : IQueryHandler<PreviewEmailTemplateQuery, EmailTemplatePreviewResponse>
{
    public Task<Result<EmailTemplatePreviewResponse>> Handle(
        PreviewEmailTemplateQuery query,
        CancellationToken cancellationToken)
    {
        var values = query.Values ?? MergeFieldRenderer.SampleValues;

        var subject = MergeFieldRenderer.RenderSubject(query.Subject, values);
        var htmlBody = MergeFieldRenderer.RenderHtml(query.HtmlBody, values);
        var textBody = MergeFieldRenderer.RenderText(htmlBody);

        return Task.FromResult(Result.Success(new EmailTemplatePreviewResponse(subject, htmlBody, textBody)));
    }
}
