using NorthernLink.Shared.EventBus;

namespace NorthernLink.Shared.Persistence.Auditing;

/// <summary>
/// Stable names stored in the audit tables — kebab-cased type names, mirroring the
/// integration-event routing-key convention, never CLR or assembly-qualified names.
/// Renaming a domain event or aggregate class is therefore a breaking change to the
/// audit vocabulary: existing rows keep the old name, and any viewer/query keyed on it
/// must account for both.
/// </summary>
public static class AuditNames
{
    private const string DomainEventSuffix = "DomainEvent";

    /// <summary>"VehicleStatusChangedDomainEvent" → "vehicle-status-changed".</summary>
    public static string ForEvent(Type domainEventType)
    {
        var name = domainEventType.Name;
        if (name.EndsWith(DomainEventSuffix, StringComparison.Ordinal))
        {
            name = name[..^DomainEventSuffix.Length];
        }

        return EventRoutingKey.ToKebabCase(name);
    }

    /// <summary>"Vehicle" → "vehicle"; "RetirementCertificate" → "retirement-certificate".</summary>
    public static string ForAggregate(Type aggregateType) => EventRoutingKey.ToKebabCase(aggregateType.Name);
}
