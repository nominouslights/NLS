using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NorthernLink.Shared.Kernel;
using NorthernLink.Notifications.Application.Abstractions;

namespace NorthernLink.Notifications.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> over Postmark's <c>/email/batch</c> endpoint — raw JSON via
/// <c>IHttpClientFactory</c>, no SDK, no Polly (one endpoint, manual ≤16-recipient sends).
/// The server token is read lazily from the <c>Notifications__PostmarkApiKey</c> environment
/// variable per request, never at registration time. Honors the <see cref="IEmailSender"/>
/// contract strictly: this class never throws — a whole-request failure (bad token, network,
/// non-2xx) degrades to all-failed outcomes carrying a transport error code.
/// </summary>
public sealed class PostmarkEmailSender(HttpClient httpClient, NotificationsOptions options) : IEmailSender
{
    /// <summary>The environment variable carrying the Postmark server token.</summary>
    public const string ApiKeyVariable = "Notifications__PostmarkApiKey";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = null, // Postmark expects PascalCase.
    };

    public async Task<IReadOnlyList<EmailSendOutcome>> SendBatchAsync(
        IReadOnlyList<OutgoingEmail> emails,
        CancellationToken cancellationToken = default)
    {
        if (emails.Count == 0)
        {
            return [];
        }

        try
        {
            var messages = emails
                .Select(email => new PostmarkMessage(
                    options.FromAddress,
                    email.To,
                    email.Subject,
                    email.HtmlBody,
                    email.TextBody,
                    options.MessageStream,
                    // Null (not empty) when there are no attachments so the property is omitted
                    // entirely — the attachment-free pickup batch stays byte-for-byte unchanged.
                    email.Attachments is { Count: > 0 } attachments
                        ? attachments
                            .Select(a => new PostmarkAttachment(a.Name, a.Base64Content, a.ContentType))
                            .ToList()
                        : null))
                .ToList();

            using var request = new HttpRequestMessage(HttpMethod.Post, "/email/batch");
            request.Headers.Add("X-Postmark-Server-Token", RequiredEnvironmentVariable.Get(ApiKeyVariable));
            request.Headers.Add("Accept", "application/json");
            request.Content = JsonContent.Create(messages, options: SerializerOptions);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var code = (int)response.StatusCode == 401
                    ? "Postmark.Unauthorized"
                    : $"Postmark.Http{(int)response.StatusCode}";
                return AllFailed(emails.Count, code,
                    $"Postmark rejected the batch request with HTTP {(int)response.StatusCode}.");
            }

            var results = await response.Content.ReadFromJsonAsync<List<PostmarkMessageResult>>(
                SerializerOptions, cancellationToken) ?? [];

            return MapOutcomes(emails.Count, results);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-initiated cancellation (client disconnect) — still an outcome, not a throw.
            return AllFailed(emails.Count, "Postmark.Unreachable", "The send was cancelled before completing.");
        }
        catch (Exception exception)
        {
            // DNS failure, timeout, malformed response body, missing token — the whole class
            // of transport problems the dispatcher must see as failed outcomes, not a 500.
            return AllFailed(emails.Count, "Postmark.Unreachable", exception.Message);
        }
    }

    /// <summary>
    /// Maps Postmark's per-message results (same order as the submitted batch) onto outcomes
    /// aligned with the input: ErrorCode 0 → sent (with message id), non-zero →
    /// <c>Postmark.{code}</c>, and a short result list pads with a missing-result failure.
    /// Pure and static so unit tests exercise the mapping without HTTP.
    /// </summary>
    public static IReadOnlyList<EmailSendOutcome> MapOutcomes(
        int emailCount,
        IReadOnlyList<PostmarkMessageResult> results) =>
        Enumerable.Range(0, emailCount)
            .Select(index =>
            {
                if (index >= results.Count)
                {
                    return new EmailSendOutcome(
                        false, "Postmark.MissingResult", "Postmark returned no result for this message.", null);
                }

                var result = results[index];
                return result.ErrorCode == 0
                    ? new EmailSendOutcome(true, null, null, result.MessageID)
                    : new EmailSendOutcome(false, $"Postmark.{result.ErrorCode}", result.Message, null);
            })
            .ToList();

    private static IReadOnlyList<EmailSendOutcome> AllFailed(int count, string errorCode, string errorMessage) =>
        Enumerable.Repeat(new EmailSendOutcome(false, errorCode, errorMessage, null), count).ToList();

    private sealed record PostmarkMessage(
        string From,
        string To,
        string Subject,
        string HtmlBody,
        string TextBody,
        string MessageStream,
        // Omitted from the payload entirely when null (the no-attachment case), so a plain
        // pickup send serializes exactly as before this feature existed.
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<PostmarkAttachment>? Attachments);

    /// <summary>Postmark's attachment shape: Content is the Base64-encoded file bytes.</summary>
    private sealed record PostmarkAttachment(string Name, string Content, string ContentType);

    /// <summary>One entry of Postmark's batch response, in submission order.</summary>
    public sealed record PostmarkMessageResult(
        [property: JsonPropertyName("ErrorCode")] int ErrorCode,
        [property: JsonPropertyName("Message")] string? Message,
        [property: JsonPropertyName("MessageID")] string? MessageID,
        [property: JsonPropertyName("To")] string? To);
}
