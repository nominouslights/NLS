using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public enum FuelLevel { Full, ThreeQuarters, Half, Quarter }

public sealed class TripPreInspection : Entity<Guid>
{
    private readonly List<TripInspectionItem> _items = [];

    public Guid TripId { get; private set; }
    public int OdometerStart { get; private set; }
    public FuelLevel FuelLevel { get; private set; }
    public string? WeatherType { get; private set; }
    public string? Temperature { get; private set; }
    public string? RoadConditions { get; private set; }
    public string? Visibility { get; private set; }
    public string? RoadAdvisories { get; private set; }
    public DateTime? WeatherPulledAt { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public IReadOnlyList<TripInspectionItem> Items => _items.AsReadOnly();

    private TripPreInspection() { }

    public static TripPreInspection Create(
        Guid tripId,
        int odometerStart,
        FuelLevel fuelLevel,
        string? weatherType,
        string? temperature,
        string? roadConditions,
        string? visibility,
        string? roadAdvisories,
        DateTime? weatherPulledAt,
        IEnumerable<(string ItemName, InspectionCategory Category, bool Passed, string? Notes)> items)
    {
        var inspection = new TripPreInspection
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            OdometerStart = odometerStart,
            FuelLevel = fuelLevel,
            WeatherType = weatherType,
            Temperature = temperature,
            RoadConditions = roadConditions,
            Visibility = visibility,
            RoadAdvisories = roadAdvisories,
            WeatherPulledAt = weatherPulledAt,
            SubmittedAt = DateTime.UtcNow
        };

        foreach (var (itemName, category, passed, notes) in items)
            inspection._items.Add(TripInspectionItem.Create(inspection.Id, itemName, category, passed, notes));

        return inspection;
    }
}
