using NorthernLink.Notifications.Application.Templates.Create;
using NorthernLink.Notifications.Application.Templates.Preview;
using NorthernLink.Notifications.Application.Templates.Update;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Infrastructure.Rendering;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The create/update/preview handlers sanitize the template body through
/// <see cref="GanssEmailHtmlSanitizer"/> before it is stored / rendered, while merge tokens
/// and safe formatting survive.
/// </summary>
public class EmailTemplateSanitizationHandlerTests
{
    private const string MaliciousBody =
        "<p style=\"color:red\">Hi {{PassengerName}},</p><script>alert(1)</script>" +
        "<a href=\"javascript:alert(1)\">x</a>";

    private readonly InMemoryEmailTemplateRepository _templates = new();
    private readonly GanssEmailHtmlSanitizer _sanitizer = new();

    [Fact]
    public async Task Create_stores_the_sanitized_body_with_tokens_preserved()
    {
        var handler = new CreateEmailTemplateCommandHandler(_templates, _sanitizer);
        var command = new CreateEmailTemplateCommand(
            TestNotifications.TenantId,
            "Community pickup",
            NotificationServiceType.Community,
            null,
            null,
            "Pickup {{TripDate}}",
            MaliciousBody);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stored = Assert.Single(_templates.Templates);
        Assert.DoesNotContain("<script", stored.HtmlBody, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", stored.HtmlBody, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{{PassengerName}}", stored.HtmlBody);
        Assert.Contains("color", stored.HtmlBody); // inline style survives
    }

    [Fact]
    public async Task Update_stores_the_sanitized_body_with_tokens_preserved()
    {
        var template = TestNotifications.CreateTemplate();
        _templates.Templates.Add(template);
        var handler = new UpdateEmailTemplateCommandHandler(_templates, _sanitizer);
        var command = new UpdateEmailTemplateCommand(
            TestNotifications.TenantId,
            template.Id,
            "Community pickup",
            NotificationServiceType.Community,
            null,
            null,
            "Pickup {{TripDate}}",
            MaliciousBody);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("<script", template.HtmlBody, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", template.HtmlBody, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{{PassengerName}}", template.HtmlBody);
    }

    [Fact]
    public async Task Preview_returns_sanitized_html()
    {
        var handler = new PreviewEmailTemplateQueryHandler(_sanitizer);
        var query = new PreviewEmailTemplateQuery(
            TestNotifications.TenantId,
            "Pickup {{TripDate}}",
            MaliciousBody,
            Values: null);

        var result = await handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain("<script", result.Value.HtmlBody, System.StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", result.Value.HtmlBody, System.StringComparison.OrdinalIgnoreCase);
        // the sample PassengerName was substituted into the (already-sanitized) body
        Assert.Contains("Alex Moody", result.Value.HtmlBody);
    }
}
