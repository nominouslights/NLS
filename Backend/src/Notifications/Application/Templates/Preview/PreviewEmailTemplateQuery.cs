using NorthernLink.Shared.Messaging;

namespace NorthernLink.Notifications.Application.Templates.Preview;

/// <summary>
/// Renders arbitrary (possibly unsaved) template content through the exact renderer the
/// send path uses. Null <paramref name="Values"/> renders with server sample data.
/// </summary>
public sealed record PreviewEmailTemplateQuery(
    Guid TenantId,
    string Subject,
    string HtmlBody,
    IReadOnlyDictionary<string, string>? Values) : IQuery<EmailTemplatePreviewResponse>;

/// <summary>The rendered preview: subject, HTML body, and the derived plain-text fallback.</summary>
public sealed record EmailTemplatePreviewResponse(string Subject, string HtmlBody, string TextBody);
