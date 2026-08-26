namespace NorthernLink.Fleet.Domain.Maintenance;

/// <summary>What the shop does to the component when a maintenance item comes due.</summary>
public enum MaintenanceTask
{
    Inspect,
    Service,
    Test,
    Replace,
}
