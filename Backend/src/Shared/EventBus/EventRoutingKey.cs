using NorthernLink.Shared.Events;

namespace NorthernLink.Shared.EventBus;

/// <summary>
/// Derives the routing key <c>&lt;domain&gt;.&lt;event-name&gt;</c> from an integration
/// event type. The domain segment comes from the type's namespace
/// (…IntegrationEvents.&lt;Domain&gt;…, i.e. NorthernLink.Shared.IntegrationEvents.&lt;Domain&gt;),
/// the event segment from the kebab-cased type name with the "IntegrationEvent" suffix
/// stripped — e.g. NorthernLink.Shared.IntegrationEvents.Trips.TripCompletedIntegrationEvent
/// → "trips.trip-completed".
/// </summary>
public static class EventRoutingKey
{
    private const string DomainMarker = ".IntegrationEvents.";
    private const string EventSuffix = "IntegrationEvent";

    public static string For(Type eventType)
    {
        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
        {
            throw new ArgumentException($"{eventType.Name} is not an IIntegrationEvent.", nameof(eventType));
        }

        var domain = DomainSegment(eventType);
        var eventName = EventSegment(eventType);
        return $"{domain}.{eventName}";
    }

    private static string DomainSegment(Type eventType)
    {
        var ns = eventType.Namespace ?? string.Empty;
        var markerIndex = ns.IndexOf(DomainMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            // Events outside the IntegrationEvents namespace (rare) fall back to "platform".
            return "platform";
        }

        var start = markerIndex + DomainMarker.Length;
        var end = ns.IndexOf('.', start);
        var domain = end < 0 ? ns[start..] : ns[start..end];
        return domain.ToLowerInvariant();
    }

    private static string EventSegment(Type eventType)
    {
        var name = eventType.Name;
        if (name.EndsWith(EventSuffix, StringComparison.Ordinal))
        {
            name = name[..^EventSuffix.Length];
        }

        return ToKebabCase(name);
    }

    // Internal: also the audit tables' naming convention (see Persistence.Auditing.AuditNames),
    // so event names in the journal and routing keys on the wire can never drift apart.
    internal static string ToKebabCase(string value)
    {
        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
