namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>§6 "all cargo secured" confirmation — NotApplicable when no cargo was carried.</summary>
public enum CargoSecuredStatus
{
    Yes,
    NotApplicable,
}
