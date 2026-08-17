using NorthernLink.Shared.Kernel;

namespace NorthernLink.Shared.EventBus;

/// <summary>
/// Broker settings, read from the process environment only — never from appsettings.json, so a
/// committed config file can never carry a broker address or credential. Credentials are required;
/// host, port and exchange fall back to the local-dev defaults when their variables are unset.
/// </summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public required string UserName { get; init; }
    public required string Password { get; init; }

    /// <summary>Single topic exchange all integration events flow through.</summary>
    public string ExchangeName { get; init; } = "northernlink.events";

    /// <summary>
    /// Reads every value from <c>RabbitMq__*</c> environment variables. Throws if a credential is
    /// missing or if <c>RabbitMq__Port</c> is set to something that is not a port number.
    /// </summary>
    public static RabbitMqOptions FromEnvironment() => new()
    {
        HostName = ReadOptional("RabbitMq__HostName") ?? "localhost",
        Port = ReadPort(),
        UserName = RequiredEnvironmentVariable.Get("RabbitMq__UserName"),
        Password = RequiredEnvironmentVariable.Get("RabbitMq__Password"),
        ExchangeName = ReadOptional("RabbitMq__ExchangeName") ?? "northernlink.events",
    };

    private static string? ReadOptional(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int ReadPort()
    {
        var value = ReadOptional("RabbitMq__Port");
        if (value is null)
        {
            return 5672;
        }

        if (!int.TryParse(value, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                $"The RabbitMq__Port environment variable is not a valid port number: '{value}'.");
        }

        return port;
    }
}
