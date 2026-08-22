using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Templates;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>The send handler's gates, rendering, idempotent replay, and persistence.</summary>
public class SendTripPickupEmailCommandHandlerTests
{
    private readonly InMemoryEmailTemplateRepository _templates = new();
    private readonly InMemoryEmailDispatchRepository _dispatches = new();
    private readonly FakeEmailSender _sender = new();

    private SendTripPickupEmailCommandHandler Handler() => new(_templates, _dispatches, _sender);

    private static SendTripPickupEmailCommand Command(
        Guid templateId,
        Guid? dispatchId = null,
        IReadOnlyList<RecipientInput>? recipients = null,
        Guid? clientId = null) => new(
        TestNotifications.TenantId,
        dispatchId ?? Guid.NewGuid(),
        templateId,
        Guid.NewGuid(),
        "NL-1042",
        Guid.NewGuid(),
        NotificationServiceType.Community,
        "Tuesday, August 4, 2026",
        "8:30 AM",
        "Thompson – Lynn Lake",
        clientId,
        "Marcel Colomb First Nation",
        recipients ?? [new RecipientInput("alex@example.com", "Alex Moody", "Thompson Terminal", "Lynn Lake Co-op")]);

    [Fact]
    public async Task Unknown_template_returns_NotFound()
    {
        var result = await Handler().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.NotFound, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Inactive_template_is_a_validation_failure_and_nothing_is_sent()
    {
        var template = TestNotifications.CreateTemplate();
        template.Deactivate();
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Command(template.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.Inactive, result.Error);
        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Template_for_a_different_service_type_is_rejected_and_nothing_is_sent()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.Charter);
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Command(template.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.ServiceTypeMismatch, result.Error);
        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Client_pinned_template_rejects_a_different_trip_client_and_nothing_is_sent()
    {
        var template = TestNotifications.CreateTemplate(
            clientId: Guid.NewGuid(), clientName: "Marcel Colomb First Nation");
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Command(template.Id, clientId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.ClientMismatch, result.Error);
        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Client_pinned_template_rejects_a_client_less_trip()
    {
        var template = TestNotifications.CreateTemplate(
            clientId: Guid.NewGuid(), clientName: "Marcel Colomb First Nation");
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Command(template.Id, clientId: null), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.ClientMismatch, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Client_pinned_template_sends_for_the_matching_client()
    {
        var clientId = Guid.NewGuid();
        var template = TestNotifications.CreateTemplate(
            clientId: clientId, clientName: "Marcel Colomb First Nation");
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Command(template.Id, clientId: clientId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_sender.Batches);
    }

    [Fact]
    public async Task Service_wide_template_sends_for_any_trip_client()
    {
        var template = TestNotifications.CreateTemplate(); // no client pin
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Command(template.Id, clientId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(_sender.Batches);
    }

    [Fact]
    public async Task Dispatch_persists_the_trip_client_id()
    {
        var clientId = Guid.NewGuid();
        var template = TestNotifications.CreateTemplate(
            clientId: clientId, clientName: "Marcel Colomb First Nation");
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Command(template.Id, clientId: clientId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(clientId, result.Value.ClientId);
        var stored = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(clientId, stored.ClientId);
    }

    [Fact]
    public async Task Empty_recipient_list_is_rejected()
    {
        var template = TestNotifications.CreateTemplate();
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Command(template.Id, recipients: []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Invalid_recipient_email_is_rejected_before_sending()
    {
        var template = TestNotifications.CreateTemplate();
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Command(template.Id, recipients: [new RecipientInput("867-5309", "Jenny", null, null)]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Dispatch.InvalidRecipientEmail", result.Error.Code);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Replayed_DispatchId_returns_the_stored_dispatch_without_resending()
    {
        var template = TestNotifications.CreateTemplate();
        _templates.Templates.Add(template);
        var dispatchId = Guid.NewGuid();

        var first = await Handler().Handle(Command(template.Id, dispatchId), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Single(_sender.Batches);

        var replay = await Handler().Handle(Command(template.Id, dispatchId), CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.Id, replay.Value.Id);
        Assert.Equal(first.Value.SentAtUtc, replay.Value.SentAtUtc);
        Assert.Single(_sender.Batches); // no second provider call
        Assert.Single(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Happy_path_renders_merge_values_persists_and_returns_outcomes()
    {
        var template = TestNotifications.CreateTemplate();
        _templates.Templates.Add(template);
        var command = Command(template.Id);

        var result = await Handler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal(command.DispatchId, response.Id);
        Assert.Equal("Sent", response.Status);
        var recipient = Assert.Single(response.Recipients);
        Assert.Equal("alex@example.com", recipient.Email);
        Assert.Equal("Sent", recipient.Status);
        Assert.Equal("msg-0", recipient.PostmarkMessageId);

        var email = Assert.Single(Assert.Single(_sender.Batches));
        Assert.Equal("Pickup Tuesday, August 4, 2026 — NL-1042", email.Subject);
        Assert.Contains("Hi Alex Moody,", email.HtmlBody);
        Assert.Contains("Pickup at Thompson Terminal at 8:30 AM.", email.HtmlBody);
        Assert.DoesNotContain("{{", email.HtmlBody);
        Assert.Contains("Hi Alex Moody,", email.TextBody);
        Assert.DoesNotContain("<p>", email.TextBody);

        Assert.Equal(1, _dispatches.SaveChangesCallCount);
        var stored = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(EmailDispatchStatus.Sent, stored.Status);
        Assert.Equal(template.Id, stored.TemplateId);
        Assert.Equal(template.Name, stored.TemplateName);
    }

    [Fact]
    public async Task Total_provider_failure_still_persists_and_returns_success_with_failed_outcomes()
    {
        var template = TestNotifications.CreateTemplate();
        _templates.Templates.Add(template);
        _sender.OutcomeFor = (_, _) => new(false, "Postmark.Unauthorized", "Bad token.", null);

        var result = await Handler().Handle(Command(template.Id), CancellationToken.None);

        Assert.True(result.IsSuccess); // outcomes are data, not an error path
        Assert.Equal("Failed", result.Value.Status);
        var recipient = Assert.Single(result.Value.Recipients);
        Assert.Equal("Postmark.Unauthorized", recipient.ErrorCode);
        Assert.Single(_dispatches.Dispatches);
    }
}
