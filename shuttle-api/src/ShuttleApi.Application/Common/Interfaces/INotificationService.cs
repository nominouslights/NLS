namespace ShuttleApi.Application.Common.Interfaces;

public interface INotificationService
{
    Task SendSmsAsync(string phone, string message, CancellationToken cancellationToken = default);
    Task SendEmailAsync(string email, string subject, string body, CancellationToken cancellationToken = default);
}
