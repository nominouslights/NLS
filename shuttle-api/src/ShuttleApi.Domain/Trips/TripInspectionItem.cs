using ShuttleApi.Domain.Common;

namespace ShuttleApi.Domain.Trips;

public enum InspectionCategory
{
    ExteriorMechanical,
    SafetyEquipment,
    InteriorComfort,
    CommunicationsNavigation
}

public sealed class TripInspectionItem : Entity<Guid>
{
    public Guid PreInspectionId { get; private set; }
    public string ItemName { get; private set; } = string.Empty;
    public InspectionCategory Category { get; private set; }
    public bool Passed { get; private set; }
    public string? Notes { get; private set; }

    private TripInspectionItem() { }

    public static TripInspectionItem Create(
        Guid preInspectionId,
        string itemName,
        InspectionCategory category,
        bool passed,
        string? notes)
    {
        return new TripInspectionItem
        {
            Id = Guid.NewGuid(),
            PreInspectionId = preInspectionId,
            ItemName = itemName,
            Category = category,
            Passed = passed,
            Notes = notes
        };
    }
}
