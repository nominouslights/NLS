// ---------------------------------------------------------------------------
// The DVIR wizard's step model. PURE and React-free, so Vitest exercises it without jsdom —
// the same reason eligibility() and hosRemaining() live in lib/ rather than in a screen.
//
// WHY A STEP MODEL AT ALL: the inspection screen used to render every checklist item as one
// continuous scroll, with the legal attestation at the very bottom — the part a driver sees
// least. On a dash-mounted 10-inch tablet, in northern daylight, with gloves on, that is the
// wrong shape, and it got worse the moment the list became form NL-PTI-01: between 67 and 80
// rows depending on the unit and the half of the form. One question per screen is the whole
// point, and that needs a flat, ordered, addressable list of steps.
//
// THE CRUX — KEEPING PROGRESS HONEST, AND THE DENOMINATOR IS NO LONGER ONE NUMBER.
// The denominator a driver reads is the checklist for THIS unit and THIS mode, and there are
// four real ones: NL-01 pre-trip 67, NL-01 post-trip 73, NL-02 pre-trip 74, NL-02 post-trip 80.
// It is DERIVED, every time, from checkCount(unit, mode) in the copied catalogue — never
// written here as a literal and never cached in a module-level constant.
//
//   That invariant is the whole point of this file, and it survived a rewrite.
//   The old code had `export const CHECK_COUNT = …` computed once at module load, which was
//   correct only while the list was one fixed 22-item array. A module constant cannot be right
//   for four different answers; a stale one would have the chip telling a driver they had
//   answered 67 of 67 questions on an 80-question form, on a legal attestation. If you ever
//   find yourself writing a number here, that is the bug.
//
// `CheckStep.n`/`.of` are computed during the checklist walk and never see the defect steps
// injected around them, so the progress chip can never read "68 of 67". A defect step is a
// CHILD of its check, reporting its parent's numbers under a "Follow-up ·" label — the driver
// is told they are on a branch off item 7, not on a 68th item. The review step carries the same
// derived `of` rather than reaching for a constant, for exactly the reason above.
//
// REJECTED, recorded so nobody re-adds it: a dot step strip. It would be the only status
// carrier in the app relying on colour plus shape with no word, and DriverField/CLAUDE.md's
// "colour + glyph + text label, always all three" admits no exception. At 67–80 dots it would
// also be unreadable, which the 22-item version at least was not.
// ---------------------------------------------------------------------------

import {
  checkCount,
  itemsFor,
  type InspectionArea,
  type InspectionFormMode,
  type ItemCategory,
} from "./inspectionForm";
import type { CheckState } from "./types";

/** The odometer reading, asked first: it is the one value the rest of the report hangs off. */
export interface OdometerStep {
  kind: "odometer";
  id: "odometer";
}

/** One of the checklist items. `n`/`of` are the only progress numbers a driver sees. */
export interface CheckStep {
  kind: "check";
  /** `check:${itemKey}` */
  id: string;
  /**
   * The catalogue's `key` — a WIRE VALUE, stored verbatim as InspectionChecklistItem.Item and
   * half of the `(InspectionId, Item)` address a defect is filed against. Answers, the draft
   * and the resume pointer all key on it.
   */
  itemId: string;
  /** The sub-group's title — "Tires & Wheels". */
  group: string;
  /** A, B or C: engine bay, exterior circuit, in-cab. Shown so a long walk stays locatable. */
  area: InspectionArea;
  label: string;
  /** The form's "Check For" column, shown under the question. */
  checkFor: string;
  /** 1-based position among the checks. */
  n: number;
  /** checkCount(unit, mode) — fixed during the checklist walk, blind to injected steps. */
  of: number;
}

/**
 * Injected immediately after a CheckStep answered "defect". A CHILD of its check, not a
 * sibling: it borrows its parent's `n`/`of` rather than taking a number of its own.
 */
export interface DefectStep {
  kind: "defect";
  /** `defect:${itemKey}` */
  id: string;
  itemId: string;
  group: string;
  area: InspectionArea;
  label: string;
  /** The form's default classification for this row. DISPLAY TEXT — nothing computes it. */
  category: ItemCategory;
  /** The form's own qualifier, verbatim ("Major if leaking"), where the row has one. */
  categoryNote?: string;
  parentN: number;
  parentOf: number;
}

/** The attestation. The only scrolling surface in the flow. */
export interface ReviewStep {
  kind: "review";
  id: "review";
  /**
   * The same derived denominator every CheckStep carries. It lives on the step rather than
   * being read from a module constant in progressLabel() — that constant is what could go
   * stale, and "Review · 67 of 67" on an 80-question form is a lie about a legal document.
   */
  of: number;
}

export type InspectionStep = OdometerStep | CheckStep | DefectStep | ReviewStep;

export const ODOMETER_STEP_ID = "odometer";
export const REVIEW_STEP_ID = "review";

export function checkStepId(itemId: string): string {
  return `check:${itemId}`;
}

export function defectStepId(itemId: string): string {
  return `defect:${itemId}`;
}

/**
 * odometer → every NL-PTI-01 row that applies to this unit and mode as a check (with a defect
 * step after any answered "defect") → review. Total: 1 + checkCount + defectCount + 1.
 *
 * `unit` and `mode` are what narrow the list: itemsFor() drops NL02Only rows for a recognised
 * NL-01 and PostTripOnly rows on a pre-trip, and an UNKNOWN unit deliberately gets every row
 * (the fail-safe direction on a compliance form is always more questions — see itemsFor's own
 * doc comment, which is the source of truth and lives in the copy).
 *
 * Pure: same arguments in, structurally equal steps out, and it never reads or writes the draft
 * store. The wizard rebuilds the list on every render, which is what makes changing an answer
 * away from "defect" drop the injected step with no bookkeeping.
 */
export function buildSteps(
  answers: Record<string, CheckState>,
  unit: string | null,
  mode: InspectionFormMode,
): InspectionStep[] {
  const steps: InspectionStep[] = [{ kind: "odometer", id: "odometer" }];
  const groups = itemsFor(unit, mode);
  // Derived from the SAME call that produced `groups`, so the walk and the denominator cannot
  // disagree even if the catalogue changes underneath this file.
  const of = groups.reduce((total, group) => total + group.items.length, 0);

  let n = 0;
  for (const group of groups) {
    for (const item of group.items) {
      n += 1;
      steps.push({
        kind: "check",
        id: checkStepId(item.key),
        itemId: item.key,
        group: group.title,
        area: group.area,
        label: item.label,
        checkFor: item.checkFor,
        n,
        of,
      });

      if (answers[item.key] === "defect") {
        steps.push({
          kind: "defect",
          id: defectStepId(item.key),
          itemId: item.key,
          group: group.title,
          area: group.area,
          label: item.label,
          category: item.category,
          categoryNote: item.categoryNote,
          // The parent's numbers, deliberately. Incrementing `n` here is the bug this
          // structure exists to make impossible.
          parentN: n,
          parentOf: of,
        });
      }
    }
  }

  steps.push({ kind: "review", id: "review", of });
  return steps;
}

/**
 * Resolves a stored step ID against the current step list.
 *
 * The resume pointer is an ID, never an index: an injected defect step shifts every later
 * index, so an index would silently resume on the wrong question. When the pointer no longer
 * exists — the driver was on a defect follow-up and then changed that answer to "Pass" — this
 * falls back to the PARENT check rather than throwing or jumping to the start.
 */
export function resolveStep(steps: InspectionStep[], stepId: string | null): InspectionStep {
  if (stepId) {
    const found = steps.find((s) => s.id === stepId);
    if (found) return found;

    if (stepId.startsWith("defect:")) {
      const parent = steps.find((s) => s.id === checkStepId(stepId.slice("defect:".length)));
      if (parent) return parent;
    }
  }
  return steps[0];
}

/**
 * The progress line. A defect step reports its PARENT's numbers under a "Follow-up ·" label —
 * so the chip can never read "68 of 67" however many defects the driver reports — and the
 * review step reports the denominator it was BUILT with, not one read from anywhere else.
 */
export function progressLabel(step: InspectionStep): string {
  switch (step.kind) {
    case "odometer":
      return "Odometer";
    case "check":
      return `Check ${step.n} of ${step.of}`;
    case "defect":
      return `Follow-up · check ${step.parentN} of ${step.parentOf}`;
    case "review":
      return `Review · ${step.of} of ${step.of}`;
  }
}

/**
 * 0–1 for the determinate bar. Monotonic through the checklist and never above 1, so the bar
 * cannot move backwards when a defect step is injected.
 */
export function progressFraction(step: InspectionStep): number {
  switch (step.kind) {
    case "odometer":
      return 0;
    case "check":
      return step.n / step.of;
    case "defect":
      return step.parentN / step.parentOf;
    case "review":
      return 1;
  }
}

/**
 * The denominator for this unit and mode, re-exported from the catalogue so a caller that needs
 * the number without building steps does not reach for a literal. Deliberately a FUNCTION, not
 * a constant — see this file's header.
 */
export { checkCount };
