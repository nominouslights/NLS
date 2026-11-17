namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// "The same vehicle" for defect purposes — EXACTLY the rule the defects panel
/// (<c>VehicleDefectReadService</c>) uses to decide which DVIRs belong to one truck, so clearing
/// duplicates can never reach a defect the panel would not have listed on that truck.
///
/// The scope is built from a VEHICLE, resolved for the source inspection by the caller
/// (<c>DuplicateDefectCascade.ScopeForAsync</c>): the source's linked vehicle when it has a
/// <see cref="VehicleInspection.VehicleId"/>, otherwise the vehicle whose CURRENT unit number is
/// the source's unit text.
/// <list type="bullet">
/// <item><see cref="ForVehicle"/> — <c>VehicleId == v.Id || (VehicleId == null &amp;&amp; Unit ==
/// v.UnitNumber)</c>: the vehicle's linked inspections plus legacy unit-only inspections whose
/// unit is its current number. With the unit number unknown (the linked vehicle is gone) only the
/// id matches — the panel's own fallback.</item>
/// <item><see cref="UnitOnly"/> — no vehicle was found for a unit-only source, so the scope is the
/// unit-only inspections (<c>VehicleId == null</c>) carrying that unit text, and nothing else.</item>
/// </list>
/// An inspection linked to a DIFFERENT vehicle is never in scope, whatever its unit text says:
/// the link wins. Units compare ordinally — both sides are trimmed on write, and the panel's SQL
/// equality is exact too.
/// </summary>
public sealed record DefectVehicleScope
{
    private DefectVehicleScope(Guid? vehicleId, string? unit)
    {
        VehicleId = vehicleId;
        Unit = unit;
    }

    /// <summary>The vehicle, or null for a <see cref="UnitOnly"/> scope.</summary>
    public Guid? VehicleId { get; }

    /// <summary>
    /// With <see cref="VehicleId"/>: the vehicle's CURRENT unit number (null when unknown, which
    /// leaves the scope id-only). Without: the unit text the unit-only inspections must carry.
    /// </summary>
    public string? Unit { get; }

    /// <summary>The panel's scope for vehicle <paramref name="vehicleId"/>, whose current unit number is <paramref name="currentUnitNumber"/> (null when unknown).</summary>
    public static DefectVehicleScope ForVehicle(Guid vehicleId, string? currentUnitNumber) =>
        new(vehicleId, string.IsNullOrWhiteSpace(currentUnitNumber) ? null : currentUnitNumber);

    /// <summary>Unit-only inspections carrying <paramref name="unit"/> — for a unit-only source no vehicle answers to.</summary>
    public static DefectVehicleScope UnitOnly(string unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return new DefectVehicleScope(null, unit);
    }

    /// <summary>True when <paramref name="inspection"/> was performed on this vehicle.</summary>
    public bool Includes(VehicleInspection inspection)
    {
        ArgumentNullException.ThrowIfNull(inspection);

        if (VehicleId is { } vehicleId)
        {
            return inspection.VehicleId == vehicleId
                || (Unit is not null
                    && inspection.VehicleId is null
                    && string.Equals(inspection.Unit, Unit, StringComparison.Ordinal));
        }

        return inspection.VehicleId is null && string.Equals(inspection.Unit, Unit, StringComparison.Ordinal);
    }
}
