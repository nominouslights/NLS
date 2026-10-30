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

    /// <summary>Upper bound on <see cref="Location"/>; matches the column's varchar(200).</summary>
    public const int LocationMaxLength = 200;

    /// <summary>
    /// Where the inspection was performed — the urban municipality, or a description of the
    /// highway location — as Manitoba's Commercial Vehicle Trip Inspection Regulation
    /// (M.R. 95/2008 s.12(1), NSC Standard 13) requires every trip-inspection report to record.
    /// Trimmed; whitespace-only is stored as null. Deliberately OPTIONAL here: records keyed in
    /// from old paper forms legitimately lack it, so the requirement for new submissions is
    /// enforced by the entry UIs, not by the aggregate. Part of the report body, so
    /// <see cref="Amend"/> may change it; <see cref="AcknowledgeAsCarrier"/> never touches it.
    /// </summary>
    public string? Location { get; private set; }

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
        string? certificationStatement = null,
        string? location = null)
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

        var normalizedLocation = Normalize(location);
        if (normalizedLocation is { Length: > LocationMaxLength })
        {
            return NorthernLink.Shared.Kernel.Result.Failure<VehicleInspection>(InspectionErrors.LocationTooLong);
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
            Location = normalizedLocation,
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
    /// Two amends are REFUSED while a defect is on an open work order — its own
    /// <see cref="InspectionDefect.WorkOrderId"/>, or, for an unresolved defect with no own link,
    /// this inspection's legacy <see cref="GeneratedWorkOrderId"/> while
    /// <paramref name="generatedWorkOrderIsOpen"/> (the same hold <see cref="CanAttachDefect"/>
    /// applies; a Completed, Cancelled or missing legacy work order holds nothing, and a resolved
    /// defect is never held by it) — for the same reason removing the inspection is: one that drops that defect's item
    /// (<see cref="InspectionErrors.AttachedDefectCannotBeDropped"/>, naming the item — renaming
    /// it counts as dropping it), and one that moves the report to another vehicle
    /// (<see cref="InspectionErrors.VehicleChangeWithDefectOnActiveWorkOrder"/>). Complete,
    /// cancel, or defer the defect off the work order first. Correcting an attached defect's
    /// severity or note is fine — it stays on its work order.
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
        bool generatedWorkOrderIsOpen,
        string? certificationStatement = null,
        string? location = null)
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

        var normalizedLocation = Normalize(location);
        if (normalizedLocation is { Length: > LocationMaxLength })
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.LocationTooLong);
        }

        // A defect on an open work order is that work order's subject — the same reason
        // removing the inspection is refused. Dropping it would leave the work order's line
        // pointing at nothing (its completion would silently skip it); moving the report to
        // another vehicle would leave the work order on one truck repairing another's defect.
        // Re-stating the defect with a corrected severity/note is fine: only its Item key matters.
        // "On an open work order" is IsHeldByWorkOrder — the same rule CanAttachDefect refuses
        // on, so the legacy generated work order's hold counts here exactly as it does there.
        var incomingItems = defects
            .Select(d => NormalizeItem(d.Item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var droppedAttached = Defects.FirstOrDefault(d =>
            IsHeldByWorkOrder(d, generatedWorkOrderIsOpen) && !incomingItems.Contains(NormalizeItem(d.Item)));
        if (droppedAttached is not null)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(
                InspectionErrors.AttachedDefectCannotBeDropped(droppedAttached.Item));
        }

        if (Defects.Any(d => IsHeldByWorkOrder(d, generatedWorkOrderIsOpen)) && ChangesVehicle(vehicleId, unit))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.VehicleChangeWithDefectOnActiveWorkOrder);
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
        Location = normalizedLocation;
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
    /// Whether an amend to (<paramref name="vehicleId"/>, <paramref name="unit"/>) would put this
    /// report on a different truck. Any change of the hard link counts — including linking a
    /// unit-only record to a vehicle or unlinking one, since this aggregate cannot see whether the
    /// new link names the same truck. A legacy unit-only record (no link either side) compares on
    /// the trimmed unit, ordinally — the same rule work-order creation uses to match it to a
    /// vehicle. For a linked record the unit is display text and may be corrected freely.
    /// </summary>
    private bool ChangesVehicle(Guid? vehicleId, string unit) =>
        VehicleId != vehicleId
        || (VehicleId is null && !string.Equals(Unit.Trim(), unit.Trim(), StringComparison.Ordinal));

    /// <summary>
    /// Flags this inspection for hard removal. Raised just before the repository deletes the row
    /// so the removal is carried across the module boundary (the mapper only ever sees real
    /// domain events; the synthetic aggregate-deleted journal row is not mapped). Read-model
    /// deletion is driven separately by that synthetic journal row.
    /// </summary>
    public void MarkRemoved() => Raise(new VehicleInspectionRemovedDomainEvent(Id, TenantId));

    /// <summary>
    /// LEGACY — new work orders attach individual defects through
    /// <see cref="AssignDefectToWorkOrder"/> and never call this; it stays so
    /// <see cref="GeneratedWorkOrderId"/> keeps its history for work orders created before
    /// per-defect links. Links the work order generated from this inspection's defects. One generation per
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
    ///
    /// This is the MANUAL path: <see cref="DefectResolutionReason.RepairedUnderWorkOrder"/> and
    /// <see cref="DefectResolutionReason.NoFaultFound"/> are reserved for work-order completion
    /// (<see cref="ResolveDefectUnderWorkOrder"/>) and fail with
    /// <see cref="InspectionErrors.ResolutionReasonReservedForWorkOrder"/>.
    /// </summary>
    public Result ResolveDefect(
        string item,
        DefectResolutionReason reason,
        string? note,
        string resolvedBy,
        DateTimeOffset atUtc)
    {
        if (reason is DefectResolutionReason.RepairedUnderWorkOrder or DefectResolutionReason.NoFaultFound)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.ResolutionReasonReservedForWorkOrder);
        }

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

    /// <summary>The defect addressed by <paramref name="item"/> (trimmed, case-insensitive), or null.</summary>
    public InspectionDefect? FindDefect(string? item)
    {
        var index = IndexOfDefect(item);
        return index < 0 ? null : Defects[index];
    }

    /// <summary>True while any defect on this inspection is attached to an active work order.</summary>
    public bool HasDefectOnActiveWorkOrder => Defects.Any(d => d.WorkOrderId is not null);

    /// <summary>
    /// THE rule for whether the defect addressed by <paramref name="item"/> is free to go on a
    /// new work order — every attach path (explicit defects, the whole-inspection form, and
    /// <see cref="AssignDefectToWorkOrder"/> itself) asks this, so they cannot disagree.
    ///
    /// A defect is NOT free when it is unknown (<see cref="InspectionErrors.DefectNotFound"/>),
    /// resolved (<see cref="InspectionErrors.DefectAlreadyResolved"/>), on a per-defect work order
    /// of its own (<see cref="InspectionDefect.WorkOrderId"/>), or — the legacy case — still held
    /// by this inspection's <see cref="GeneratedWorkOrderId"/>: a work order created before
    /// per-defect links claims every unresolved defect with no own link, for as long as it is open,
    /// because its completion (the #106 path) is what resolves them. Both of the last two fail
    /// with <see cref="InspectionErrors.DefectAlreadyOnWorkOrder"/>.
    ///
    /// <paramref name="generatedWorkOrderIsOpen"/> is whether <see cref="GeneratedWorkOrderId"/>
    /// names a work order that exists and is neither Completed nor Cancelled — a cross-aggregate
    /// fact, so the caller loads it. It is ignored when there is no generated work order.
    /// </summary>
    public Result CanAttachDefect(string? item, bool generatedWorkOrderIsOpen)
    {
        var defect = FindDefect(item);
        if (defect is null)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DefectNotFound);
        }

        if (defect.IsResolved)
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DefectAlreadyResolved);
        }

        if (IsHeldByWorkOrder(defect, generatedWorkOrderIsOpen))
        {
            return NorthernLink.Shared.Kernel.Result.Failure(InspectionErrors.DefectAlreadyOnWorkOrder);
        }

        return NorthernLink.Shared.Kernel.Result.Success();
    }

    /// <summary>
    /// Every defect that <see cref="CanAttachDefect"/> would accept, in list order — the
    /// whole-inspection form's attach set. Empty while an open legacy generated work order holds
    /// the inspection.
    /// </summary>
    public IReadOnlyList<InspectionDefect> AttachableDefects(bool generatedWorkOrderIsOpen) =>
        [.. Defects.Where(d => CanAttachDefect(d.Item, generatedWorkOrderIsOpen).IsSuccess)];

    /// <summary>
    /// THE "held" rule, shared by <see cref="CanAttachDefect"/> and <see cref="Amend"/> so the two
    /// can never disagree: a defect is held by an open work order when it carries its own
    /// <see cref="InspectionDefect.WorkOrderId"/> (resolved by hand or not — the work order still
    /// lists it until it completes or lets go), or when it is unresolved, has no own link, and
    /// <see cref="GeneratedWorkOrderId"/> names a work order that is still open.
    ///
    /// A RESOLVED defect with no own link is not held by the legacy work order: that work
    /// order's completion (<see cref="ResolveDefectsForWorkOrder"/>) skips resolved defects, so
    /// nothing it will do depends on the row still being there.
    /// </summary>
    private bool IsHeldByWorkOrder(InspectionDefect defect, bool generatedWorkOrderIsOpen) =>
        defect.WorkOrderId is not null || IsHeldByGeneratedWorkOrder(defect, generatedWorkOrderIsOpen);

    private bool IsHeldByGeneratedWorkOrder(InspectionDefect defect, bool generatedWorkOrderIsOpen) =>
        generatedWorkOrderIsOpen
        && GeneratedWorkOrderId is not null
        && !defect.IsResolved
        && defect.WorkOrderId is null;

    /// <summary>
    /// Attaches one open defect to the active work order <paramref name="workOrderId"/>. A defect
    /// is on at most one active work order: a second attach fails with
    /// <see cref="InspectionErrors.DefectAlreadyOnWorkOrder"/> (even for the same work order —
    /// attaching happens once, at creation), a defect still held by an open legacy generated work
    /// order fails the same way, and a resolved defect cannot be attached at all. See
    /// <see cref="CanAttachDefect"/> for the rule and <paramref name="generatedWorkOrderIsOpen"/>.
    /// </summary>
    public Result AssignDefectToWorkOrder(string item, Guid workOrderId, bool generatedWorkOrderIsOpen)
    {
        var attachable = CanAttachDefect(item, generatedWorkOrderIsOpen);
        if (attachable.IsFailure)
        {
            return attachable;
        }

        var index = IndexOfDefect(item);
        var defect = Defects[index];
        ReplaceDefect(index, defect with { WorkOrderId = workOrderId });
        Raise(new VehicleInspectionDefectWorkOrderChangedDomainEvent(Id, TenantId, defect.Item, workOrderId));
        return NorthernLink.Shared.Kernel.Result.Success();
    }

    /// <summary>
    /// Detaches one defect from <paramref name="workOrderId"/> — the work order deferred it or
    /// was cancelled. The defect stays exactly as resolved or open as it was; it is simply free
    /// for another work order. A no-op (no change, no event) when the item is gone or the defect
    /// is not on that work order.
    /// </summary>
    public void ReleaseDefectFromWorkOrder(string item, Guid workOrderId)
    {
        var index = IndexOfDefect(item);
        if (index < 0 || Defects[index].WorkOrderId != workOrderId)
        {
            return;
        }

        var defect = Defects[index];
        ReplaceDefect(index, defect with { WorkOrderId = null });
        Raise(new VehicleInspectionDefectWorkOrderChangedDomainEvent(Id, TenantId, defect.Item, null));
    }

    /// <summary>
    /// Resolves one defect as the outcome of completing <paramref name="workOrderId"/> (reason
    /// <see cref="DefectResolutionReason.RepairedUnderWorkOrder"/> or
    /// <see cref="DefectResolutionReason.NoFaultFound"/>), attributing it to that work order and
    /// detaching it. Only a defect actually on that work order is touched — a missing item or a
    /// defect on another (or no) work order is a no-op.
    ///
    /// A defect that was already resolved by hand keeps its own resolution stamp (resolution is
    /// final); it is only detached from the work order.
    /// </summary>
    public void ResolveDefectUnderWorkOrder(
        string item,
        Guid workOrderId,
        DefectResolutionReason reason,
        string? note,
        string resolvedBy,
        DateTimeOffset atUtc)
    {
        var index = IndexOfDefect(item);
        if (index < 0 || Defects[index].WorkOrderId != workOrderId)
        {
            return;
        }

        var defect = Defects[index];
        if (defect.IsResolved)
        {
            ReplaceDefect(index, defect with { WorkOrderId = null });
            Raise(new VehicleInspectionDefectWorkOrderChangedDomainEvent(Id, TenantId, defect.Item, null));
            return;
        }

        ReplaceDefect(index, defect with
        {
            WorkOrderId = null,
            ResolutionReason = reason,
            ResolutionNote = Normalize(note),
            ResolvedBy = Normalize(resolvedBy),
            ResolvedAtUtc = atUtc,
            ResolvedByWorkOrderId = workOrderId,
        });
        Raise(new VehicleInspectionDefectsResolvedDomainEvent(Id, TenantId));
    }

    private int IndexOfDefect(string? item)
    {
        var key = NormalizeItem(item);
        return Defects.FindIndex(d => NormalizeItem(d.Item).Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private void ReplaceDefect(int index, InspectionDefect replacement)
    {
        var updated = new List<InspectionDefect>(Defects);
        updated[index] = replacement;
        Defects = updated;
    }

    /// <summary>
    /// LEGACY PATH ONLY — work orders with no defect lines (created before per-defect links, or
    /// manually). Stamps the still-unresolved defects on this inspection as repaired under
    /// <paramref name="workOrderId"/> — only those whose Item is in <paramref name="items"/>
    /// (trimmed, case-insensitive), or every open one when <paramref name="items"/> is null.
    /// Called when that work order completes — creating or starting one shows as "repair
    /// underway" but leaves the defects open, because only completion asserts a mechanic
    /// actually touched the truck. <see cref="DefectItemsCoveredBy"/> decides the item set.
    ///
    /// Idempotent by construction: already-resolved defects are skipped, and a run that changes
    /// nothing mutates nothing and raises no event (which matters — the audit pipeline rejects an
    /// eventless write, so a no-op must also be a no-write).
    /// </summary>
    public void ResolveDefectsForWorkOrder(
        Guid workOrderId,
        string resolvedBy,
        DateTimeOffset atUtc,
        IReadOnlyCollection<string>? items = null)
    {
        var wanted = items?
            .Select(NormalizeItem)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A defect attached to an active per-defect work order belongs to THAT work order: a
        // legacy completion must never sweep it, or it would end up resolved while still
        // attached (breaking "WorkOrderId set ⇔ on an open work order").
        bool InScope(InspectionDefect d) =>
            !d.IsResolved
            && d.WorkOrderId is null
            && (wanted is null || wanted.Contains(NormalizeItem(d.Item)));

        if (!Defects.Any(InScope))
        {
            return;
        }

        Defects = [.. Defects.Select(d => !InScope(d)
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
    /// Which of this inspection's defects (resolved or not) a work order's line items cover —
    /// the interim stand-in for a real per-defect link. The Dispatcher builds a DVIR work order's
    /// line items one per defect as <c>"{item} — {severity}[: {note}]"</c>
    /// (<c>Dispatcher/lib/inspectionWorkOrder.ts</c> <c>prefillFromInspection</c>), so a line item
    /// covers a defect when, compared case-insensitively with whitespace trimmed and collapsed, it
    /// equals the defect's Item or starts with the Item followed by a separator (<c>—</c>,
    /// <c>–</c>, <c>-</c> or <c>:</c>, spaces optional). When several Items match one line (one Item
    /// a prefix of another) only the longest counts.
    ///
    /// Returns null when no line item matches any defect — a manual work order or an unknown
    /// format — which <see cref="ResolveDefectsForWorkOrder"/> reads as "resolve every open
    /// defect", the pre-existing behaviour, so legacy work orders don't regress. Already-resolved
    /// defects are matched too: a work order raised for one defect that was then cleared by hand
    /// must still resolve nothing else, not fall back to everything.
    /// </summary>
    public IReadOnlyCollection<string>? DefectItemsCoveredBy(IEnumerable<string> lineItems)
    {
        var defectItems = Defects
            .Select(d => (Item: d.Item, Key: CollapseWhitespace(d.Item)))
            .Where(d => d.Key.Length > 0)
            .OrderByDescending(d => d.Key.Length)
            .ToList();

        var covered = new List<string>();
        foreach (var line in lineItems.Select(CollapseWhitespace))
        {
            var match = defectItems.FirstOrDefault(d => LineItemNamesDefect(line, d.Key));
            if (match.Key is not null)
            {
                covered.Add(match.Item);
            }
        }

        return covered.Count == 0 ? null : covered;
    }

    private static bool LineItemNamesDefect(string line, string item)
    {
        if (!line.StartsWith(item, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = line.AsSpan(item.Length).TrimStart();
        return rest.IsEmpty || rest[0] is '—' or '–' or '-' or ':';
    }

    private static string CollapseWhitespace(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
    /// clearing it is meaningless) — except a defect on an open work order, which
    /// <see cref="Amend"/> refuses to drop before this runs. A defect the amendment adds starts
    /// unresolved.
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
            .Where(d => d.IsResolved || d.WorkOrderId is not null)
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
                // The active work-order link is maintenance history too: an amend that keeps the
                // item keeps it on its work order.
                WorkOrderId = prior.WorkOrderId,
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

    /// <summary>
    /// Nulls the five resolution fields and the active work-order link — none of them can ever be
    /// set from the wire; <c>RecurrenceOfInspectionId</c> is left alone.
    /// </summary>
    private static InspectionDefect StripResolution(InspectionDefect defect) => defect with
    {
        ResolutionReason = null,
        ResolutionNote = null,
        ResolvedBy = null,
        ResolvedAtUtc = null,
        ResolvedByWorkOrderId = null,
        WorkOrderId = null,
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
