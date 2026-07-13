using System.Text.Json;

namespace NorthernLink.Shared.Events;

/// <summary>
/// The single wire format for integration events: camelCase Web defaults. Used by the
/// bus's direct publish, the outbox writer (whose stored payload is the exact bytes
/// later published), and the consumer's deserialization — one definition so producer
/// and consumer can never disagree.
/// </summary>
public static class IntegrationEventJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);

    public static string Serialize(IIntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), Options);
}
