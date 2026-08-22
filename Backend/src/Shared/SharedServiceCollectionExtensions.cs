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

        // Every broker setting — address included, not just the credentials — comes from the
        // environment; there is no RabbitMq section in appsettings.json to bind.
        services.AddSingleton(RabbitMqOptions.FromEnvironment());
        services.AddSingleton<RabbitMqIntegrationEventBus>();
        services.AddSingleton<IIntegrationEventBus>(sp => sp.GetRequiredService<RabbitMqIntegrationEventBus>());
        services.AddSingleton<IOutboxTransport>(sp => sp.GetRequiredService<RabbitMqIntegrationEventBus>());
        services.AddHostedService<RabbitMqInitializer>();

        var outboxOptions = configuration.GetSection(OutboxOptions.SectionName).Get<OutboxOptions>()
            ?? new OutboxOptions();
        services.AddSingleton(outboxOptions);

        var outboxPollingOptions = configuration.GetSection(OutboxPollingOptions.SectionName).Get<OutboxPollingOptions>()
            ?? new OutboxPollingOptions();
        services.AddSingleton(outboxPollingOptions);

        // Chain-reaction events that must go through RabbitMQ list their types here.
        // Currently empty: every integration event is storing/projecting and is consumed
        // in-database by the modules' outbox polling consumers.
        services.AddSingleton(new BusPublicationRegistry());

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
