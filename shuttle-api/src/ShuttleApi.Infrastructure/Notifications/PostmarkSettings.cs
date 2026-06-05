namespace ShuttleApi.Infrastructure.Notifications;

public sealed class PostmarkSettings
{
    public const string SectionName = "Postmark";

    public string ServerToken { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public string? MessageStream { get; init; }
}
