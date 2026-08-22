using System.Text;
using System.Text.Json;
using NorthernLink.Notifications.Application.Abstractions;
using NorthernLink.Notifications.Infrastructure.Email;
using Xunit;

namespace NorthernLink.Notifications.Tests;

/// <summary>
/// The Postmark request payload for an <see cref="OutgoingEmail"/>: an attachment serializes into
/// an <c>Attachments</c> array (Name/Content/ContentType), and the property is omitted entirely
/// when there are none — so the attachment-free pickup batch stays byte-for-byte as before.
/// Exercises the real sender (and its private message/serializer types) through a capturing
/// HTTP handler rather than reaching into internals.
/// </summary>
public class PostmarkEmailSenderSerializationTests
{
    private static (PostmarkEmailSender Sender, CapturingHandler Handler) BuildSender()
    {
        Environment.SetEnvironmentVariable(PostmarkEmailSender.ApiKeyVariable, "test-token");
        var handler = new CapturingHandler();
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.postmarkapp.com") };
        return (new PostmarkEmailSender(client, new NotificationsOptions()), handler);
    }

    [Fact]
    public async Task An_email_with_an_attachment_serializes_an_Attachments_array()
    {
        var (sender, handler) = BuildSender();
        var email = new OutgoingEmail(
            "crew@example.com", "Report", "<p>See attached.</p>", "See attached.",
            [new EmailAttachment("pickup-emails-NL-1042.pdf", "AQID", "application/pdf")]);

        await sender.SendBatchAsync([email], CancellationToken.None);

        using var document = JsonDocument.Parse(handler.CapturedBody!);
        var message = document.RootElement[0];

        Assert.True(message.TryGetProperty("Attachments", out var attachments));
        Assert.Equal(JsonValueKind.Array, attachments.ValueKind);
        var attachment = Assert.Single(attachments.EnumerateArray().ToList());
        Assert.Equal("pickup-emails-NL-1042.pdf", attachment.GetProperty("Name").GetString());
        Assert.Equal("AQID", attachment.GetProperty("Content").GetString());
        Assert.Equal("application/pdf", attachment.GetProperty("ContentType").GetString());
    }

    [Fact]
    public async Task An_email_without_attachments_omits_the_Attachments_property_entirely()
    {
        var (sender, handler) = BuildSender();
        var email = new OutgoingEmail(
            "alex@example.com", "Pickup", "<p>Hi.</p>", "Hi."); // no attachments

        await sender.SendBatchAsync([email], CancellationToken.None);

        using var document = JsonDocument.Parse(handler.CapturedBody!);
        var message = document.RootElement[0];

        Assert.False(
            message.TryGetProperty("Attachments", out _),
            "The Attachments property must be omitted when there are no attachments.");
    }

    /// <summary>Captures the outgoing request body and answers with a minimal all-sent batch response.</summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            // One sent result is enough — these tests only assert on the request payload.
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "[{\"ErrorCode\":0,\"Message\":\"OK\",\"MessageID\":\"m1\",\"To\":\"x@example.com\"}]",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }
}
