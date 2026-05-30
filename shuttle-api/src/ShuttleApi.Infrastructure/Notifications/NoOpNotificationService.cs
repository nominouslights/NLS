using Microsoft.Extensions.Logging;
using ShuttleApi.Application.Common.Interfaces;

namespace ShuttleApi.Infrastructure.Notifications;

internal sealed class NoOpNotificationService(ILogger<NoOpNotificationService> logger)
    : INotificationService
{
    public Task SendSmsAsync(string phone, string message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[NoOp SMS] To: {Phone} | {Message}", phone, message);
        return Task.CompletedTask;
    }

    public Task SendEmailAsync(string email, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[NoOp Email] To: {Email} | Subject: {Subject}", email, subject);
        return Task.CompletedTask;
    }
}
