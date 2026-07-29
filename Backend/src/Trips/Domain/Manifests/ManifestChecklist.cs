namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// Row limits for the passenger + cargo manifest, as printed on the NL-TM-01 form. The
/// pre/post-trip inspection checklists and certification attestations that used to live
/// here moved to Fleet's <c>VehicleInspection</c> when inspections detached from the
/// manifest.
/// </summary>
public static class ManifestChecklist
{
    /// <summary>§5 passenger row limit, as printed on the form.</summary>
    public const int MaxPassengers = 8;

    /// <summary>§6 cargo row limit, as printed on the form.</summary>
    public const int MaxCargoItems = 4;
}
