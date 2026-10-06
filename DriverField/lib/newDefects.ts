// ---------------------------------------------------------------------------
// The post-trip's "New defects since the pre-trip" (NL-PTI-01 rev 4). PURE and React-free, so
// Vitest exercises it without jsdom — the same reason inspectionSteps.ts lives in lib/.
//
// WHAT CHANGED IN REV 4, and why this file exists: the post-trip used to re-ask 21 "can change
// while driving" rows plus one "Defects noticed while driving" row. By the owner's decision
// (2026-10) it now asks one question — "Any defect found after the pre-trip?" — and, on Yes, a
// list of NEW defects, each filed against the PRE-trip row it concerns ("Tire condition",
// "Parking brake" …) so work orders and recurrence tracking still match on the item. The six
// Close-Out rows stay ordinary checklist rows. See the header of lib/inspectionForm.ts (a copy).
//
// MIRRORS, deliberately, and does NOT import across apps:
//   Dispatcher/components/inspection/checklistRows.ts — newDefectOptions, newDefectsProblem,
//   newDefectsWire, inspectionResult. The semantics are the same; the two tablet deltas are
//   named where they occur:
//     • the No/Yes answer starts UNANSWERED here (`null`), where the console starts at No — the
//       console transcribes a report the driver already made, the tablet IS the driver's report,
//       and a default here would be an answer the driver never gave;
//     • a new defect's severity starts ungraded (`null`) here, where the console starts at Minor
//       — for the same reason, and matching how a checklist defect works on this app.
//
// WHAT THE BACKEND WILL REJECT, and so must never be sent: two defects on one item in one
// inspection (InspectionErrors.DuplicateDefectItem). The picker hides items already chosen, and
// newDefectsProblem refuses a duplicate anyway.
//
// NOTHING NEW ON THE WIRE. A new defect is an ordinary DefectInput `{ item, severity, note }`.
// The No/Yes answer itself has no wire field — "No" is an empty new-defect list — so it is not
// sent; inventing a field for it would be inventing a contract.
// ---------------------------------------------------------------------------

import { itemsFor, type ItemCategory } from "./inspectionForm";
import { severityToWire, type DefectSeverityWire } from "./inspectionGate";
import type { DefectSeverity, NewDefectDraft } from "./types";

export interface NewDefectOption {
  /** The pre-trip row's catalogue KEY — the value sent as DefectInput.Item. */
  key: string;
  /** The row's label as printed on the form. */
  label: string;
  /** The sub-group title, for the picker's grouping ("Tires & Wheels"). */
  group: string;
  /** The form's default classification for the row. DISPLAY TEXT — nothing computes from it. */
  category: ItemCategory;
  /** The form's own qualifier, verbatim ("Major if leaking"), where the row has one. */
  categoryNote?: string;
}

/**
 * What a new post-trip defect may be filed against: every PRE-trip row this unit has, in form
 * order, minus the rows the trip's pre-trip already reported a defect on — a defect the pre-trip
 * recorded is not new. Mirrors `newDefectOptions` in Dispatcher's checklistRows.ts.
 *
 * Narrowed by unit exactly as itemsFor() narrows (an unknown unit gets the superset). Items the
 * driver has already picked on OTHER rows of this report are excluded per row by the picker
 * (see `optionsForRow`), not here.
 */
export function newDefectOptions(
  unit: string | null,
  alreadyReported: ReadonlySet<string>,
): NewDefectOption[] {
  return itemsFor(unit, "PreTrip").flatMap((group) =>
    group.items
      .filter((item) => !alreadyReported.has(item.key))
      .map((item) => ({
        key: item.key,
        label: item.label,
        group: group.title,
        category: item.category,
        categoryNote: item.categoryNote,
      })),
  );
}

/**
 * The options one row's picker offers: `options` minus every item picked on a DIFFERENT row of
 * this report (the backend rejects a second defect on one item — DuplicateDefectItem). The
 * row's own pick stays, so its picker still shows what it holds.
 */
export function optionsForRow(
  options: NewDefectOption[],
  defects: NewDefectDraft[],
  index: number,
): NewDefectOption[] {
  const taken = new Set(defects.filter((_, i) => i !== index).map((d) => d.itemKey));
  return options.filter((o) => !taken.has(o.key));
}

/**
 * The first thing that blocks certifying the new-defects answer, or null when it is complete.
 * Mirrors `newDefectsProblem` (plus the empty-Yes check the console makes inline in
 * TripInspectionModal), with the two tablet deltas from this file's header.
 *
 * `options` is newDefectOptions() for this unit and pre-trip: an item outside it is off this
 * unit's form or was already reported, and is named as a problem rather than silently dropped.
 */
export function newDefectsProblem(
  found: boolean | null,
  defects: NewDefectDraft[],
  options: NewDefectOption[],
): string | null {
  if (found === null) {
    return "Answer “Any defect found after the pre-trip?” — No, or Yes and list them.";
  }
  if (!found) return null;
  if (defects.length === 0) {
    return "Add the new defect, or answer No to “Any defect found after the pre-trip?”.";
  }
  if (defects.some((d) => d.itemKey === "")) return "Pick the item each new defect concerns.";

  const allowed = new Set(options.map((o) => o.key));
  const offList = defects.find((d) => !allowed.has(d.itemKey));
  if (offList) {
    return (
      `“${offList.itemKey}” is not a new-defect item for this vehicle — it is not on this ` +
      "unit's pre-trip, or the pre-trip already reported it. Pick another item or remove it."
    );
  }

  const keys = defects.map((d) => d.itemKey);
  if (new Set(keys).size !== keys.length) {
    return "Each item can carry only one new defect — remove the duplicate.";
  }
  if (defects.some((d) => d.severity === null)) return "Grade each new defect Minor or Major.";
  if (defects.some((d) => d.note.trim() === "")) {
    return "New defects need a note — describe what was found.";
  }
  return null;
}

/**
 * The new defects that COUNT: all of them on Yes, none on No or unanswered. The one place the
 * "kept on No but never sent" rule from InspectionDraft.newDefects is applied, so the result,
 * the review, the payload and the local certification all agree.
 */
export function reportedNewDefects(
  found: boolean | null,
  defects: NewDefectDraft[],
): NewDefectDraft[] {
  return found === true ? defects : [];
}

/** Their severities, for deriveResult — an ungraded one contributes nothing until graded. */
export function newDefectSeverities(defects: NewDefectDraft[]): DefectSeverity[] {
  return defects.flatMap((d): DefectSeverity[] => (d.severity === null ? [] : [d.severity]));
}

/**
 * The wire entries — ordinary DefectInput rows against the real item key. Mirrors
 * `newDefectsWire`. The note is REQUIRED for a new defect (newDefectsProblem), so in practice it
 * is never null here; trimmed, blank → null, matching the checklist defects' rule.
 *
 * Only call once newDefectsProblem() has answered null — an ungraded entry has no wire value.
 */
export function newDefectsWire(
  defects: NewDefectDraft[],
): { item: string; severity: DefectSeverityWire; note: string | null }[] {
  return defects
    .filter((d): d is NewDefectDraft & { severity: NonNullable<NewDefectDraft["severity"]> } =>
      d.severity !== null,
    )
    .map((d) => ({
      item: d.itemKey,
      severity: severityToWire(d.severity),
      note: d.note.trim() === "" ? null : d.note.trim(),
    }));
}
