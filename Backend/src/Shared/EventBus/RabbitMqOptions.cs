namespace NorthernLink.Shared.EventBus;

/// <summary>Bound from the "RabbitMq" configuration section.</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";

    /// <summary>Single topic exchange all integration events flow through.</summary>
    public string ExchangeName { get; init; } = "northernlink.events";
}
