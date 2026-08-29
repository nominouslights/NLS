using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Application.Dispatches;
using NorthernLink.Notifications.Application.Dispatches.SendClientAccrualsEmail;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>The accruals send handler's gates, composition, idempotent replay, and persistence.</summary>
public class SendClientAccrualsEmailCommandHandlerTests
{
    private static readonly Guid ClientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

    private readonly InMemoryEmailDispatchRepository _dispatches = new();
    private readonly FakeEmailSender _sender = new();
    private readonly FakeClientAccrualsReportPdf _reportPdf = new();

    private SendClientAccrualsEmailCommandHandler Handler() => new(_dispatches, _sender, _reportPdf);

    private static SendClientAccrualsEmailCommand Command(
        Guid? dispatchId = null,
        Guid? clientId = null,
        string clientName = "Vale Manitoba Operations",
        IReadOnlyList<AccrualsRecipientInput>? recipients = null) => new(
        TestNotifications.TenantId,
        dispatchId ?? Guid.NewGuid(),
        clientId ?? ClientId,
        clientName,
        NotificationServiceType.ContractCrew,
        TestNotifications.SampleAccrualsReport(),
        recipients ?? [new AccrualsRecipientInput("dana@example.com", "Dana Reyes")]);

    [Fact]
    public async Task Missing_client_id_is_rejected_and_nothing_is_sent()
    {
        var result = await Handler().Handle(Command(clientId: Guid.Empty), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.ClientRequired, result.Error);
        Assert.Empty(_sender.Batches);
        Assert.Empty(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Missing_client_name_is_rejected_and_nothing_is_sent()
    {
        var result = await Handler().Handle(Command(clientName: "  "), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.ClientRequired, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Empty_recipient_list_is_rejected()
    {
        var result = await Handler().Handle(Command(recipients: []), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Invalid_recipient_email_is_rejected_before_sending()
    {
        var result = await Handler().Handle(
            Command(recipients: [new AccrualsRecipientInput("867-5309", "Jenny")]),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Notifications.Dispatch.InvalidRecipientEmail", result.Error.Code);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task More_than_the_distinct_recipient_cap_is_rejected()
    {
        var recipients = Enumerable.Range(0, EmailDispatch.MaxRecipients + 1)
            .Select(i => new AccrualsRecipientInput($"contact{i}@example.com", $"Contact {i}"))
            .ToList();

        var result = await Handler().Handle(Command(recipients: recipients), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.TooManyRecipients, result.Error);
        Assert.Empty(_sender.Batches);
    }

    [Fact]
    public async Task Duplicate_addresses_are_sent_and_recorded_once()
    {
        var result = await Handler().Handle(
            Command(recipients:
            [
                new AccrualsRecipientInput("dana@example.com", "Dana Reyes"),
                new AccrualsRecipientInput("DANA@example.com", "Dana R."),
                new AccrualsRecipientInput("  dana@example.com  ", "Dana"),
            ]),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var email = Assert.Single(Assert.Single(_sender.Batches));
        Assert.Equal("dana@example.com", email.To);
        var recipient = Assert.Single(result.Value.Recipients);
        Assert.Equal("Dana Reyes", recipient.PassengerName); // first occurrence's name wins
    }

    [Fact]
    public async Task Replayed_DispatchId_returns_the_stored_dispatch_without_resending()
    {
        var dispatchId = Guid.NewGuid();

        var first = await Handler().Handle(Command(dispatchId), CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Single(_sender.Batches);

        var replay = await Handler().Handle(Command(dispatchId), CancellationToken.None);

        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.Id, replay.Value.Id);
        Assert.Equal(first.Value.SentAtUtc, replay.Value.SentAtUtc);
        Assert.Single(_sender.Batches); // no second provider call
        Assert.Single(_dispatches.Dispatches);
    }

    [Fact]
    public async Task Happy_path_attaches_the_pdf_persists_and_returns_outcomes()
    {
        var command = Command(recipients:
        [
            new AccrualsRecipientInput("dana@example.com", "Dana Reyes"),
            new AccrualsRecipientInput("sam@example.com", "Sam Okoye"),
        ]);

        var result = await Handler().Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var response = result.Value;
        Assert.Equal(command.DispatchId, response.Id);
        Assert.Equal("Sent", response.Status);
        Assert.Equal(2, response.Recipients.Count);

        // One batch, one PDF build, the same attachment on every recipient's email.
        var builtReport = Assert.Single(_reportPdf.Built);
        Assert.Equal("Vale Manitoba Operations", builtReport.ClientName);
        var batch = Assert.Single(_sender.Batches);
        Assert.Equal(2, batch.Count);
        foreach (var email in batch)
        {
            Assert.Equal("Accruals report — Vale Manitoba Operations — August 2026", email.Subject);
            Assert.Contains("accruals report for Vale Manitoba Operations", email.HtmlBody);
            var attachment = Assert.Single(email.Attachments!);
            Assert.Equal("accruals-report-august-2026.pdf", attachment.Name);
            Assert.Equal("application/pdf", attachment.ContentType);
            Assert.False(string.IsNullOrEmpty(attachment.Base64Content));
        }

        // Recorded as a client-anchored dispatch: no trip, no template.
        Assert.Equal(1, _dispatches.SaveChangesCallCount);
        var stored = Assert.Single(_dispatches.Dispatches);
        Assert.Equal(ClientId, stored.ClientId);
        Assert.Equal("Vale Manitoba Operations", stored.ClientName);
        Assert.Null(stored.TripId);
        Assert.Null(stored.TripNumber);
        Assert.Null(stored.TemplateId);
        Assert.Null(stored.TemplateName);
        Assert.Equal(EmailDispatchStatus.Sent, stored.Status);
    }

    [Fact]
    public async Task Total_provider_failure_still_persists_and_returns_success_with_failed_outcomes()
    {
        _sender.OutcomeFor = (_, _) => new(false, "Postmark.Unauthorized", "Bad token.", null);

        var result = await Handler().Handle(Command(), CancellationToken.None);

        Assert.True(result.IsSuccess); // outcomes are data, not an error path
        Assert.Equal("Failed", result.Value.Status);
        var recipient = Assert.Single(result.Value.Recipients);
        Assert.Equal("Postmark.Unauthorized", recipient.ErrorCode);
        Assert.Single(_dispatches.Dispatches);
    }
}

/// <summary>
/// Test double for <see cref="IClientAccrualsReportPdf"/> — records the reports it was asked
/// to render and returns fixed bytes, so tests never invoke QuestPDF (or need its license).
/// </summary>
internal sealed class FakeClientAccrualsReportPdf : IClientAccrualsReportPdf
{
    public List<ClientAccrualsReport> Built { get; } = [];

    public byte[] Build(ClientAccrualsReport report)
    {
        Built.Add(report);
        return [1, 2, 3];
    }
}
