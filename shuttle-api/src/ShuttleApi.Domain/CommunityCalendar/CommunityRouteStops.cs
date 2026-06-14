namespace ShuttleApi.Domain.CommunityCalendar;

public static class CommunityRouteStops
{
    public static readonly IReadOnlyDictionary<string, (int Order, string Name, string? Address)[]> Outbound =
        new Dictionary<string, (int, string, string?)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["LynnLake"] = [(1, "Thompson", "Thompson, MB"), (2, "Lynn Lake", "Lynn Lake, MB")],
            ["LeafRapids"] = [(1, "Thompson", "Thompson, MB"), (2, "Leaf Rapids", "Leaf Rapids, MB")],
        };

    public static readonly IReadOnlyDictionary<string, (int Order, string Name, string? Address)[]> Inbound =
        new Dictionary<string, (int, string, string?)[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["LynnLake"] = [(1, "Lynn Lake", "Lynn Lake, MB"), (2, "Thompson", "Thompson, MB")],
            ["LeafRapids"] = [(1, "Leaf Rapids", "Leaf Rapids, MB"), (2, "Thompson", "Thompson, MB")],
        };

    public static string DisplayName(string destination) =>
        destination.Equals("LeafRapids", StringComparison.OrdinalIgnoreCase) ? "Leaf Rapids" : "Lynn Lake";
}
