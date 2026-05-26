using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public sealed class TripInspectionItem : Entity<Guid>
{
    public Guid PreInspectionId { get; private set; }
    public string ItemName { get; private set; } = string.Empty;
    public bool Passed { get; private set; }
    public string? Notes { get; private set; }

    private TripInspectionItem() { }

    public static TripInspectionItem Create(Guid preInspectionId, string itemName, bool passed, string? notes)
    {
        return new TripInspectionItem
        {
            Id = Guid.NewGuid(),
            PreInspectionId = preInspectionId,
            ItemName = itemName,
            Passed = passed,
            Notes = notes
        };
    }
}
