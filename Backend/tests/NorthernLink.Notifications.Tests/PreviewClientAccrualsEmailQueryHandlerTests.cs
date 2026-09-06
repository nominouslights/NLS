using NorthernLink.Notifications.Application.Dispatches.PreviewClientAccrualsEmail;
using NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The accruals preview handler: same gates as the send (shared recipient gate + client
/// snapshot check), same composition (shared composer), nothing sent — the handler takes no
/// <c>IEmailSender</c> at all, so sending is structurally impossible.
/// </summary>
public class PreviewClientAccrualsEmailQueryHandlerTests
{
    private static readonly Guid ClientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly FakeClientAccrualsReportPdf _reportPdf = new();

    private PreviewClientAccrualsEmailQueryHandler Handler() => new(_reportPdf);

    private static PreviewClientAccrualsEmailQuery Query(
        Guid? clientId = null,
        string clientName = "Vale Manitoba Operations",
        IReadOnlyList<AccrualsRecipientInput>? recipients = null) => new(
        TestNotifications.TenantId,
        clientId ?? ClientId,
        clientName,
        NotificationServiceType.ContractCrew,
        TestNotifications.SampleAccrualsReport(),
        recipients ?? [new AccrualsRecipientInput("dana@example.com", "Dana Reyes")]);

    [Fact]
    public async Task Missing_client_id_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(Query(clientId: Guid.Empty), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.ClientRequired, result.Error);
    }

    [Fact]
    public async Task Empty_recipient_list_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(Query(recipients: []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, result.Error);
    }

    [Fact]
    public async Task Invalid_recipient_email_fails_exactly_like_the_send()
    {
        var result = await Handler().Handle(
            Query(recipients: [new AccrualsRecipientInput("867-5309", "Jenny")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Dispatch.InvalidRecipientEmail", result.Error.Code);
    }

    [Fact]
    public async Task More_than_the_distinct_recipient_cap_fails_exactly_like_the_send()
    {
        var recipients = Enumerable.Range(0, EmailDispatch.MaxRecipients + 1)
            .Select(i => new AccrualsRecipientInput($"contact{i}@example.com", $"Contact {i}"))
            .ToList();

        var result = await Handler().Handle(Query(recipients: recipients), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.TooManyRecipients, result.Error);
    }

    [Fact]
    public async Task Preview_composes_subject_bodies_and_the_pdf_without_sending()
    {
        var result = await Handler().Handle(Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal("Accruals report — Vale Manitoba Operations — August 2026", response.Subject);
        Assert.Contains("accruals report for Vale Manitoba Operations", response.HtmlBody);
        Assert.DoesNotContain("<p>", response.TextBody);
        Assert.Equal(Convert.ToBase64String(new byte[] { 1, 2, 3 }), response.PdfBase64);
        Assert.Single(_reportPdf.Built);
    }

    [Fact]
    public async Task Recipients_are_deduplicated_and_echoed_for_display()
    {
        var result = await Handler().Handle(
            Query(recipients:
            [
                new AccrualsRecipientInput("dana@example.com", "Dana Reyes"),
                new AccrualsRecipientInput("DANA@example.com", "Dana R."),
                new AccrualsRecipientInput("sam@example.com", "Sam Okoye"),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.RecipientCount);
        Assert.Equal(new[] { "dana@example.com", "sam@example.com" }, result.Value.Recipients.ToArray());
    }
}
