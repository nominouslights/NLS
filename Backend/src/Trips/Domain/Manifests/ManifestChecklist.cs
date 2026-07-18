namespace NorthernLink.Trips.Domain.Manifests;

/// <summary>
/// The canonical contents of the NL-TM-01 Trip Manifest &amp; Vehicle Inspection Report:
/// 28 pre-trip items in 4 groups, 6 post-trip items, and the 5 certification
/// attestations — exactly as printed on the reference form. The backend owns this
/// contract; frontends mirror it (blank paper forms render every row from these lists),
/// so changing an item here is a form revision, not a refactor.
/// </summary>
public static class ManifestChecklist
{
    public const string ExteriorMechanicalGroup = "Exterior & Mechanical";
    public const string SafetyEquipmentGroup = "Safety Equipment";
    public const string InteriorComfortGroup = "Interior & Comfort";
    public const string CommunicationsNavigationGroup = "Communications & Navigation";

    /// <summary>§3 — the 28 pre-trip inspection items, grouped as on the form.</summary>
    public static readonly IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> PreTripGroups =
    [
        new(ExteriorMechanicalGroup,
        [
            "Tires",
            "Lights",
            "Windshield & mirrors",
            "Wipers & washer fluid",
            "Brakes",
            "Horn",
            "Exhaust",
            "Fluid levels",
            "Battery",
            "Block heater cord (winter)",
        ]),
        new(SafetyEquipmentGroup,
        [
            "First aid kit",
            "Fire extinguisher",
            "Emergency triangles / flares",
            "Spill kit (mine requirement)",
            "Safety beacon / whip flag",
            "Backup alarm",
            "Seatbelts",
            "Cargo restraints / tie-downs",
            "Reflective safety vest",
            "Jumper cables / booster pack",
        ]),
        new(InteriorComfortGroup,
        [
            "Interior clean",
            "Heat / AC",
            "Doors",
            "Dashboard — no warning lights",
        ]),
        new(CommunicationsNavigationGroup,
        [
            "Cell phone charged",
            "Starlink / satellite comm connected",
            "GPS / navigation",
            "Emergency contacts list",
        ]),
    ];

    /// <summary>§9 — the six post-trip checks.</summary>
    public static readonly IReadOnlyList<string> PostTripItems =
    [
        "Vehicle exterior — no new damage",
        "All passengers disembarked safely",
        "Vehicle secured / plugged in",
        "Interior cleaned & checked",
        "All cargo delivered / accounted for",
        "Keys returned / secured",
    ];

    /// <summary>§10 — the five attestation statements the driver certifies.</summary>
    public static readonly IReadOnlyList<string> Attestations =
    [
        "I have completed the pre-trip inspection and the vehicle is safe to operate",
        "All passengers were transported safely with seatbelts worn",
        "All cargo was properly secured",
        "All information on this manifest is accurate and complete",
        "I have reported all issues, incidents, or defects",
    ];

    /// <summary>§5 and §6 row limits, and the attestation count, as printed on the form.</summary>
    public const int MaxPassengers = 8;
    public const int MaxCargoItems = 4;
    public const int AttestationCount = 5;
}
