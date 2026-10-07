// ---------------------------------------------------------------------------
// The DVIR wizard's step model. PURE and React-free, so Vitest exercises it without jsdom —
// the same reason eligibility() and hosRemaining() live in lib/ rather than in a screen.
//
// WHY A STEP MODEL AT ALL: the inspection screen used to render every checklist item as one
// continuous scroll, with the legal attestation at the very bottom — the part a driver sees
// least. On a dash-mounted 10-inch tablet, in northern daylight, with gloves on, that is the
// wrong shape, and it got worse the moment the list became form NL-PTI-01: up to 64 rows on a
// pre-trip (rev 3), depending on the unit. One question per screen is the whole
// point, and that needs a flat, ordered, addressable list of steps.
//
// THE CRUX — KEEPING PROGRESS HONEST, AND THE DENOMINATOR IS NO LONGER ONE NUMBER.
// The denominator a driver reads is the checklist for THIS unit and THIS mode: NL-01 pre-trip
// 53, NL-02 pre-trip 64 (rev 3), and the post-trip 6 for both — since rev 4 its checklist is the
// Close-Out rows alone, followed by the "New defects since the pre-trip" step, which is a
// question about the run rather than a check and so is NOT counted in the denominator.
// It is DERIVED, every time, from checkCount(unit, mode) in the copied catalogue — never
// written here as a literal and never cached in a module-level constant.
//
//   That invariant is the whole point of this file, and it survived a rewrite.
//   The old code had `export const CHECK_COUNT = …` computed once at module load, which was
//   correct only while the list was one fixed 22-item array. A module constant cannot be right
//   for three different answers; a stale one would have the chip telling a driver they had
//   answered 53 of 53 questions on a 64-question form, on a legal attestation. If you ever
//   find yourself writing a number here, that is the bug.
//
// `CheckStep.n`/`.of` are computed during the checklist walk and never see the defect steps
// injected around them, so the progress chip can never read "54 of 53". A defect step is a
// CHILD of its check, reporting its parent's numbers under a "Follow-up ·" label — the driver
// is told they are on a branch off item 7, not on a 72nd item. The review step carries the same
// derived `of` rather than reaching for a constant, for exactly the reason above.
//
// REJECTED, recorded so nobody re-adds it: a dot step strip. It would be the only status
// carrier in the app relying on colour plus shape with no word, and DriverField/CLAUDE.md's
// "colour + glyph + text label, always all three" admits no exception. At 64 dots it would
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

/**
 * The report header, asked first: the odometer reading (the one value the rest of the report
 * hangs off) and the inspection location (Man. Reg. 95/2008 s.12(1)). The id stays "odometer"
 * so a resume pointer and every caller keep working.
 */
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
  /**
   * The sub-group's catalogue key — the wire value sent as ChecklistItemInput.Group, and what
   * the per-section "All OK" shortcut is scoped by. Distinct from `group`, the display title.
   */
  groupKey: string;
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
  groupKey: string;
  area: InspectionArea;
  label: string;
  /** The form's default classification for this row. DISPLAY TEXT — nothing computes it. */
  category: ItemCategory;
  /** The form's own qualifier, verbatim ("Major if leaking"), where the row has one. */
  categoryNote?: string;
  parentN: number;
  parentOf: number;
}

/**
 * POST-TRIP ONLY (NL-PTI-01 rev 4): "Any defect found after the pre-trip?", and on Yes the list
 * of new defects, each filed against a pre-trip item. Placed after the Close-Out checks and
 * before the review. It is not a check, so it takes no `n`: it carries the checklist's `of` only
 * so its progress line can say the checks are behind the driver, and the denominator stays the
 * number of CHECKS — "7 of 6" is unreachable for the same reason "54 of 53" is.
 */
export interface NewDefectsStep {
  kind: "newDefects";
  id: "newDefects";
  of: number;
}

/** The attestation. The only scrolling surface in the flow. */
export interface ReviewStep {
  kind: "review";
  id: "review";
  /**
   * The same derived denominator every CheckStep carries. It lives on the step rather than
   * being read from a module constant in progressLabel() — that constant is what could go
   * stale, and "Review · 53 of 53" on a 64-question form is a lie about a legal document.
   */
  of: number;
}

export type InspectionStep = OdometerStep | CheckStep | DefectStep | NewDefectsStep | ReviewStep;

export const ODOMETER_STEP_ID = "odometer";
export const NEW_DEFECTS_STEP_ID = "newDefects";
export const REVIEW_STEP_ID = "review";

export function checkStepId(itemId: string): string {
  return `check:${itemId}`;
}

export function defectStepId(itemId: string): string {
  return `defect:${itemId}`;
}

/**
 * odometer → every NL-PTI-01 row that applies to this unit and mode as a check (with a defect
 * step after any answered "defect") → [post-trip only: the new-defects step] → review.
 * Total: 1 + checkCount + defectCount + (PostTrip ? 1 : 0) + 1.
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
        groupKey: group.key,
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
          groupKey: group.key,
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

  // Rev 4: the post-trip asks for NEW defects instead of re-checking the vehicle. One step,
  // whatever the answer — the defects themselves are entered on it, not injected as more steps,
  // so nothing about it can move a check's number.
  if (mode === "PostTrip") steps.push({ kind: "newDefects", id: "newDefects", of });

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
 * so the chip can never read "54 of 53" however many defects the driver reports — and the
 * review step reports the denominator it was BUILT with, not one read from anywhere else.
 */
export function progressLabel(step: InspectionStep): string {
  switch (step.kind) {
    case "odometer":
      return "Odometer & location";
    case "check":
      return `Check ${step.n} of ${step.of}`;
    case "defect":
      return `Follow-up · check ${step.parentN} of ${step.parentOf}`;
    case "newDefects":
      return `New defects · ${step.of} of ${step.of} checks done`;
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
    case "newDefects":
      // Every check is behind the driver by now; the bar is full and stays full into Review.
      return 1;
    case "review":
      return 1;
  }
}

// ---------------------------------------------------------------------------
// The per-section "All OK" shortcut (NL-PTI-01 rev 3, at the owner's request).
//
// WHAT IT IS: on the FIRST unanswered check of a sub-group, the driver may instead review that
// sub-group's rows on one screen and confirm them all as Pass in one tap. It is the main time
// saver of rev 3, and it is bounded by rules that keep it from becoming a rubber stamp:
//
//   • ONE SUB-GROUP AT A TIME. There is no whole-form "all OK" and there must never be one —
//     a one-question-per-screen flow whose first affordance skips every question is
//     self-defeating. markSectionOk() in lib/inspectionStore.ts takes a group key, not a list
//     of item keys, so nothing can call it for more than one sub-group.
//   • IT ONLY FILLS BLANKS. A Defect or an N/A (or a Pass) the driver already gave is never
//     overwritten — enforced in the store, not here, so no caller can get it wrong.
//   • THE DRIVER SEES WHAT THEY ARE CERTIFYING. The confirm panel lists every row of the
//     sub-group by its label, with what will happen to it, before the one confirming tap.
//   • NOTHING NEW ON THE WIRE. Each filled row is an ordinary "pass" answer, sent as its own
//     ChecklistItemInput with state Ok — no "section passed" value exists anywhere — and each
//     can still be changed individually afterwards (Back, or the review step's Change).
//
// Offered only when at least SECTION_SHORTCUT_MIN rows are still blank (for one row, the Pass
// tile IS the shortcut) and the sub-group fits the confirm panel's height budget
// (`wizard.sectionMaxRows` in lib/tablet.ts). A sub-group that outgrew the budget loses the
// shortcut rather than rendering a list that WizardFrame would silently clip.
// ---------------------------------------------------------------------------

/** Below this many blank rows the shortcut is not offered — the Pass tile already is one. */
export const SECTION_SHORTCUT_MIN = 2;

export interface SectionShortcutRow {
  itemId: string;
  label: string;
  checkFor: string;
  /** The driver's current answer, or null when the shortcut would fill it with Pass. */
  state: CheckState | null;
}

export interface SectionShortcut {
  groupKey: string;
  title: string;
  area: InspectionArea;
  /** Every row of the sub-group on THIS form, in form order, answered or not. */
  rows: SectionShortcutRow[];
  /** How many rows the shortcut would fill — the number on the button. */
  unanswered: number;
}

/**
 * The shortcut for the check step `stepId`, or null when it is not offered there.
 *
 * Offered only on a CHECK step that is unanswered AND is the first unanswered row of its
 * sub-group in form order, with at least SECTION_SHORTCUT_MIN blanks and no more rows than
 * `maxRows`. Read from `steps`, so it sees exactly the unit- and mode-narrowed form the wizard
 * walks — an NL02Only row can never appear in an NL-01 confirm list.
 */
export function sectionShortcut(
  steps: InspectionStep[],
  answers: Record<string, CheckState>,
  stepId: string,
  maxRows: number,
): SectionShortcut | null {
  const step = steps.find((s) => s.id === stepId);
  if (!step || step.kind !== "check") return null;
  if (answers[step.itemId] !== undefined) return null;

  const groupChecks = steps.filter(
    (s): s is CheckStep => s.kind === "check" && s.groupKey === step.groupKey,
  );
  const firstBlank = groupChecks.find((c) => answers[c.itemId] === undefined);
  if (!firstBlank || firstBlank.id !== step.id) return null;

  const rows: SectionShortcutRow[] = groupChecks.map((c) => ({
    itemId: c.itemId,
    label: c.label,
    checkFor: c.checkFor,
    state: answers[c.itemId] ?? null,
  }));
  const unanswered = rows.filter((r) => r.state === null).length;
  if (unanswered < SECTION_SHORTCUT_MIN) return null;
  if (rows.length > maxRows) return null;

  return { groupKey: step.groupKey, title: step.group, area: step.area, rows, unanswered };
}

/**
 * Where the wizard goes after the shortcut fills `groupKey`: the first still-unanswered check
 * AFTER that sub-group, else the post-trip's new-defects step if this form has one, else the
 * review step. Rows left blank BEFORE the
 * sub-group are not jumped back to — the review step lists them as "Not answered" and blocks
 * Certify on them, exactly as for a driver who skipped ahead with the Review button.
 *
 * Built from the steps AFTER the fill, so a defect follow-up still owed inside the sub-group is
 * not a target either: it was answered "defect" before the shortcut and the review step's
 * ungraded count is what catches a missing severity.
 */
export function nextAfterSection(
  steps: InspectionStep[],
  answers: Record<string, CheckState>,
  groupKey: string,
): InspectionStep {
  let lastInGroup = -1;
  steps.forEach((s, i) => {
    if ((s.kind === "check" || s.kind === "defect") && s.groupKey === groupKey) lastInGroup = i;
  });
  const review = steps[steps.length - 1];
  if (lastInGroup === -1) return review;

  for (let i = lastInGroup + 1; i < steps.length; i += 1) {
    const s = steps[i];
    if (s.kind === "check" && answers[s.itemId] === undefined) return s;
    // On a post-trip the new-defects question follows the checks and is never skipped past:
    // "All OK" on Close-Out says the six checks are fine, not that nothing broke on the run.
    if (s.kind === "newDefects") return s;
  }
  return review;
}

/**
 * The denominator for this unit and mode, re-exported from the catalogue so a caller that needs
 * the number without building steps does not reach for a literal. Deliberately a FUNCTION, not
 * a constant — see this file's header.
 */
export { checkCount };
