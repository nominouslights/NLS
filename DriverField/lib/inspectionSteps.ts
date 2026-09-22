// ---------------------------------------------------------------------------
// The DVIR wizard's step model. PURE and React-free, so Vitest exercises it without jsdom —
// the same reason eligibility() and hosRemaining() live in lib/ rather than in a screen.
//
// WHY A STEP MODEL AT ALL: the inspection screen used to render all 22 checklist items as one
// continuous scroll, three or four screens deep, with the legal attestation at the very bottom
// — the part a driver sees least. On a dash-mounted 10-inch tablet, in northern daylight, with
// gloves on, that is the wrong shape. One question per screen is the whole point, and that
// needs a flat, ordered, addressable list of steps.
//
// THE CRUX — KEEPING PROGRESS HONEST. The denominator a driver reads is the CHECKLIST, which is
// 22 forever. `CheckStep.n`/`.of` are computed during the checklist walk and never see the
// defect steps injected around them, so the progress chip can never read "23 of 22". A defect
// step is a CHILD of its check, reporting its parent's numbers under a "Follow-up ·" label —
// the driver is told they are on a branch off item 7, not on a 23rd item.
//
// REJECTED, recorded so nobody re-adds it: a 22-dot step strip. It would be the only status
// carrier in the app relying on colour plus shape with no word, and DriverField/CLAUDE.md's
// "colour + glyph + text label, always all three" admits no exception.
// ---------------------------------------------------------------------------

import { dvirChecklist } from "./data";
import type { CheckState } from "./types";

/**
 * The number of checklist items — the ONLY denominator a driver ever sees. Derived from the
 * data rather than written as 22, so adding a checklist item cannot leave the chip lying.
 */
export const CHECK_COUNT: number = dvirChecklist.reduce((n, g) => n + g.items.length, 0);

/** The odometer reading, asked first: it is the one value the rest of the report hangs off. */
export interface OdometerStep {
  kind: "odometer";
  id: "odometer";
}

/** One of the checklist items. `n`/`of` are the only progress numbers a driver sees. */
export interface CheckStep {
  kind: "check";
  /** `check:${itemId}` */
  id: string;
  itemId: string;
  group: string;
  label: string;
  /** 1-based position among the checks. */
  n: number;
  /** CHECK_COUNT — fixed during the checklist walk, blind to injected steps. */
  of: number;
}

/**
 * Injected immediately after a CheckStep answered "defect". A CHILD of its check, not a
 * sibling: it borrows its parent's `n`/`of` rather than taking a number of its own.
 */
export interface DefectStep {
  kind: "defect";
  /** `defect:${itemId}` */
  id: string;
  itemId: string;
  group: string;
  label: string;
  parentN: number;
  parentOf: number;
}

/** The attestation. The only scrolling surface in the flow. */
export interface ReviewStep {
  kind: "review";
  id: "review";
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
 * odometer → each group's items as checks (with a defect step after any answered "defect")
 * → review. Total: 1 + CHECK_COUNT + defectCount + 1.
 *
 * Pure: same answers in, structurally equal steps out, and it never reads or writes the draft
 * store. The wizard rebuilds the list on every render, which is what makes changing an answer
 * away from "defect" drop the injected step with no bookkeeping.
 */
export function buildSteps(answers: Record<string, CheckState>): InspectionStep[] {
  const steps: InspectionStep[] = [{ kind: "odometer", id: "odometer" }];

  let n = 0;
  for (const group of dvirChecklist) {
    for (const item of group.items) {
      n += 1;
      steps.push({
        kind: "check",
        id: checkStepId(item.id),
        itemId: item.id,
        group: group.group,
        label: item.label,
        n,
        of: CHECK_COUNT,
      });

      if (answers[item.id] === "defect") {
        steps.push({
          kind: "defect",
          id: defectStepId(item.id),
          itemId: item.id,
          group: group.group,
          label: item.label,
          // The parent's numbers, deliberately. Incrementing `n` here is the bug this
          // structure exists to make impossible.
          parentN: n,
          parentOf: CHECK_COUNT,
        });
      }
    }
  }

  steps.push({ kind: "review", id: "review" });
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
 * so the chip can never read "23 of 22" however many defects the driver reports.
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
      return `Review · ${CHECK_COUNT} of ${CHECK_COUNT}`;
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
