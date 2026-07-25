using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NorthernLink.Shared.Events;
using NorthernLink.Shared.Kernel;
using NorthernLink.Shared.Messaging;
using NorthernLink.Shared.EventBus;
using NorthernLink.Shared.Persistence.Auditing;
using NorthernLink.Shared.Persistence.Projections;
using NorthernLink.Shared.Storage;

namespace NorthernLink.Shared;

public static class SharedServiceCollectionExtensions
{
    /// <summary>
    /// Registers the platform-wide building blocks: the in-process command/query dispatcher,
    /// the RabbitMQ integration event bus, the outbox plumbing every module's dispatcher
    /// shares, and object storage for binary media. Called once by the API host, before modules.
    /// </summary>
    public static IServiceCollection AddNorthernLinkShared(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<ISender, Sender>();

        // Host/port/exchange are non-secret and stay config-bound; the credential pair is
        // always sourced from the environment, never appsettings.json.
        var boundRabbitMqOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
            ?? new RabbitMqOptions();
        var rabbitMqOptions = new RabbitMqOptions
        {
            HostName = boundRabbitMqOptions.HostName,
            Port = boundRabbitMqOptions.Port,
            UserName = RequiredEnvironmentVariable.Get("RabbitMq__UserName"),
            Password = RequiredEnvironmentVariable.Get("RabbitMq__Password"),
            ExchangeName = boundRabbitMqOptions.ExchangeName,
        };
        services.AddSingleton(rabbitMqOptions);
        services.AddSingleton<RabbitMqIntegrationEventBus>();
        services.AddSingleton<IIntegrationEventBus>(sp => sp.GetRequiredService<RabbitMqIntegrationEventBus>());
        services.AddSingleton<IOutboxTransport>(sp => sp.GetRequiredService<RabbitMqIntegrationEventBus>());
        services.AddHostedService<RabbitMqInitializer>();

        var outboxOptions = configuration.GetSection(OutboxOptions.SectionName).Get<OutboxOptions>()
            ?? new OutboxOptions();
        services.AddSingleton(outboxOptions);

        var projectionOptions = configuration.GetSection(ProjectionOptions.SectionName).Get<ProjectionOptions>()
            ?? new ProjectionOptions();
        services.AddSingleton(projectionOptions);

        // Object storage: endpoint/bucket are config-bound (non-secret); credentials come from environment.
        var objectStorageOptions = configuration.GetSection("ObjectStorage").Get<ObjectStorageOptions>()
            ?? new ObjectStorageOptions { ServiceUrl = "https://tor1.digitaloceanspaces.com", BucketName = "nlstorage" };
        services.AddSingleton<IObjectStorage>(
            _ => new DigitalOceanSpacesObjectStorage(
                objectStorageOptions.ServiceUrl,
                objectStorageOptions.BucketName,
                RequiredEnvironmentVariable.Get("ObjectStorage__AccessKey"),
                RequiredEnvironmentVariable.Get("ObjectStorage__SecretKey")
            )
        );

        return services;
    }
}

/// <summary>Config section for object storage (endpoint, bucket). Credentials stay in the environment.</summary>
internal sealed class ObjectStorageOptions
{
    public string ServiceUrl { get; set; } = null!;
    public string BucketName { get; set; } = null!;
}
