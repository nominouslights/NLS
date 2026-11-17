namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// "The same vehicle" for defect purposes — exactly the rule the defects panel
/// (<c>VehicleDefectReadService</c>) uses to decide which DVIRs belong to one truck, so clearing
/// duplicates can never reach a defect the panel would not have shown on that truck.
///
/// For an inspection linked to a vehicle: every inspection carrying that
/// <see cref="VehicleId"/>, plus legacy unit-only inspections (no link) whose unit equals the
/// vehicle's CURRENT unit number. When that unit number is unknown (vehicle missing) only the id
/// matches — the panel's own fallback. For a legacy unit-only inspection there is no id to go
/// on, so the scope is its unit alone, whatever the other inspection's link. Units compare
/// ordinally: both sides are trimmed on write, and the panel's SQL equality is exact too.
/// </summary>
public sealed record DefectVehicleScope
{
    private DefectVehicleScope(Guid? vehicleId, string? unit)
    {
        VehicleId = vehicleId;
        Unit = unit;
    }

    /// <summary>The hard vehicle link, or null for a unit-only scope.</summary>
    public Guid? VehicleId { get; }

    /// <summary>
    /// With <see cref="VehicleId"/>: the vehicle's current unit number (null when unknown).
    /// Without: the addressed inspection's own unit.
    /// </summary>
    public string? Unit { get; }

    /// <summary>
    /// The scope of the vehicle <paramref name="addressed"/> was performed on.
    /// <paramref name="linkedVehicleUnitNumber"/> is the linked vehicle's current unit number
    /// (null when the vehicle is not found), and is ignored for a unit-only inspection.
    /// </summary>
    public static DefectVehicleScope Of(VehicleInspection addressed, string? linkedVehicleUnitNumber)
    {
        ArgumentNullException.ThrowIfNull(addressed);

        return addressed.VehicleId is { } vehicleId
            ? new DefectVehicleScope(vehicleId, string.IsNullOrWhiteSpace(linkedVehicleUnitNumber) ? null : linkedVehicleUnitNumber)
            : new DefectVehicleScope(null, addressed.Unit);
    }

    /// <summary>True when <paramref name="inspection"/> was performed on this vehicle.</summary>
    public bool Includes(VehicleInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        return VehicleId is { } vehicleId
            ? inspection.VehicleId == vehicleId
                || (Unit is not null && inspection.VehicleId is null && string.Equals(inspection.Unit, Unit, StringComparison.Ordinal))
            : string.Equals(inspection.Unit, Unit, StringComparison.Ordinal);
    }
}
