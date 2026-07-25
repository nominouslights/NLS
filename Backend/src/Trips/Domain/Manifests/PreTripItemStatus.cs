namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>§3 per-item outcome. A Fail must carry a severity and a note.</summary>
public enum PreTripItemStatus
{
    Ok,
    Fail,
}
