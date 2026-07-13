namespace NorthernLink.Fleet.Domain.Vehicles;

/// <summary>How a retired vehicle leaves the fleet. Both outcomes are terminal.</summary>
public enum DisposalMethod
{
    Sold,
    Recycled,
}
