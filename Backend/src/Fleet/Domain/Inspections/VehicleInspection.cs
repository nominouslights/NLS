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

    /// <summary>
    /// LEGACY. The manifest §10 tick-boxes, positional and meaningless without the paper form
    /// beside them. Kept because existing rows carry it and nothing reads it back except the
    /// screen that wrote it; new work records the attestation as
    /// <see cref="CertificationStatement"/> instead, which says what was actually signed.
    /// </summary>
    public List<bool> Attestations { get; private set; } = [];

    /// <summary>
    /// The exact sentence the driver certified, stored verbatim rather than by reference, so a
    /// printed report can say WHICH attestation was made. Re-wording the form later changes what
    /// new inspections store and rewrites nothing that was already signed.
    /// </summary>
    public string? CertificationStatement { get; private set; }

    public string? DriverSignatureName { get; private set; }
    public DateTimeOffset? CertifiedAt { get; private set; }
    public bool FuelAdded { get; private set; }
    public decimal? FuelLitres { get; private set; }
    public decimal? FuelCostCad { get; private set; }

    // Carrier acknowledgement (NL-PTI-01). One signature per REPORT, not per defect: the paper
    // form has a single carrier line, and what it attests is that this report — with whatever
    // Major/OutOfService defect put it into Fail — was escalated and seen. Set only through
    // AcknowledgeAsCarrier; deliberately unreachable from Enter and Amend.
    public string? CarrierAcknowledgedBy { get; private set; }
    public DateTimeOffset? CarrierAcknowledgedAtUtc { get; private set; }
    public string? CarrierAcknowledgementNote { get; private set; }

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
    ///
    /// <paramref name="certificationStatement"/> is the sentence that was signed, kept verbatim.
    /// The carrier acknowledgement fields are NOT parameters here and never will be: see
    /// <see cref="AcknowledgeAsCarrier"/>.
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
        decimal? fuelCostCad,
        string? certificationStatement = null)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.UnitRequired);
        }

        if (string.IsNullOrWhiteSpace(driverName))
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.DriverRequired);
        }

        if (HasDuplicateItems(defects))
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.DuplicateDefectItem);
        }

        // A freshly entered defect is never pre-resolved: allowing a resolution stamp in here
        // would make the create path an unaudited back door around ResolveDefect. Recurrence
        // pointers survive — re-reporting a cleared fault is a legitimate thing to say on entry.
        var enteredDefects = defects.Select(StripResolution).ToList();

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
            Result = DeriveResult(enteredDefects),
            ChecklistItems = NormalizeChecklist(checklistItems),
            Defects = enteredDefects,
            Weather = [.. weather],
            TemperatureC = Normalize(temperatureC),
            RoadConditions = [.. roadConditions],
            Visibility = visibility,
            RoadAdvisories = Normalize(roadAdvisories),
            FuelLevel = fuelLevel,
            Issues = [.. issues],
            Attestations = [.. attestations],
            CertificationStatement = Normalize(certificationStatement),
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
    ///
    /// An amend re-states what the DVIR found; it must never re-state the maintenance history,
    /// so <paramref name="defects"/> is MERGED against the current list rather than replacing it
    /// — see <see cref="MergeResolutions"/> for the rule.
    ///
    /// Accepted limitation: if an amendment RENAMES an item ("Brakes" → "Brake lines") the match
    /// fails and the resolution is not carried — the honest reading is that a renamed item is a
    /// different defect. Unlike a silent wipe this is visible: the row simply comes back on
    /// screen as open, where a dispatcher can re-resolve it.
    ///
    /// An amend likewise never touches the carrier acknowledgement — those fields are not
    /// parameters here (see <see cref="AcknowledgeAsCarrier"/>). That has a consequence worth
    /// stating outright, because <see cref="Result"/> IS re-derived here: an inspection that was
    /// acknowledged as a Fail and is then amended down to Pass or PassWithDefects KEEPS its
    /// acknowledgement stamp. It is a historical record of what was escalated at the time, not a
    /// flag describing the report's current state, and erasing it would destroy the only evidence
    /// that the carrier ever saw the defect that has since been downgraded. The stamp is
    /// therefore stale-by-design after such an amend, and a reader must interpret it together
    /// with the amendment rather than against the current <see cref="Result"/>.
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
        decimal? fuelCostCad,
        string? certificationStatement = null)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.UnitRequired);
        }

        if (string.IsNullOrWhiteSpace(driverName))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DriverRequired);
        }

        // Duplicate guard first: Item is the key the merge below matches on, so two defects
        // sharing one would silently collapse into a single resolution stamp.
        if (HasDuplicateItems(defects))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DuplicateDefectItem);
        }

        var merged = MergeResolutions(Defects, defects);

        Source = source;
        EnteredBy = string.IsNullOrWhiteSpace(enteredBy)
            ? source == InspectionSource.Dispatcher ? "Dispatch" : null
            : enteredBy.Trim();
        VehicleId = vehicleId;
        Unit = unit.Trim();
        DriverName = driverName.Trim();
        PerformedAt = performedAt;
        OdometerKm = odometerKm;
        Result = DeriveResult(merged);
        ChecklistItems = NormalizeChecklist(checklistItems);
        Defects = merged;
        Weather = [.. weather];
        TemperatureC = Normalize(temperatureC);
        RoadConditions = [.. roadConditions];
        Visibility = visibility;
        RoadAdvisories = Normalize(roadAdvisories);
        FuelLevel = fuelLevel;
        Issues = [.. issues];
        Attestations = [.. attestations];
        CertificationStatement = Normalize(certificationStatement);
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

    /// <summary>
    /// Signs the NL-PTI-01 carrier acknowledgement line: the carrier's representative confirming
    /// they were shown this report. One signature per report — the paper form has one line, and
    /// what it attests is that the report as a whole was escalated, not that any particular
    /// defect was.
    ///
    /// Only valid while <see cref="Result"/> is <see cref="InspectionResult.Fail"/>. Acknowledging
    /// a clean report is meaningless, and permitting it would destroy the field's value as
    /// evidence that a Major/OutOfService defect was escalated: if everything can be signed,
    /// a signature proves nothing.
    ///
    /// Acknowledgement is FINAL, like <see cref="ResolveDefect"/> and
    /// <see cref="LinkWorkOrder"/> — a second call fails rather than re-stamping, because
    /// overwriting who signed and when is exactly what an evidence field must not allow.
    ///
    /// Deliberately NOT reachable from <see cref="Enter"/> or <see cref="Amend"/>, for the same
    /// reason a defect resolution is not: the entry path comes off the wire, and a caller that
    /// could put the stamp in the body would be self-acknowledging — signing the carrier's line
    /// on the carrier's behalf, with nothing in the record to show it.
    ///
    /// See <see cref="Amend"/> on what happens when an acknowledged report is later amended out
    /// of Fail: the stamp stays.
    /// </summary>
    public Result AcknowledgeAsCarrier(string acknowledgedBy, string? note, DateTimeOffset atUtc)
    {
        if (string.IsNullOrWhiteSpace(acknowledgedBy))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.CarrierAcknowledgerRequired);
        }

        if (Result != InspectionResult.Fail)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.CarrierAcknowledgementNotRequired);
        }

        if (CarrierAcknowledgedAtUtc is not null)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.CarrierAlreadyAcknowledged);
        }

        CarrierAcknowledgedBy = acknowledgedBy.Trim();
        CarrierAcknowledgedAtUtc = atUtc;
        CarrierAcknowledgementNote = Normalize(note);

        Raise(new VehicleInspectionCarrierAcknowledgedDomainEvent(Id, TenantId));
        return NorthernLink.Shared.Kernel.Result.Success();
    }

    /// <summary>
    /// Clears one open defect, addressed by <paramref name="item"/> (trimmed, case-insensitive —
    /// the same matching the amend merge uses). There is no defect id to address it by: a defect
    /// lives inside this aggregate's jsonb, so its key is <c>(InspectionId, Item)</c>.
    ///
    /// Resolution is FINAL. A second resolve fails with
    /// <see cref="InspectionErrors.DefectAlreadyResolved"/> rather than re-stamping, and there is
    /// deliberately no reopen: a fault that comes back is re-reported as a NEW defect on a later
    /// inspection, pointing here via <see cref="InspectionDefect.RecurrenceOfInspectionId"/>, so
    /// the original audit record stays intact.
    /// </summary>
    public Result ResolveDefect(
        string item,
        DefectResolutionReason reason,
        string? note,
        string resolvedBy,
        DateTimeOffset atUtc)
    {
        var key = NormalizeItem(item);
        var index = Defects.FindIndex(d => NormalizeItem(d.Item).Equals(key, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DefectNotFound);
        }

        if (Defects[index].IsResolved)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DefectAlreadyResolved);
        }

        var updated = new List<InspectionDefect>(Defects);
        updated[index] = updated[index] with
        {
            ResolutionReason = reason,
            ResolutionNote = Normalize(note),
            ResolvedBy = Normalize(resolvedBy),
            ResolvedAtUtc = atUtc,
            ResolvedByWorkOrderId = null,
        };

        Defects = updated;

        Raise(new VehicleInspectionDefectsResolvedDomainEvent(Id, TenantId));
        return NorthernLink.Shared.Kernel.Result.Success();
    }

    /// <summary>
    /// Stamps every still-unresolved defect on this inspection as repaired under
    /// <paramref name="workOrderId"/>. Called when that work order completes — creating or
    /// starting one shows as "repair underway" but leaves the defects open, because only
    /// completion asserts a mechanic actually touched the truck.
    ///
    /// Idempotent by construction: already-resolved defects are skipped, and a run that changes
    /// nothing mutates nothing and raises no event (which matters — the audit pipeline rejects an
    /// eventless write, so a no-op must also be a no-write).
    /// </summary>
    public void ResolveDefectsForWorkOrder(Guid workOrderId, string resolvedBy, DateTimeOffset atUtc)
    {
        if (Defects.All(d => d.IsResolved))
        {
            return;
        }

        Defects = [.. Defects.Select(d => d.IsResolved
            ? d
            : d with
            {
                ResolutionReason = DefectResolutionReason.RepairedUnderWorkOrder,
                ResolutionNote = null,
                ResolvedBy = Normalize(resolvedBy),
                ResolvedAtUtc = atUtc,
                ResolvedByWorkOrderId = workOrderId,
            })];

        Raise(new VehicleInspectionDefectsResolvedDomainEvent(Id, TenantId));
    }

    /// <summary>
    /// Carries resolution stamps across an amendment. An amend re-states what the DVIR found; it
    /// must never re-state the maintenance history. Incoming defects come off the wire and carry
    /// no resolution fields, so a naive replace would silently un-resolve everything on the
    /// inspection.
    ///
    /// Rule: match incoming to existing by Item (trimmed, case-insensitive) and copy the
    /// resolution stamp onto the survivor. Item / Severity / Note always take the AMENDED values —
    /// correcting a severity is the whole point of an amend. A defect the amendment drops takes
    /// its resolution with it (the report of the fault is being retracted, so the record of
    /// clearing it is meaningless). A defect the amendment adds starts unresolved.
    ///
    /// Resolution fields on the incoming records are discarded unconditionally, so no caller can
    /// ever mark a defect resolved through this path — ResolveDefect and work-order completion
    /// stay the only two ways in. RecurrenceOfInspectionId is NOT stripped: that one is
    /// legitimately set from the wire when a dispatcher re-reports.
    /// </summary>
    private static List<InspectionDefect> MergeResolutions(
        IReadOnlyList<InspectionDefect> existing,
        IReadOnlyList<InspectionDefect> incoming)
    {
        var priorByItem = existing
            .Where(d => d.IsResolved)
            .GroupBy(d => NormalizeItem(d.Item), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return [.. incoming.Select(d => priorByItem.TryGetValue(NormalizeItem(d.Item), out var prior)
            ? d with
            {
                ResolutionReason = prior.ResolutionReason,
                ResolutionNote = prior.ResolutionNote,
                ResolvedBy = prior.ResolvedBy,
                ResolvedAtUtc = prior.ResolvedAtUtc,
                ResolvedByWorkOrderId = prior.ResolvedByWorkOrderId,
            }
            : StripResolution(d))];
    }

    /// <summary>
    /// The one place the checklist tri-state invariant lives, applied on both write paths so no
    /// call site has to remember it:
    ///
    /// <c>Passed == (State != Defect)</c>
    ///
    /// NOT <c>State == Ok</c>. <see cref="InspectionChecklistItem.Passed"/> is what every consumer
    /// written before NL-PTI-01 reads, and to those consumers it means "this row is not a
    /// defect". An N/A row is not a defect — a bumper-mounted item on a unit that has no bumper
    /// did not fail the inspection — so it must read back as Passed. Deriving from
    /// <c>State == Ok</c> instead would silently turn every N/A answer into a failure on the
    /// older screens and in every report built on that flag.
    ///
    /// A row that supplies no <see cref="InspectionChecklistItem.State"/> is left exactly as it
    /// came: it is the legacy two-value shape, and
    /// <see cref="InspectionChecklistItem.EffectiveState"/> is what reads it back.
    /// </summary>
    private static List<InspectionChecklistItem> NormalizeChecklist(IReadOnlyList<InspectionChecklistItem> items) =>
        [.. items.Select(item => item.State is { } state
            ? item with { Passed = state != ChecklistItemState.Defect }
            : item)];

    /// <summary>Nulls the five resolution fields; <c>RecurrenceOfInspectionId</c> is left alone.</summary>
    private static InspectionDefect StripResolution(InspectionDefect defect) => defect with
    {
        ResolutionReason = null,
        ResolutionNote = null,
        ResolvedBy = null,
        ResolvedAtUtc = null,
        ResolvedByWorkOrderId = null,
    };

    /// <summary><c>Item</c> is the addressing key, so it must be unique within one inspection.</summary>
    private static bool HasDuplicateItems(IReadOnlyList<InspectionDefect> defects) =>
        defects
            .Select(d => NormalizeItem(d.Item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() != defects.Count;

    private static string NormalizeItem(string? item) => item?.Trim() ?? string.Empty;

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
