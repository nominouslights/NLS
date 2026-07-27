using NorthernLink.Clients.Domain.ClientInteractions;

namespace NorthernLink.Clients.Application.ClientInteractions;

/// <summary>
/// Maps between the <see cref="InteractionType"/> enum and its wire string. Every value
/// round-trips by name except <see cref="InteractionType.SiteVisit"/>, whose wire form is
/// "Site Visit" (with a space). An unknown or blank inbound value falls back to
/// <see cref="InteractionType.Other"/>.
/// </summary>
public static class InteractionTypeWire
{
    public const string SiteVisitWire = "Site Visit";

    public static string ToWire(InteractionType type) =>
        type == InteractionType.SiteVisit ? SiteVisitWire : type.ToString();

    public static InteractionType FromWire(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return InteractionType.Other;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, SiteVisitWire, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, nameof(InteractionType.SiteVisit), StringComparison.OrdinalIgnoreCase))
        {
            return InteractionType.SiteVisit;
        }

        return Enum.TryParse<InteractionType>(normalized, ignoreCase: true, out var parsed)
            ? parsed
            : InteractionType.Other;
    }
}
