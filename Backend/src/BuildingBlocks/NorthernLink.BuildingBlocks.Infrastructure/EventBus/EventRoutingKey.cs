using NorthernLink.BuildingBlocks.Application.Events;

namespace NorthernLink.BuildingBlocks.Infrastructure.EventBus;

/// <summary>
/// Derives the routing key <c>&lt;module&gt;.&lt;event-name&gt;</c> from an integration
/// event type. The module segment comes from the type's namespace
/// (…Modules.&lt;Module&gt;.Contracts…), the event segment from the kebab-cased type name
/// with the "IntegrationEvent" suffix stripped —
/// e.g. NorthernLink.Modules.Trips.Contracts.TripCompletedIntegrationEvent → "trips.trip-completed".
/// </summary>
public static class EventRoutingKey
{
    private const string ModulesMarker = ".Modules.";
    private const string EventSuffix = "IntegrationEvent";

    public static string For(Type eventType)
    {
        if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
        {
            throw new ArgumentException($"{eventType.Name} is not an IIntegrationEvent.", nameof(eventType));
        }

        var module = ModuleSegment(eventType);
        var eventName = EventSegment(eventType);
        return $"{module}.{eventName}";
    }

    private static string ModuleSegment(Type eventType)
    {
        var ns = eventType.Namespace ?? string.Empty;
        var markerIndex = ns.IndexOf(ModulesMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            // Events outside a module namespace (rare) fall back to "platform".
            return "platform";
        }

        var start = markerIndex + ModulesMarker.Length;
        var end = ns.IndexOf('.', start);
        var module = end < 0 ? ns[start..] : ns[start..end];
        return module.ToLowerInvariant();
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

    private static string ToKebabCase(string value)
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
