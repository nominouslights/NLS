using NorthernLink.Shared.Kernel;
using NorthernLink.Notifications.Domain;
using NorthernLink.Notifications.Domain.Dispatches;
using NorthernLink.Notifications.Domain.Dispatches.Events;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>EmailDispatch status derivation and recording behavior.</summary>
public class EmailDispatchTests
{
    private static DispatchRecipient Recipient(string email, DispatchRecipientStatus status) => new()
    {
        Email = email,
        PassengerName = "Passenger",
        Status = status,
        ErrorCode = status == DispatchRecipientStatus.Failed ? "Postmark.300" : null,
        ErrorMessage = status == DispatchRecipientStatus.Failed ? "Invalid email" : null,
        PostmarkMessageId = status == DispatchRecipientStatus.Sent ? Guid.NewGuid().ToString() : null,
    };

    private static Result<EmailDispatch> Record(params DispatchRecipient[] recipients) =>
        EmailDispatch.Record(
            Guid.NewGuid(),
            TestNotifications.TenantId,
            Guid.NewGuid(),
            "NL-1042",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Community pickup",
            NotificationServiceType.Community,
            Guid.NewGuid(),
            "Marcel Colomb First Nation",
            recipients);

    [Fact]
    public void All_recipients_sent_derives_Sent()
    {
        var result = Record(
            Recipient("a@example.com", DispatchRecipientStatus.Sent),
            Recipient("b@example.com", DispatchRecipientStatus.Sent));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailDispatchStatus.Sent, result.Value.Status);
    }

    [Fact]
    public void Mixed_outcomes_derive_PartiallyFailed()
    {
        var result = Record(
            Recipient("a@example.com", DispatchRecipientStatus.Sent),
            Recipient("b@example.com", DispatchRecipientStatus.Failed));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailDispatchStatus.PartiallyFailed, result.Value.Status);
    }

    [Fact]
    public void No_recipient_sent_derives_Failed()
    {
        var result = Record(
            Recipient("a@example.com", DispatchRecipientStatus.Failed),
            Recipient("b@example.com", DispatchRecipientStatus.Failed));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailDispatchStatus.Failed, result.Value.Status);
    }

    [Fact]
    public void Empty_recipients_are_rejected()
    {
        var result = Record();

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, result.Error);
    }

    [Fact]
    public void More_than_the_recipient_cap_is_rejected()
    {
        var recipients = Enumerable.Range(0, EmailDispatch.MaxRecipients + 1)
            .Select(i => Recipient($"p{i}@example.com", DispatchRecipientStatus.Sent))
            .ToArray();

        var result = Record(recipients);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.TooManyRecipients, result.Error);
    }

    [Fact]
    public void Record_uses_the_client_dispatch_id_and_raises_recorded_event()
    {
        var dispatchId = Guid.NewGuid();
        var result = EmailDispatch.Record(
            dispatchId,
            TestNotifications.TenantId,
            Guid.NewGuid(),
            "NL-1042",
            null,
            Guid.NewGuid(),
            "Community pickup",
            NotificationServiceType.Community,
            null,
            null,
            [Recipient("a@example.com", DispatchRecipientStatus.Sent)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(dispatchId, result.Value.Id);
        var recorded = Assert.IsType<EmailDispatchRecordedDomainEvent>(Assert.Single(result.Value.DomainEvents));
        Assert.Equal(dispatchId, recorded.DispatchId);
        Assert.Equal(TestNotifications.TenantId, recorded.TenantId);
    }

    // ---- Client accruals recording ----------------------------------------------------------

    private static Result<EmailDispatch> RecordAccruals(params DispatchRecipient[] recipients) =>
        EmailDispatch.RecordClientAccruals(
            Guid.NewGuid(),
            TestNotifications.TenantId,
            Guid.NewGuid(),
            "Vale Manitoba Operations",
            NotificationServiceType.ContractCrew,
            recipients);

    [Fact]
    public void RecordClientAccruals_has_no_trip_and_no_template_but_a_required_client()
    {
        var dispatchId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var result = EmailDispatch.RecordClientAccruals(
            dispatchId,
            TestNotifications.TenantId,
            clientId,
            "  Vale Manitoba Operations  ",
            NotificationServiceType.ContractCrew,
            [Recipient("a@example.com", DispatchRecipientStatus.Sent)]);

        Assert.True(result.IsSuccess);
        var dispatch = result.Value;
        Assert.Equal(dispatchId, dispatch.Id);
        Assert.Null(dispatch.TripId);
        Assert.Null(dispatch.TripNumber);
        Assert.Null(dispatch.ManifestId);
        Assert.Null(dispatch.TemplateId);
        Assert.Null(dispatch.TemplateName);
        Assert.Equal(clientId, dispatch.ClientId);
        Assert.Equal("Vale Manitoba Operations", dispatch.ClientName); // trimmed
        var recorded = Assert.IsType<EmailDispatchRecordedDomainEvent>(Assert.Single(dispatch.DomainEvents));
        Assert.Equal(dispatchId, recorded.DispatchId);
    }

    [Fact]
    public void RecordClientAccruals_rejects_an_empty_client_id()
    {
        var result = EmailDispatch.RecordClientAccruals(
            Guid.NewGuid(),
            TestNotifications.TenantId,
            Guid.Empty,
            "Vale Manitoba Operations",
            NotificationServiceType.ContractCrew,
            [Recipient("a@example.com", DispatchRecipientStatus.Sent)]);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.ClientRequired, result.Error);
    }

    [Fact]
    public void RecordClientAccruals_rejects_a_blank_client_name()
    {
        var result = EmailDispatch.RecordClientAccruals(
            Guid.NewGuid(),
            TestNotifications.TenantId,
            Guid.NewGuid(),
            "  ",
            NotificationServiceType.ContractCrew,
            [Recipient("a@example.com", DispatchRecipientStatus.Sent)]);

        Assert.True(result.IsFailure);
        Assert.Equal(EmailDispatchErrors.ClientRequired, result.Error);
    }

    [Fact]
    public void RecordClientAccruals_enforces_the_same_recipient_bounds()
    {
        var none = RecordAccruals();
        Assert.True(none.IsFailure);
        Assert.Equal(EmailDispatchErrors.NoRecipients, none.Error);

        var tooMany = RecordAccruals(Enumerable.Range(0, EmailDispatch.MaxRecipients + 1)
            .Select(i => Recipient($"p{i}@example.com", DispatchRecipientStatus.Sent))
            .ToArray());
        Assert.True(tooMany.IsFailure);
        Assert.Equal(EmailDispatchErrors.TooManyRecipients, tooMany.Error);
    }

    [Fact]
    public void RecordClientAccruals_derives_status_from_recipient_outcomes()
    {
        var result = RecordAccruals(
            Recipient("a@example.com", DispatchRecipientStatus.Sent),
            Recipient("b@example.com", DispatchRecipientStatus.Failed));

        Assert.True(result.IsSuccess);
        Assert.Equal(EmailDispatchStatus.PartiallyFailed, result.Value.Status);
    }

    [Theory]
    [InlineData("dddddddd-dddd-dddd-dddd-dddddddddddd")]
    [InlineData(null)]
    public void Record_snapshots_the_client_id(string? clientId)
    {
        var expected = clientId is null ? (Guid?)null : Guid.Parse(clientId);

        var result = EmailDispatch.Record(
            Guid.NewGuid(),
            TestNotifications.TenantId,
            Guid.NewGuid(),
            "NL-1042",
            null,
            Guid.NewGuid(),
            "Community pickup",
            NotificationServiceType.Community,
            expected,
            expected is null ? null : "Marcel Colomb First Nation",
            [Recipient("a@example.com", DispatchRecipientStatus.Sent)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value.ClientId);
    }
}
