using System.Text;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches.PreviewTripPickupReport;
using NorthernLink.Notifications.Application.Dispatches.SendTripPickupEmail;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Templates;
using NorthernLink.Notifications.Infrastructure.Reporting;
using QuestPDF.Infrastructure;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The preview handler's gates, recipient validation, report-recipient echo, and the guarantee
/// that a preview never sends or records anything. Gating is asserted for parity with
/// <see cref="SendTripPickupEmailCommandHandlerTests"/>.
/// </summary>
public class PreviewTripPickupReportQueryHandlerTests
{
    static PreviewTripPickupReportQueryHandlerTests()
    {
        // The real QuestPDF renderer relies on the process-wide license normally set in
        // AddNotifications; a raw unit test never runs DI, so set it before the first Build.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly InMemoryEmailTemplateRepository _templates = new();
    private readonly FakePickupEmailReportPdf _reportPdf = new();

    private PreviewTripPickupReportQueryHandler Handler() => new(_templates, _reportPdf);

    private static PreviewTripPickupReportQuery Query(
        Guid templateId,
        IReadOnlyList<RecipientInput>? recipients = null,
        Guid? clientId = null,
        NotificationServiceType serviceType = NotificationServiceType.ContractCrew,
        IReadOnlyList<string>? reportRecipients = null) => new(
        TestNotifications.TenantId,
        templateId,
        Guid.NewGuid(),
        "NL-1042",
        Guid.NewGuid(),
        serviceType,
        "Tuesday, August 4, 2026",
        "8:30 AM",
        "11:15 AM",
        "Thompson – Lynn Lake",
        clientId,
        "Marcel Colomb First Nation",
        recipients ?? [new RecipientInput(
            "alex@example.com", "Alex Moody",
            "Thompson Terminal", "12 Station Rd, Thompson, MB R8N 0A1",
            "Lynn Lake Co-op", "5 Co-op Lane, Lynn Lake, MB R0B 0W0")],
        reportRecipients ?? []);

    // ---- Happy path: composes a real report, sends/records nothing -------------------------

    [Fact]
    public async Task ContractCrew_preview_returns_a_real_pdf_and_a_non_empty_html_body_and_subject()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.ContractCrew);
        _templates.Templates.Add(template);

        // Use the real QuestPDF renderer so the Base64 decodes to genuine PDF magic bytes.
        var handler = new PreviewTripPickupReportQueryHandler(_templates, new QuestPickupEmailReportPdf());

        var result = await handler.Handle(Query(template.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var preview = result.Value;

        Assert.False(string.IsNullOrEmpty(preview.PdfBase64));
        var pdfBytes = Convert.FromBase64String(preview.PdfBase64);
        Assert.True(pdfBytes.Length >= 4);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdfBytes, 0, 4));

        Assert.False(string.IsNullOrEmpty(preview.HtmlBody));
        Assert.False(string.IsNullOrEmpty(preview.Subject));
    }

    [Fact]
    public void Handler_depends_on_neither_a_sender_nor_a_dispatch_repository()
    {
        // Structural proof the preview can never send or record: those collaborators are not even
        // reachable from the handler's constructor.
        var parameterTypes = typeof(PreviewTripPickupReportQueryHandler)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToList();

        Assert.DoesNotContain(typeof(IEmailSender), parameterTypes);
        Assert.DoesNotContain(typeof(IEmailDispatchRepository), parameterTypes);
    }

    // ---- Recipient count + report-recipient echo ------------------------------------------

    [Fact]
    public async Task RecipientCount_equals_the_number_of_valid_recipients()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.ContractCrew);
        _templates.Templates.Add(template);

        var recipients = new[]
        {
            new RecipientInput("alex@example.com", "Alex Moody", null, null, null, null),
            new RecipientInput("jordan@example.com", "Jordan Bell", null, null, null, null),
            new RecipientInput("sam@example.com", "Sam Rae", null, null, null, null),
        };

        var result = await Handler().Handle(Query(template.Id, recipients: recipients), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.RecipientCount);
    }

    [Fact]
    public async Task ReportRecipients_echoes_distinct_valid_addresses_and_drops_invalid_and_duplicates()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.ContractCrew);
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Query(
                template.Id,
                reportRecipients:
                [
                    "reports@crew.example.com",
                    "ops@crew.example.com",
                    "REPORTS@crew.example.com", // duplicate (case-insensitive) — dropped
                    "not-an-email",             // invalid — dropped
                    "  ops@crew.example.com  ", // trimmed duplicate — dropped
                ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "reports@crew.example.com", "ops@crew.example.com" },
            result.Value.ReportRecipients.ToArray());
    }

    // ---- Gating parity with the send ------------------------------------------------------

    [Fact]
    public async Task Unknown_template_returns_NotFound()
    {
        var result = await Handler().Handle(Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.NotFound, result.Error);
        Assert.Empty(_reportPdf.Built);
    }

    [Fact]
    public async Task Inactive_template_returns_Inactive()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.ContractCrew);
        template.Deactivate();
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Query(template.Id), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.Inactive, result.Error);
        Assert.Empty(_reportPdf.Built);
    }

    [Fact]
    public async Task Service_type_mismatch_returns_ServiceTypeMismatch()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.Charter);
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Query(template.Id, serviceType: NotificationServiceType.ContractCrew),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.ServiceTypeMismatch, result.Error);
        Assert.Empty(_reportPdf.Built);
    }

    [Fact]
    public async Task Client_pinned_template_rejects_a_different_trip_client_with_ClientMismatch()
    {
        var template = TestNotifications.CreateTemplate(
            serviceType: NotificationServiceType.ContractCrew,
            clientId: Guid.NewGuid(),
            clientName: "Marcel Colomb First Nation");
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Query(template.Id, clientId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailTemplateErrors.ClientMismatch, result.Error);
        Assert.Empty(_reportPdf.Built);
    }

    [Fact]
    public async Task Zero_recipients_returns_NoRecipients()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.ContractCrew);
        _templates.Templates.Add(template);

        var result = await Handler().Handle(Query(template.Id, recipients: []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, result.Error);
        Assert.Empty(_reportPdf.Built);
    }

    [Fact]
    public async Task More_than_the_max_recipients_returns_TooManyRecipients()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.ContractCrew);
        _templates.Templates.Add(template);

        var tooMany = Enumerable.Range(0, EmailDispatch.MaxRecipients + 1)
            .Select(index => new RecipientInput($"p{index}@example.com", $"Passenger {index}", null, null, null, null))
            .ToList();

        var result = await Handler().Handle(Query(template.Id, recipients: tooMany), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.TooManyRecipients, result.Error);
        Assert.Empty(_reportPdf.Built);
    }

    [Fact]
    public async Task Invalid_recipient_email_returns_InvalidRecipientEmail()
    {
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.ContractCrew);
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Query(template.Id, recipients: [new RecipientInput("867-5309", "Jenny", null, null, null, null)]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Dispatch.InvalidRecipientEmail", result.Error.Code);
        Assert.Empty(_reportPdf.Built);
    }

    // ---- Finding: the preview does NOT gate on ContractCrew; it composes for any service type ---

    [Fact]
    public async Task Preview_composes_for_a_non_crew_service_type_too()
    {
        // The report *send* is ContractCrew-only, but the preview handler has no service-category
        // gate — a matching active template of any service type composes a full preview.
        var template = TestNotifications.CreateTemplate(serviceType: NotificationServiceType.Community);
        _templates.Templates.Add(template);

        var result = await Handler().Handle(
            Query(template.Id, serviceType: NotificationServiceType.Community), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrEmpty(result.Value.Subject));
        Assert.False(string.IsNullOrEmpty(result.Value.HtmlBody));
        Assert.False(string.IsNullOrEmpty(result.Value.PdfBase64));
        Assert.Single(_reportPdf.Built); // a report was composed even though the trip is not crew
    }
}
