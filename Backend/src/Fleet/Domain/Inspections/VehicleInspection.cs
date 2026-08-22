using NorthernLink.Fleet.Domain.Inspections.Events;
using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// One vehicle inspection record — a pre- or post-trip DVIR entered directly from the trip
/// workflow (or, later, the Driver Field App) and kept as the Fleet source of truth. Pre-trip
/// records carry the departure weather/road conditions, fuel level, and odometer-in reading;
/// post-trip records carry the issues log, certification (attestations + signature), fuel added,
/// and odometer-out reading. <see cref="Result"/> is derived, not supplied: no defects → Pass,
/// only Minor defects → PassWithDefects, any Major or OutOfService defect → Fail. The odometer
/// reading advances the linked <see cref="Vehicles.Vehicle"/> intra-Fleet (a same-module
/// reaction to <see cref="VehicleInspectionCreatedDomainEvent"/>).
/// </summary>
public sealed class VehicleInspection : AggregateRoot, ITenantScoped
{
    private VehicleInspection()
    {
        // EF Core materialization only.
        Unit = null!;
        DriverName = null!;
    }

    public Guid TenantId { get; private set; }

    /// <summary>Hard link to the vehicle asset; null for legacy or unit-only entries.</summary>
    public Guid? VehicleId { get; private set; }

    public string Unit { get; private set; }
    public InspectionType Type { get; private set; }
    public string DriverName { get; private set; }
    public InspectionSource Source { get; private set; }
    public string? EnteredBy { get; private set; }

    /// <summary>Trip number for trip-context inspections; null for standalone vehicle entries.</summary>
    public string? TripNumber { get; private set; }

    /// <summary>Legacy manifest id (manifest-materialized inspections); null for trip-context entries.</summary>
    public Guid? ManifestId { get; private set; }

    public DateTimeOffset PerformedAt { get; private set; }

    /// <summary>Odometer reading — odometer-in on a pre-trip, odometer-out on a post-trip.</summary>
    public int? OdometerKm { get; private set; }

    public InspectionResult Result { get; private set; }
    public List<InspectionChecklistItem> ChecklistItems { get; private set; } = [];
    public List<InspectionDefect> Defects { get; private set; } = [];

    // Pre-trip conditions (moved off the manifest §4/§2).
    public List<InspectionWeather> Weather { get; private set; } = [];
    public string? TemperatureC { get; private set; }
    public List<InspectionRoadCondition> RoadConditions { get; private set; } = [];
    public InspectionVisibility? Visibility { get; private set; }
    public string? RoadAdvisories { get; private set; }
    public InspectionFuelLevel? FuelLevel { get; private set; }

    // Post-trip log & certification (moved off the manifest §7/§8/§10).
    public List<string> Issues { get; private set; } = [];
    public List<bool> Attestations { get; private set; } = [];
    public string? DriverSignatureName { get; private set; }
    public DateTimeOffset? CertifiedAt { get; private set; }
    public bool FuelAdded { get; private set; }
    public decimal? FuelLitres { get; private set; }
    public decimal? FuelCostCad { get; private set; }

    /// <summary>The work order this inspection's defects generated, if any (set on WO creation).</summary>
    public Guid? GeneratedWorkOrderId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>
    /// The single entry point for inspections. <paramref name="source"/> attributes the entry
    /// (<see cref="InspectionSource.DriverApp"/> or <see cref="InspectionSource.Dispatcher"/>);
    /// pre-trip section fields (weather/road/visibility/fuel) apply to a
    /// <see cref="InspectionType.PreTrip"/> record, the log/certification/fuel-added fields to a
    /// <see cref="InspectionType.PostTrip"/> record — callers simply pass empty/null for the half
    /// that does not apply. <see cref="Result"/> is derived from <paramref name="defects"/>.
    /// </summary>
    public static Result<VehicleInspection> Enter(
        Guid tenantId,
        InspectionSource source,
        InspectionType type,
        string? tripNumber,
        Guid? vehicleId,
        string unit,
        string driverName,
        string? enteredBy,
        DateTimeOffset performedAt,
        int? odometerKm,
        IReadOnlyList<InspectionChecklistItem> checklistItems,
        IReadOnlyList<InspectionDefect> defects,
        IReadOnlyList<InspectionWeather> weather,
        string? temperatureC,
        IReadOnlyList<InspectionRoadCondition> roadConditions,
        InspectionVisibility? visibility,
        string? roadAdvisories,
        InspectionFuelLevel? fuelLevel,
        IReadOnlyList<string> issues,
        IReadOnlyList<bool> attestations,
        string? driverSignatureName,
        DateTimeOffset? certifiedAt,
        bool fuelAdded,
        decimal? fuelLitres,
        decimal? fuelCostCad)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.UnitRequired);
        }

        if (string.IsNullOrWhiteSpace(driverName))
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.DriverRequired);
        }

        var enteredByValue = string.IsNullOrWhiteSpace(enteredBy)
            ? source == InspectionSource.Dispatcher ? "Dispatch" : null
            : enteredBy.Trim();

        var inspection = new VehicleInspection
        {
            TenantId = tenantId,
            VehicleId = vehicleId,
            Unit = unit.Trim(),
            Type = type,
            DriverName = driverName.Trim(),
            Source = source,
            EnteredBy = enteredByValue,
            TripNumber = string.IsNullOrWhiteSpace(tripNumber) ? null : tripNumber.Trim(),
            ManifestId = null,
            PerformedAt = performedAt,
            OdometerKm = odometerKm,
            Result = DeriveResult(defects),
            ChecklistItems = [.. checklistItems],
            Defects = [.. defects],
            Weather = [.. weather],
            TemperatureC = Normalize(temperatureC),
            RoadConditions = [.. roadConditions],
            Visibility = visibility,
            RoadAdvisories = Normalize(roadAdvisories),
            FuelLevel = fuelLevel,
            Issues = [.. issues],
            Attestations = [.. attestations],
            DriverSignatureName = Normalize(driverSignatureName),
            CertifiedAt = certifiedAt,
            FuelAdded = fuelAdded,
            FuelLitres = fuelLitres,
            FuelCostCad = fuelCostCad,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        inspection.Raise(new VehicleInspectionCreatedDomainEvent(inspection.Id, tenantId));
        return NorthernLink.Shared.Kernel.Result.Success(inspection);
    }

    /// <summary>
    /// Corrects an existing inspection's contents in place. Identity is fixed:
    /// <see cref="Type"/> and <see cref="TripNumber"/> never change here — an amend fixes the
    /// odometer/driver/checklist/defects/sections/unit/vehicle link of the same pre- or post-trip
    /// record, not which trip or which half it is (re-record by removing and re-entering for
    /// that). Re-runs the same validation as <see cref="Enter"/> and re-derives
    /// <see cref="Result"/> from the corrected <paramref name="defects"/>. Raises
    /// <see cref="VehicleInspectionAmendedDomainEvent"/>.
    /// </summary>
    public Result Amend(
        InspectionSource source,
        Guid? vehicleId,
        string unit,
        string driverName,
        string? enteredBy,
        DateTimeOffset performedAt,
        int? odometerKm,
        IReadOnlyList<InspectionChecklistItem> checklistItems,
        IReadOnlyList<InspectionDefect> defects,
        IReadOnlyList<InspectionWeather> weather,
        string? temperatureC,
        IReadOnlyList<InspectionRoadCondition> roadConditions,
        InspectionVisibility? visibility,
        string? roadAdvisories,
        InspectionFuelLevel? fuelLevel,
        IReadOnlyList<string> issues,
        IReadOnlyList<bool> attestations,
        string? driverSignatureName,
        DateTimeOffset? certifiedAt,
        bool fuelAdded,
        decimal? fuelLitres,
        decimal? fuelCostCad)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.UnitRequired);
        }

        if (string.IsNullOrWhiteSpace(driverName))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DriverRequired);
        }

        Source = source;
        EnteredBy = string.IsNullOrWhiteSpace(enteredBy)
            ? source == InspectionSource.Dispatcher ? "Dispatch" : null
            : enteredBy.Trim();
        VehicleId = vehicleId;
        Unit = unit.Trim();
        DriverName = driverName.Trim();
        PerformedAt = performedAt;
        OdometerKm = odometerKm;
        Result = DeriveResult(defects);
        ChecklistItems = [.. checklistItems];
        Defects = [.. defects];
        Weather = [.. weather];
        TemperatureC = Normalize(temperatureC);
        RoadConditions = [.. roadConditions];
        Visibility = visibility;
        RoadAdvisories = Normalize(roadAdvisories);
        FuelLevel = fuelLevel;
        Issues = [.. issues];
        Attestations = [.. attestations];
        DriverSignatureName = Normalize(driverSignatureName);
        CertifiedAt = certifiedAt;
        FuelAdded = fuelAdded;
        FuelLitres = fuelLitres;
        FuelCostCad = fuelCostCad;

        Raise(new VehicleInspectionAmendedDomainEvent(Id, TenantId));
        return NorthernLink.Shared.Kernel.Result.Success();
    }

    /// <summary>
    /// Flags this inspection for hard removal. Raised just before the repository deletes the row
    /// so the removal is carried across the module boundary (the mapper only ever sees real
    /// domain events; the synthetic aggregate-deleted journal row is not mapped). Read-model
    /// deletion is driven separately by that synthetic journal row.
    /// </summary>
    public void MarkRemoved() => Raise(new VehicleInspectionRemovedDomainEvent(Id, TenantId));

    /// <summary>
    /// Links the work order generated from this inspection's defects. One generation per
    /// inspection: fails once <see cref="GeneratedWorkOrderId"/> is set — deliberately even if
    /// that earlier work order was cancelled (the cancelled-WO escape hatch needs a
    /// cross-aggregate read and is a documented follow-up).
    /// </summary>
    public Result LinkWorkOrder(Guid workOrderId)
    {
        if (GeneratedWorkOrderId is not null)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.WorkOrderAlreadyGenerated);
        }

        GeneratedWorkOrderId = workOrderId;

        Raise(new VehicleInspectionWorkOrderLinkedDomainEvent(Id, workOrderId));
        return NorthernLink.Shared.Kernel.Result.Success();
    }

    /// <summary>The derivation rule: Pass / PassWithDefects (all Minor) / Fail (any Major or OutOfService).</summary>
    public static InspectionResult DeriveResult(IReadOnlyList<InspectionDefect> defects)
    {
        if (defects.Count == 0)
        {
            return InspectionResult.Pass;
        }

        return defects.Any(d => d.Severity is InspectionDefectSeverity.Major or InspectionDefectSeverity.OutOfService)
            ? InspectionResult.Fail
            : InspectionResult.PassWithDefects;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
