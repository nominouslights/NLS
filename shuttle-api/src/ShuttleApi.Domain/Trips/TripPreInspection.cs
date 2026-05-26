using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripPreInspection : Entity<Guid>
{
    private readonly List<TripInspectionItem> _items = [];

    public Guid TripId { get; private set; }
    public int OdometerStart { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public IReadOnlyList<TripInspectionItem> Items => _items.AsReadOnly();

    private TripPreInspection() { }

    public static TripPreInspection Create(
        Guid tripId,
        int odometerStart,
        IEnumerable<(string ItemName, bool Passed, string? Notes)> items)
    {
        var inspection = new TripPreInspection
        {
            Id = Guid.NewGuid(),
            TripId = tripId,
            OdometerStart = odometerStart,
            SubmittedAt = DateTime.UtcNow
        };

        foreach (var (itemName, passed, notes) in items)
            inspection._items.Add(TripInspectionItem.Create(inspection.Id, itemName, passed, notes));

        return inspection;
    }
}
