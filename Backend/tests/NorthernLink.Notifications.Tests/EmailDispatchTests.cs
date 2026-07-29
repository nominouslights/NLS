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
            [Recipient("a@example.com", DispatchRecipientStatus.Sent)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(dispatchId, result.Value.Id);
        var recorded = Assert.IsType<EmailDispatchRecordedDomainEvent>(Assert.Single(result.Value.DomainEvents));
        Assert.Equal(dispatchId, recorded.DispatchId);
        Assert.Equal(TestNotifications.TenantId, recorded.TenantId);
    }
}
