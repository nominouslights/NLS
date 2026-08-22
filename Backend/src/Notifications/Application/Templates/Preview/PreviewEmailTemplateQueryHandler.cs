using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Rendering;

namespace NorthernLink.Notifications.Application.Templates.Preview;

/// <summary>Handles <see cref="PreviewEmailTemplateQuery"/>.</summary>
public sealed class PreviewEmailTemplateQueryHandler(IEmailHtmlSanitizer htmlSanitizer)
    : IQueryHandler<PreviewEmailTemplateQuery, EmailTemplatePreviewResponse>
{
    public Task<Result<EmailTemplatePreviewResponse>> Handle(
        PreviewEmailTemplateQuery query,
        CancellationToken cancellationToken)
    {
        var values = query.Values ?? MergeFieldRenderer.SampleValues;

        // Sanitize the incoming body first, so the preview reflects exactly what will be
        // stored/sent (create/update sanitize the same way at save time).
        var subject = MergeFieldRenderer.RenderSubject(query.Subject, values);
        var htmlBody = MergeFieldRenderer.RenderHtml(htmlSanitizer.Sanitize(query.HtmlBody), values);
        var textBody = MergeFieldRenderer.RenderText(htmlBody);

        return Task.FromResult(Result.Success(new EmailTemplatePreviewResponse(subject, htmlBody, textBody)));
    }
}
