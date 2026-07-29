using NorthernLink.Notifications.Infrastructure.Email;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>The pure outcome mapping from Postmark's batch response (no HTTP involved).</summary>
public class PostmarkEmailSenderTests
{
    [Fact]
    public void ErrorCode_zero_maps_to_sent_with_message_id()
    {
        var outcomes = PostmarkEmailSender.MapOutcomes(1,
        [
            new PostmarkEmailSender.PostmarkMessageResult(0, "OK", "msg-123", "a@example.com"),
        ]);

        var outcome = Assert.Single(outcomes);
        Assert.True(outcome.Sent);
        Assert.Null(outcome.ErrorCode);
        Assert.Equal("msg-123", outcome.MessageId);
    }

    [Fact]
    public void Nonzero_ErrorCode_maps_to_failed_with_postmark_code()
    {
        var outcomes = PostmarkEmailSender.MapOutcomes(1,
        [
            new PostmarkEmailSender.PostmarkMessageResult(300, "Invalid 'To' address.", null, "bad@"),
        ]);

        var outcome = Assert.Single(outcomes);
        Assert.False(outcome.Sent);
        Assert.Equal("Postmark.300", outcome.ErrorCode);
        Assert.Equal("Invalid 'To' address.", outcome.ErrorMessage);
        Assert.Null(outcome.MessageId);
    }

    [Fact]
    public void Outcomes_align_with_submission_order()
    {
        var outcomes = PostmarkEmailSender.MapOutcomes(3,
        [
            new PostmarkEmailSender.PostmarkMessageResult(0, null, "msg-1", "a@example.com"),
            new PostmarkEmailSender.PostmarkMessageResult(406, "Inactive recipient.", null, "b@example.com"),
            new PostmarkEmailSender.PostmarkMessageResult(0, null, "msg-3", "c@example.com"),
        ]);

        Assert.Equal(3, outcomes.Count);
        Assert.True(outcomes[0].Sent);
        Assert.Equal("msg-1", outcomes[0].MessageId);
        Assert.False(outcomes[1].Sent);
        Assert.Equal("Postmark.406", outcomes[1].ErrorCode);
        Assert.True(outcomes[2].Sent);
        Assert.Equal("msg-3", outcomes[2].MessageId);
    }

    [Fact]
    public void A_short_result_list_pads_with_missing_result_failures()
    {
        var outcomes = PostmarkEmailSender.MapOutcomes(2,
        [
            new PostmarkEmailSender.PostmarkMessageResult(0, null, "msg-1", "a@example.com"),
        ]);

        Assert.Equal(2, outcomes.Count);
        Assert.True(outcomes[0].Sent);
        Assert.False(outcomes[1].Sent);
        Assert.Equal("Postmark.MissingResult", outcomes[1].ErrorCode);
    }
}
