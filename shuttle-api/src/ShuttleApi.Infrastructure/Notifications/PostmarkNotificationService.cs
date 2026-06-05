using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PostmarkDotNet;
using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Infrastructure.Notifications;

internal sealed class PostmarkNotificationService(
    IOptions<PostmarkSettings> options,
    ILogger<PostmarkNotificationService> logger)
    : INotificationService
{
    private readonly PostmarkSettings _settings = options.Value;

    public Task SendSmsAsync(string phone, string message, CancellationToken cancellationToken = default)
    {
        // Postmark is email-only. SMS has no provider yet; log so the call site keeps working.
        logger.LogInformation("[SMS skipped - no provider] To: {Phone} | {Message}", phone, message);
        return Task.CompletedTask;
    }

    public async Task SendEmailAsync(string email, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ServerToken))
        {
            logger.LogWarning(
                "[Postmark not configured] Skipping email. To: {Email} | Subject: {Subject}", email, subject);
            return;
        }

        var client = new PostmarkClient(_settings.ServerToken);

        var message = new PostmarkMessage
        {
            From = string.IsNullOrWhiteSpace(_settings.FromName)
                ? _settings.FromAddress
                : $"{_settings.FromName} <{_settings.FromAddress}>",
            To = email,
            Subject = subject,
            TextBody = body
        };

        if (!string.IsNullOrWhiteSpace(_settings.MessageStream))
            message.MessageStream = _settings.MessageStream;

        var response = await client.SendMessageAsync(message);

        if (response.Status != PostmarkStatus.Success)
        {
            logger.LogError(
                "Postmark email failed. To: {Email} | Subject: {Subject} | Code: {ErrorCode} | Message: {Message}",
                email, subject, response.ErrorCode, response.Message);
            throw new InvalidOperationException($"Postmark email failed: {response.Message}");
        }

        logger.LogInformation("Postmark email sent. To: {Email} | Subject: {Subject}", email, subject);
    }
}
