namespace NorthernLink.Fleet.Domain.Inspections;

/// <summary>
/// One checklist row of an inspection. <see cref="Group"/> is the manifest's §3 group
/// for pre-trip items and null for post-trip items. Persisted as jsonb.
/// </summary>
public sealed record InspectionChecklistItem
{
    public string? Group { get; init; }
    public required string Item { get; init; }

    /// <summary>
    /// Retained deliberately. Rows written before the NL-PTI-01 tri-state landed carry only this
    /// field, and it is still what a legacy consumer reads. On every write from here on it is
    /// DERIVED from <see cref="State"/> — <c>VehicleInspection.NormalizeChecklist</c> owns that
    /// invariant, and it is stated there once rather than at each call site.
    /// </summary>
    public bool Passed { get; init; }

    // Tri-state + per-item note (NL-PTI-01). Absent from every jsonb row written before this
    // change, which is exactly the intended clean slate: the whole existing backlog reads back
    // through EffectiveState below, with no data migration. Both fields ride the same
    // OwnsMany(...).ToJson("checklist_items") mapping, so they change the jsonb PAYLOAD only.
    public ChecklistItemState? State { get; init; }
    public string? Note { get; init; }

    /// <summary>Old rows carry no State — derive it from Passed so the whole backlog stays legible.</summary>
    public ChecklistItemState EffectiveState =>
        State ?? (Passed ? ChecklistItemState.Ok : ChecklistItemState.Defect);
}
