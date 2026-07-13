using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// JSON options for audit payloads (journal events and aggregate snapshots): camelCase
/// like the wire format, enums as strings so the audit trail reads "Retired" rather
/// than 4, cycles ignored, and the <c>DomainEvents</c> collection stripped from any
/// <see cref="AggregateRoot"/> via a contract modifier — keeping the shared kernel free
/// of serialization attributes. Snapshots are viewer-only documents: they are serialized
/// here but never deserialized for rehydration, so private setters and constructors on
/// aggregates don't matter, and computed properties are captured as a bonus.
/// </summary>
public static class AuditJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static string Serialize(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { StripDomainEvents } },
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static void StripDomainEvents(JsonTypeInfo typeInfo)
    {
        if (!typeof(AggregateRoot).IsAssignableFrom(typeInfo.Type))
        {
            return;
        }

        for (var i = typeInfo.Properties.Count - 1; i >= 0; i--)
        {
            if (string.Equals(typeInfo.Properties[i].Name, nameof(AggregateRoot.DomainEvents), StringComparison.OrdinalIgnoreCase))
            {
                typeInfo.Properties.RemoveAt(i);
            }
        }
    }
}
