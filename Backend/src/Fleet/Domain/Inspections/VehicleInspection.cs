using NorthernLink.Shared.Kernel;

namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// One vehicle inspection record — the Fleet-side materialization of a trip manifest's
/// pre- or post-trip checklist, created by the <c>trips.trip-manifest-completed</c>
/// integration event consumer (never through an endpoint: the manifest is the source
/// of truth, this is Fleet's read-optimized copy keyed by unit). <see cref="Result"/>
/// is derived, not supplied: no defects → Pass, only Minor defects → PassWithDefects,
/// any Major or OutOfService defect → Fail.
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
    public string Unit { get; private set; }
    public InspectionType Type { get; private set; }
    public string DriverName { get; private set; }
    public InspectionSource Source { get; private set; }
    public string? EnteredBy { get; private set; }

    /// <summary>Trip number for manifest-materialized inspections; null for dispatcher paper-backup entries.</summary>
    public string? TripNumber { get; private set; }

    /// <summary>Manifest id for manifest-materialized inspections; null for dispatcher paper-backup entries.</summary>
    public Guid? ManifestId { get; private set; }

    public DateTimeOffset PerformedAt { get; private set; }
    public int? OdometerKm { get; private set; }
    public InspectionResult Result { get; private set; }
    public List<InspectionChecklistItem> ChecklistItems { get; private set; } = [];
    public List<InspectionDefect> Defects { get; private set; } = [];

    /// <summary>The work order this inspection's defects generated, if any (set on WO creation).</summary>
    public Guid? GeneratedWorkOrderId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static VehicleInspection Create(
        Guid tenantId,
        string unit,
        InspectionType type,
        string driverName,
        InspectionSource source,
        string? enteredBy,
        string tripNumber,
        Guid manifestId,
        DateTimeOffset performedAt,
        int? odometerKm,
        IReadOnlyList<InspectionChecklistItem> checklistItems,
        IReadOnlyList<InspectionDefect> defects) => new()
    {
        TenantId = tenantId,
        Unit = unit.Trim(),
        Type = type,
        DriverName = driverName.Trim(),
        Source = source,
        EnteredBy = string.IsNullOrWhiteSpace(enteredBy) ? null : enteredBy.Trim(),
        TripNumber = tripNumber.Trim(),
        ManifestId = manifestId,
        PerformedAt = performedAt,
        OdometerKm = odometerKm,
        Result = DeriveResult(defects),
        ChecklistItems = [.. checklistItems],
        Defects = [.. defects],
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };

    /// <summary>
    /// Dispatcher paper-backup entry — the office fallback when the Driver App failed in the
    /// field. No manifest or trip; source is <see cref="InspectionSource.PaperTranscription"/>.
    /// </summary>
    public static Result<VehicleInspection> EnterByDispatcher(
        Guid tenantId,
        string unit,
        InspectionType type,
        string driverName,
        string? enteredBy,
        DateTimeOffset performedAt,
        int? odometerKm,
        IReadOnlyList<InspectionChecklistItem> checklistItems,
        IReadOnlyList<InspectionDefect> defects)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.UnitRequired);
        }

        if (string.IsNullOrWhiteSpace(driverName))
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.DriverRequired);
        }

        var inspection = new VehicleInspection
        {
            TenantId = tenantId,
            Unit = unit.Trim(),
            Type = type,
            DriverName = driverName.Trim(),
            Source = InspectionSource.PaperTranscription,
            EnteredBy = string.IsNullOrWhiteSpace(enteredBy) ? "Dispatch" : enteredBy.Trim(),
            TripNumber = null,
            ManifestId = null,
            PerformedAt = performedAt,
            OdometerKm = odometerKm,
            Result = DeriveResult(defects),
            ChecklistItems = [.. checklistItems],
            Defects = [.. defects],
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        return NorthernLink.Shared.Kernel.Result.Success(inspection);
    }

    /// <summary>Links the work order generated from this inspection's defects.</summary>
    public void LinkWorkOrder(Guid workOrderId) => GeneratedWorkOrderId = workOrderId;

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
}
