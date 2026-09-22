import type { ChecklistRow } from "./ChecklistGroupEditor";
import {
  NL_PTI_01,
  itemsFor,
  type InspectionFormMode,
  type InspectionSubGroup,
} from "@/lib/inspectionForm";
import type { ChecklistItemStateWire, InspectionInput, VehicleInspection } from "@/lib/api/maintenance";

// Shared NL-PTI-01 row plumbing for the two inspection-entry modals (trip-scoped
// TripInspectionModal and vehicle-scoped InspectionEntryModal). Both build their
// editable rows from `itemsFor()` and send the same wire shape, so the mapping
// lives here once rather than twice.

/** Every wire key NL-PTI-01 knows, across all units and both halves of the form. */
const CATALOGUE_KEYS: ReadonlySet<string> = new Set(
  NL_PTI_01.flatMap((group) => group.items.map((item) => item.key)),
);

/** Fresh, UNANSWERED rows for this unit and mode, in form order. */
export function rowsFor(unit: string | null, mode: InspectionFormMode): ChecklistRow[] {
  return itemsFor(unit, mode).flatMap((group) =>
    group.items.map((item) => ({
      groupKey: group.key,
      itemKey: item.key,
      label: item.label,
      state: null,
      severity: "Minor" as const,
      note: "",
    })),
  );
}

/**
 * Is this saved record written against a form revision NL-PTI-01 replaced?
 *
 * Every item string changed when NL-TM-01 became NL-PTI-01 (28 old rows → 80 new),
 * and the rows are addressed by that string. A record holding even one unknown item
 * cannot be rebuilt into editable rows: the lookup finds nothing, every row renders
 * unanswered, and saving would overwrite what the old inspection actually said. The
 * caller's job is to render it read-only instead — never to guess a correspondence.
 */
export function isRetiredFormRecord(existing: VehicleInspection): boolean {
  return existing.checklist.some((c) => !CATALOGUE_KEYS.has(c.item));
}

/**
 * Editable rows for an existing inspection, for the CURRENT catalogue.
 *
 * Only safe once `isRetiredFormRecord` has answered false — see its doc comment.
 * A row the record simply does not carry comes back unanswered rather than OK, so
 * a partially-filled record cannot be re-saved as a complete one by accident.
 */
export function rowsFromRecord(
  existing: VehicleInspection,
  unit: string | null,
  mode: InspectionFormMode,
): ChecklistRow[] {
  const savedByItem = new Map(existing.checklist.map((c) => [c.item, c]));
  const defectByItem = new Map(existing.defects.map((d) => [d.item, d]));

  return rowsFor(unit, mode).map((row) => {
    const saved = savedByItem.get(row.itemKey);
    const defect = defectByItem.get(row.itemKey);
    // A record written before the tri-state existed has no `state` (or the backend
    // derived one from `passed`); a defect filed against the item settles it either way.
    const state: ChecklistItemStateWire | null =
      defect != null ? "Defect" : (saved?.state ?? (saved ? (saved.passed ? "Ok" : "Defect") : null));
    return {
      ...row,
      state,
      severity: defect?.severity ?? row.severity,
      // The note used to live only on the defect; it is a checklist field now.
      note: saved?.note ?? defect?.note ?? "",
    };
  });
}

/**
 * Recorded items that the rows for this unit and mode do NOT include — the same
 * data-loss shape as a retired form, from a different cause: a report entered for
 * NL-02 and re-opened after the trip's vehicle became NL-01 answers rows the
 * narrowed form no longer shows, and rebuilding would drop them. The caller opens
 * such a record read-only rather than silently shedding answers.
 *
 * Only meaningful once `isRetiredFormRecord` has answered false; a retired record
 * is outside the form by definition.
 */
export function itemsOutsideForm(existing: VehicleInspection, rows: ChecklistRow[]): string[] {
  const rendered = new Set(rows.map((r) => r.itemKey));
  return existing.checklist.filter((c) => !rendered.has(c.item)).map((c) => c.item);
}

/** Sub-group keys that end an area — the legend prints once per area, not per group. */
export function areaLegendKeys(groups: InspectionSubGroup[]): ReadonlySet<string> {
  const lastOfArea = new Map<string, string>();
  for (const group of groups) lastOfArea.set(group.area, group.key);
  return new Set(lastOfArea.values());
}

/** The wire checklist — EVERY row, in both modes. `passed` is derived the same way
 *  the backend's own NormalizeChecklist derives it, so an N-A never reads as a
 *  failure. An unanswered row sends `state: null`; callers block submit before that
 *  can happen (see `unansweredCount`). */
export function checklistWire(rows: ChecklistRow[]): InspectionInput["checklist"] {
  return rows.map((r) => ({
    group: r.groupKey || null,
    item: r.itemKey,
    passed: r.state !== "Defect",
    state: r.state,
    note: r.note.trim() || null,
  }));
}

/** The wire defects — one per `Defect` row, in both modes. */
export function defectsWire(rows: ChecklistRow[]): InspectionInput["defects"] {
  return rows
    .filter((r) => r.state === "Defect")
    .map((r) => ({
      item: r.itemKey,
      severity: r.severity,
      note: r.note.trim() || null,
      ...(r.recurrenceOfInspectionId ? { recurrenceOfInspectionId: r.recurrenceOfInspectionId } : {}),
    }));
}
