namespace ShuttleApi.Domain.CommunityCalendar;

public static class CommunityRouteFares
{
    public static decimal OneWay(string destination) =>
        destination.Equals("LeafRapids", StringComparison.OrdinalIgnoreCase) ? 100m : 120m;

    public static decimal Return(string destination) => OneWay(destination) * 2;
}
