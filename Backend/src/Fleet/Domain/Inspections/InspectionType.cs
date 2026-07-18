namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>Which half of the trip manifest an inspection record came from.</summary>
public enum InspectionType
{
    PreTrip,
    PostTrip,
}
