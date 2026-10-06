import { groupResult, type ChecklistRow } from "./ChecklistGroupEditor";
import {
  NL_PTI_01,
  itemsFor,
  type InspectionFormMode,
  type InspectionSubGroup,
} from "@/lib/inspectionForm";
import type {
  ChecklistItemStateWire,
  DefectSeverityWire,
  InspectionInput,
  VehicleInspection,
} from "@/lib/api/maintenance";

// Shared NL-PTI-01 row plumbing for the two inspection-entry modals (trip-scoped
// TripInspectionModal and vehicle-scoped InspectionEntryModal). Both build their
// editable rows from `itemsFor()` and send the same wire shape, so the mapping
// lives here once rather than twice.

/** Every wire key NL-PTI-01 knows, across all units and both halves of the form. */
const CATALOGUE_KEYS: ReadonlySet<string> = new Set(
  NL_PTI_01.flatMap((group) => group.items.map((item) => item.key)),
);

/** Fresh rows for this unit and mode, in form order — UNANSWERED unless `initial`
 *  says otherwise (see `TRIP_ROW_START`). */
export function rowsFor(
  unit: string | null,
  mode: InspectionFormMode,
  initial: ChecklistItemStateWire | null = null,
): ChecklistRow[] {
  return itemsFor(unit, mode).flatMap((group) =>
    group.items.map((item) => ({
      groupKey: group.key,
      itemKey: item.key,
      label: item.label,
      state: initial,
      severity: "Minor" as const,
      note: "",
    })),
  );
}

/**
 * The answer a NEW trip inspection's rows start with: OK, in both modes, by the
 * owner's decision (2026-10). The dispatcher transcribes an inspection the driver
 * already did, and marking the few defects is faster than ticking 53–64 OKs (the
 * post-trip's six Close-Out rows follow suit). Never applied to a saved record —
 * rows the record does not carry still come back unanswered (`rowsFromRecord`).
 */
export const TRIP_ROW_START: ChecklistItemStateWire = "Ok";

/**
 * Rows for a newly chosen unit, carrying over every answer already given to a row
 * both forms share. Rows the new unit does not have are DROPPED, never kept hidden:
 * a hidden row would either block the save as unanswered or — once it starts at
 * OK — be sent as "OK" for equipment the vehicle does not have.
 */
export function rowsForUnitChange(
  prev: ChecklistRow[],
  unit: string | null,
  mode: InspectionFormMode,
): ChecklistRow[] {
  const byKey = new Map(prev.map((r) => [r.itemKey, r]));
  return rowsFor(unit, mode, TRIP_ROW_START).map((row) => byKey.get(row.itemKey) ?? row);
}

/**
 * Is this saved record written against a form revision NL-PTI-01 replaced?
 *
 * Every item string changed when NL-TM-01 became NL-PTI-01 (28 old rows → 80 new),
 * and rev 3 retired 23 more NL-PTI-01 keys into combined rows (`RETIRED_KEYS`). The
 * rows are addressed by that string. A record holding even one item outside today's
 * catalogue cannot be rebuilt into editable rows: the lookup finds nothing, that row
 * is lost, and saving would overwrite what the old inspection actually said. The
 * caller's job is to render it read-only instead — never to guess a correspondence.
 *
 * Deliberately tested against the CURRENT catalogue only, not `RETIRED_KEYS`: a
 * retired-but-known key ("Engine oil") is treated exactly like a completely unknown
 * one. `RETIRED_KEYS` names the row that now covers the check for display, but
 * folding "Engine oil: OK" into "Engine fluid levels: OK" would assert three checks
 * the driver never answered.
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

// ---- post-trip "new defects" (rev 4) ----------------------------------------------

/** One defect found AFTER the pre-trip, filed against the pre-trip row it concerns.
 *  `itemKey` is "" until the dispatcher picks the item. */
export interface NewDefect {
  itemKey: string;
  severity: DefectSeverityWire;
  note: string;
}

export interface NewDefectOption {
  key: string;
  label: string;
}

/**
 * What a new post-trip defect may be filed against: every pre-trip row this unit has,
 * minus the rows the trip's pre-trip already reported a defect on — a defect the
 * pre-trip recorded is not new. Filed against the real item key (never free text) so
 * work orders and recurrence tracking still match it.
 */
export function newDefectOptions(unit: string | null, preTrip: VehicleInspection | null): NewDefectOption[] {
  const alreadyReported = new Set((preTrip?.defects ?? []).map((d) => d.item));
  return itemsFor(unit, "PreTrip").flatMap((group) =>
    group.items
      .filter((item) => !alreadyReported.has(item.key))
      .map((item) => ({ key: item.key, label: `${group.title} · ${item.label}` })),
  );
}

/** A saved post-trip's new defects: every defect not filed against one of its
 *  checklist (Close-Out) rows. */
export function newDefectsFromRecord(existing: VehicleInspection, rows: ChecklistRow[]): NewDefect[] {
  const rowKeys = new Set(rows.map((r) => r.itemKey));
  return existing.defects
    .filter((d) => !rowKeys.has(d.item))
    .map((d) => ({ itemKey: d.item, severity: d.severity, note: d.note ?? "" }));
}

export function newDefectsWire(defects: NewDefect[]): InspectionInput["defects"] {
  return defects.map((d) => ({ item: d.itemKey, severity: d.severity, note: d.note.trim() || null }));
}

/** The first problem that blocks saving these new defects, or null. */
export function newDefectsProblem(defects: NewDefect[]): string | null {
  if (defects.some((d) => !d.itemKey)) return "Pick the item each new defect concerns.";
  const noNote = defects.find((d) => !d.note.trim());
  if (noNote) return "New defects need a note — describe what was found.";
  return null;
}

/** The inspection result over checklist rows AND new defects — same rule as
 *  `groupResult`: any Major fails, any other defect is "Pass with defects". */
export function inspectionResult(rows: ChecklistRow[], defects: NewDefect[]): ReturnType<typeof groupResult> {
  const fromRows = groupResult(rows);
  if (fromRows === "Fail" || defects.some((d) => d.severity === "Major" || d.severity === "OutOfService")) return "Fail";
  if (fromRows === "Pass with defects" || defects.length > 0) return "Pass with defects";
  return "Pass";
}
