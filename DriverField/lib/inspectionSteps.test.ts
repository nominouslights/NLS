import { describe, expect, it } from "vitest";
import {
  buildSteps,
  checkStepId,
  defectStepId,
  nextAfterSection,
  progressFraction,
  progressLabel,
  resolveStep,
  sectionShortcut,
  SECTION_SHORTCUT_MIN,
  type CheckStep,
  type DefectStep,
  type InspectionStep,
} from "./inspectionSteps";
import {
  checkCount,
  itemsFor,
  NL_PTI_01,
  RETIRED_KEYS,
  WITHDRAWN_KEYS,
  type InspectionFormMode,
} from "./inspectionForm";
import { wizard } from "./tablet";
import type { CheckState } from "./types";

// The DVIR wizard's step model, over form NL-PTI-01.
//
// The assertions that matter are about the PROGRESS DENOMINATOR and the ITEM KEYS. A driver
// answering a legal attestation must be told exactly where they are; a chip reading "54 of 53"
// or a follow-up presented as a 72nd question is a trust failure, not a cosmetic one.
//
// THE DENOMINATOR IS NO LONGER ONE NUMBER, AND THAT IS WHAT THIS FILE EXISTS TO PIN.
// It depends on the unit and on the half of the form. Rev 3 consolidated the non-NSC pre-trip
// rows, and rev 4 made the post-trip's checklist the six Close-Out rows alone (every vehicle row
// is pre-trip-only; the post-trip asks for NEW defects on a step of its own that is not a check),
// so there are THREE distinct right answers across the four forms: NL-01 pre 53, NL-02 pre 64,
// and post 6 for both. The counts below are written out because they are the ACCEPTANCE
// CRITERION — what the paper form says — but every one of them is also asserted to equal what
// the code DERIVES from the catalogue. The code must never carry a literal; the test must. If
// the catalogue legitimately changes, exactly these numbers move, and somebody has to look at
// the form to move them.
//
// Item keys mirror InspectionChecklistItem.Item on the backend: the key is the wire value, and
// half of the `(InspectionId, Item)` address a defect is filed against. The uniqueness test
// stands in for two bugs at once — the old model keyed answers by display string (two groups
// sharing an item name collided), and a duplicate key today makes `Enter` fail with
// DuplicateDefectItem.

/** The four real forms. `null` is the unassigned-unit case, which gets the superset. */
const NL01 = "NL-01";
const NL02 = "NL-02";

const DENOMINATORS: { unit: string; mode: InspectionFormMode; expected: number }[] = [
  { unit: NL01, mode: "PreTrip", expected: 53 },
  { unit: NL01, mode: "PostTrip", expected: 6 },
  { unit: NL02, mode: "PreTrip", expected: 64 },
  { unit: NL02, mode: "PostTrip", expected: 6 },
];

/** The post-trip's one extra, uncounted step (rev 4): the new-defects question. */
function extraSteps(mode: InspectionFormMode): number {
  return mode === "PostTrip" ? 1 : 0;
}

function keysFor(unit: string | null, mode: InspectionFormMode): string[] {
  return itemsFor(unit, mode).flatMap((g) => g.items.map((i) => i.key));
}

function steps(
  answers: Record<string, CheckState> = {},
  unit: string | null = NL01,
  mode: InspectionFormMode = "PreTrip",
): InspectionStep[] {
  return buildSteps(answers, unit, mode);
}

function checks(
  answers: Record<string, CheckState> = {},
  unit: string | null = NL01,
  mode: InspectionFormMode = "PreTrip",
): CheckStep[] {
  return steps(answers, unit, mode).filter((s): s is CheckStep => s.kind === "check");
}

const NL01_PRE_KEYS = keysFor(NL01, "PreTrip");

describe("NL-PTI-01 item keys", () => {
  it("are unique across the WHOLE catalogue, not just within a sub-group", () => {
    // THE regression guard. A duplicate key silently merges two different physical checks into
    // one answer, and makes the backend's Enter handler fail with DuplicateDefectItem.
    const all = NL_PTI_01.flatMap((g) => g.items.map((i) => i.key));
    expect(new Set(all).size).toBe(all.length);
  });

  it("stay unique once the form is narrowed, for every unit and mode", () => {
    for (const { unit, mode } of DENOMINATORS) {
      const keys = keysFor(unit, mode);
      expect(new Set(keys).size).toBe(keys.length);
    }
  });

  it("gives every item a non-empty label and a non-empty Check For line", () => {
    // CheckStep.tsx renders both. "Ground beneath the vehicle" with no Check For column is a
    // question a driver cannot answer.
    for (const group of NL_PTI_01) {
      for (const item of group.items) {
        expect(item.label.trim().length).toBeGreaterThan(0);
        expect(item.checkFor.trim().length).toBeGreaterThan(0);
      }
    }
  });
});

describe("the denominator", () => {
  it.each(DENOMINATORS)(
    "is $expected for $unit $mode — derived, never written into the step model",
    ({ unit, mode, expected }) => {
      expect(checkCount(unit, mode)).toBe(expected);
      expect(keysFor(unit, mode)).toHaveLength(expected);
      expect(checks({}, unit, mode)).toHaveLength(expected);
      for (const check of checks({}, unit, mode)) expect(check.of).toBe(expected);
    },
  );

  it("differs between the two units on the pre-trip, and between the two halves of the form", () => {
    // The pin against a module-level constant coming back. The post-trip is the same 6 for
    // both units BY DESIGN (rev 4), so three distinct values is the right answer — a cached
    // denominator would collapse them to one and nothing else in the suite would notice.
    const observed = DENOMINATORS.map(({ unit, mode }) => checkCount(unit, mode));
    expect(new Set(observed).size).toBe(3);
    expect(checkCount(NL01, "PreTrip")).not.toBe(checkCount(NL02, "PreTrip"));
    expect(checkCount(NL01, "PostTrip")).not.toBe(checkCount(NL01, "PreTrip"));
  });

  it("gives an unknown or unassigned unit the FULL superset, never NL-01's narrower form", () => {
    // itemsFor's fail-safe direction: more questions when we do not know what is being
    // inspected. Defaulting an unknown unit to NL-01 would silently drop eleven rows from a
    // compliance pre-trip. (The post-trip has no NL02Only row, so it is 6 either way.)
    expect(checkCount(null, "PreTrip")).toBe(64);
    expect(checkCount("", "PreTrip")).toBe(64);
    expect(checkCount("NL-99", "PreTrip")).toBe(64);
    expect(checkCount(null, "PostTrip")).toBe(6);
    expect(checkCount("", "PostTrip")).toBe(6);
    expect(checkCount("NL-99", "PostTrip")).toBe(6);
  });

  it("makes the post-trip checklist Close-Out alone: the same rows for every unit, nothing else", () => {
    // Rev 4: NSC 13 requires no full post-trip inspection — the end-of-day duty is to RECORD
    // defects found en route. By the owner's decision the post-trip no longer re-checks the
    // vehicle at all: its checklist is the six Close-Out rows, and new defects are asked for on
    // their own step against the pre-trip rows. "Defects noticed while driving" is withdrawn.
    expect(keysFor(NL01, "PostTrip")).toEqual(keysFor(NL02, "PostTrip"));
    expect(keysFor(null, "PostTrip")).toEqual(keysFor(NL02, "PostTrip"));
    expect(itemsFor(NL01, "PostTrip").map((g) => g.key)).toEqual(["Close-Out"]);
    expect(keysFor(NL01, "PostTrip")).not.toContain("Defects noticed while driving");
    expect(keysFor(NL01, "PreTrip")).not.toContain("Defects noticed while driving");
    expect(WITHDRAWN_KEYS.has("Defects noticed while driving")).toBe(true);

    // Every row is on exactly one half — nothing is asked twice any more.
    const all = NL_PTI_01.flatMap((g) => g.items);
    expect(all.every((i) => i.mode === "PreTripOnly" || i.mode === "PostTripOnly")).toBe(true);
    for (const key of keysFor(null, "PostTrip")) expect(keysFor(null, "PreTrip")).not.toContain(key);
    const preTripOnly = all.filter((i) => i.mode === "PreTripOnly").map((i) => i.key);
    expect(preTripOnly.length).toBeGreaterThan(0);
    for (const key of preTripOnly) expect(keysFor(null, "PostTrip")).not.toContain(key);
  });

  it("never lets a withdrawn or retired key back onto either half of the form", () => {
    const current = new Set(NL_PTI_01.flatMap((g) => g.items.map((i) => i.key)));
    for (const key of WITHDRAWN_KEYS) expect(current.has(key)).toBe(false);
    for (const key of RETIRED_KEYS.keys()) expect(current.has(key)).toBe(false);
  });

  it("is what the review step reports, for each unit and mode", () => {
    // progressLabel's review case reads the step's own `of`, built in the same walk as the
    // checks. Reading a module constant here is exactly the staleness this asserts against.
    for (const { unit, mode, expected } of DENOMINATORS) {
      const all = steps({}, unit, mode);
      const review = all[all.length - 1];
      expect(progressLabel(review)).toBe(`Review · ${expected} of ${expected}`);
    }
  });
});

describe("buildSteps", () => {
  it("builds odometer + every check (+ the post-trip's new-defects step) + review with no defects", () => {
    for (const { unit, mode, expected } of DENOMINATORS) {
      const all = steps({}, unit, mode);
      expect(all).toHaveLength(1 + expected + extraSteps(mode) + 1);
      expect(all[0].kind).toBe("odometer");
      expect(all[all.length - 1].kind).toBe("review");
      expect(all.filter((s) => s.kind === "defect")).toHaveLength(0);
    }
  });

  it("injects exactly one defect step, immediately after its own check", () => {
    const target = NL01_PRE_KEYS[7];
    const all = steps({ [target]: "defect" });

    expect(all).toHaveLength(1 + 53 + 1 + 1);
    const at = all.findIndex((s) => s.id === checkStepId(target));
    expect(all[at + 1].id).toBe(defectStepId(target));
  });

  it("keeps `of` at the derived count on every check even when EVERY item is a defect", () => {
    // The denominator cannot be inflated by a branch. This is the assertion that makes
    // "54 of 53" unreachable rather than merely unlikely.
    for (const { unit, mode, expected } of DENOMINATORS) {
      const answers = Object.fromEntries(
        keysFor(unit, mode).map((k) => [k, "defect" as CheckState]),
      );
      const all = steps(answers, unit, mode);

      expect(all).toHaveLength(1 + expected + expected + extraSteps(mode) + 1);
      for (const check of checks(answers, unit, mode)) expect(check.of).toBe(expected);
      expect(checks(answers, unit, mode).map((c) => c.n)).toEqual(
        Array.from({ length: expected }, (_, i) => i + 1),
      );
    }
  });

  it("makes a defect step report its parent's n/of, not a number of its own", () => {
    const target = NL01_PRE_KEYS[6]; // the 7th check
    const defect = steps({ [target]: "defect" }).find(
      (s): s is DefectStep => s.kind === "defect",
    );
    if (!defect) throw new Error("no defect step built");

    expect(defect.parentN).toBe(7);
    expect(defect.parentOf).toBe(53);
    expect(progressLabel(defect)).toBe("Follow-up · check 7 of 53");
    // Same position on the bar as its parent — the bar never moves backwards.
    expect(progressFraction(defect)).toBe(7 / 53);
  });

  it("carries the sub-group, the area and the Check For text onto every step that needs them", () => {
    // ChecklistItemInput has a Group field, and CheckStep.tsx renders the area + sub-group line
    // that makes a walk-around of up to 64 rows locatable. Losing either is a silent regression.
    const answers = Object.fromEntries(
      NL01_PRE_KEYS.map((k) => [k, "defect" as CheckState]),
    );
    for (const step of steps(answers)) {
      if (step.kind === "check" || step.kind === "defect") {
        expect(step.group.trim().length).toBeGreaterThan(0);
        expect(["A", "B", "C"]).toContain(step.area);
        expect(NL_PTI_01.some((g) => g.title === step.group)).toBe(true);
      }
      if (step.kind === "check") expect(step.checkFor.trim().length).toBeGreaterThan(0);
      if (step.kind === "defect") expect(["Minor", "Major"]).toContain(step.category);
    }
  });

  it("gives every step a unique id", () => {
    // The resume pointer is an id, so a collision resumes on the wrong step.
    const answers = Object.fromEntries(
      keysFor(NL02, "PreTrip").map((k) => [k, "defect" as CheckState]),
    );
    const ids = steps(answers, NL02, "PreTrip").map((s) => s.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("drops the injected step when the answer flips away from defect", () => {
    const target = NL01_PRE_KEYS[3];
    expect(steps({ [target]: "defect" }).some((s) => s.kind === "defect")).toBe(true);
    expect(steps({ [target]: "pass" }).some((s) => s.kind === "defect")).toBe(false);
    expect(steps({ [target]: "na" }).some((s) => s.kind === "defect")).toBe(false);
  });

  it("ignores an answer for a row this unit and mode do not ask", () => {
    // A draft started on NL-02 and reopened after a reassignment to NL-01 still holds answers
    // for the NL02Only rows. They must not resurrect a step the narrowed form does not have.
    const nl02Only = keysFor(NL02, "PreTrip").filter((k) => !keysFor(NL01, "PreTrip").includes(k));
    expect(nl02Only.length).toBeGreaterThan(0);

    const answers = Object.fromEntries(nl02Only.map((k) => [k, "defect" as CheckState]));
    const all = steps(answers, NL01, "PreTrip");
    expect(all).toHaveLength(1 + 53 + 1);
    expect(all.some((s) => s.kind === "defect")).toBe(false);
  });

  it("ignores an answer for a row on the OTHER half of the form", () => {
    // A post-trip draft from before rev 4 holds answers for rows that are now pre-trip-only
    // ("Tire condition" was asked on both halves through rev 3). They must not resurrect a
    // check step on the six-row post-trip — a new defect against one of those rows is entered
    // on the new-defects step, never as a checklist row.
    const preOnly = keysFor(NL01, "PreTrip").filter((k) => !keysFor(NL01, "PostTrip").includes(k));
    expect(preOnly).toContain("Tire condition");

    const answers = Object.fromEntries(preOnly.map((k) => [k, "defect" as CheckState]));
    const all = steps(answers, NL01, "PostTrip");
    expect(all).toHaveLength(1 + 6 + 1 + 1);
    expect(all.some((s) => s.kind === "defect")).toBe(false);
  });

  it("is pure — it neither mutates its input nor returns a shared array", () => {
    const answers: Record<string, CheckState> = { [NL01_PRE_KEYS[0]]: "defect" };
    const first = steps(answers);
    const second = steps(answers);
    expect(answers).toEqual({ [NL01_PRE_KEYS[0]]: "defect" });
    expect(first).not.toBe(second);
    expect(first).toEqual(second);
  });
});

describe("the post-trip's new-defects step (rev 4)", () => {
  it("sits after the last check and before Review on a post-trip, for every unit", () => {
    for (const unit of [NL01, NL02, null]) {
      const all = steps({}, unit, "PostTrip");
      const at = all.findIndex((s) => s.kind === "newDefects");
      expect(at).toBe(all.length - 2);
      expect(all[at - 1].kind).toBe("check");
      expect(all[all.length - 1].kind).toBe("review");
      expect(all.filter((s) => s.kind === "newDefects")).toHaveLength(1);
    }
  });

  it("never appears on a pre-trip", () => {
    for (const unit of [NL01, NL02, null]) {
      expect(steps({}, unit, "PreTrip").some((s) => s.kind === "newDefects")).toBe(false);
    }
  });

  it("follows a Close-Out defect follow-up, not the check before it", () => {
    const closeOut = keysFor(NL01, "PostTrip");
    const last = closeOut[closeOut.length - 1];
    const all = steps({ [last]: "defect" }, NL01, "PostTrip");
    const at = all.findIndex((s) => s.kind === "newDefects");
    expect(all[at - 1].id).toBe(defectStepId(last));
  });

  it("is not a check: it takes no number and cannot inflate the denominator", () => {
    // "7 of 6" is unreachable for the same reason "54 of 53" is.
    const all = steps({}, NL01, "PostTrip");
    const step = all.find((s) => s.kind === "newDefects");
    if (!step) throw new Error("no new-defects step built");
    expect(progressLabel(step)).toBe("New defects · 6 of 6 checks done");
    expect(progressFraction(step)).toBe(1);
    for (const check of checks({}, NL01, "PostTrip")) expect(check.of).toBe(6);
    expect(progressLabel(all[all.length - 1])).toBe("Review · 6 of 6");
  });

  it("is where Close-Out's All OK lands — the shortcut never skips the new-defects question", () => {
    const closeOut = itemsFor(NL01, "PostTrip")[0];
    const answers = Object.fromEntries(closeOut.items.map((i) => [i.key, "pass" as CheckState]));
    const all = steps(answers, NL01, "PostTrip");
    expect(nextAfterSection(all, answers, closeOut.key).kind).toBe("newDefects");
  });

  it("resolves by id on a post-trip, and falls back to the start on a pre-trip", () => {
    expect(resolveStep(steps({}, NL01, "PostTrip"), "newDefects").kind).toBe("newDefects");
    expect(resolveStep(steps({}, NL01, "PreTrip"), "newDefects").kind).toBe("odometer");
  });
});

describe("resolveStep", () => {
  it("resolves a known id", () => {
    const target = checkStepId(NL01_PRE_KEYS[5]);
    expect(resolveStep(steps(), target).id).toBe(target);
  });

  it("opens on the odometer with no pointer", () => {
    expect(resolveStep(steps(), null).kind).toBe("odometer");
  });

  it("falls back to the parent check when a defect step disappears", () => {
    // The real sequence: the driver is on the follow-up for item 4, taps Back, changes the
    // answer to Pass. The follow-up no longer exists. Landing on item 4 is the only sane
    // outcome — throwing would lose the draft, and jumping to step 1 would lose their place.
    const target = NL01_PRE_KEYS[3];
    expect(resolveStep(steps({ [target]: "pass" }), defectStepId(target)).id).toBe(
      checkStepId(target),
    );
  });

  it("falls back to the first step for an id that means nothing", () => {
    expect(resolveStep(steps(), "check:Nothing at all").kind).toBe("odometer");
    expect(resolveStep(steps(), "defect:Nothing at all").kind).toBe("odometer");
  });

  it("falls back to the first step for a row the narrowed form does not ask", () => {
    // Same reassignment story as above, seen through the resume pointer rather than the steps.
    const nl02Only = keysFor(NL02, "PreTrip").find((k) => !NL01_PRE_KEYS.includes(k));
    if (!nl02Only) throw new Error("the catalogue has no NL-02-only row");
    expect(resolveStep(steps(), checkStepId(nl02Only)).kind).toBe("odometer");
  });
});

describe("progress", () => {
  it("labels the odometer, a check and the review without a fraction lie", () => {
    const all = steps();
    expect(progressLabel(all[0])).toBe("Odometer & location");
    expect(progressLabel(all[1])).toBe("Check 1 of 53");
    expect(progressLabel(all[all.length - 1])).toBe("Review · 53 of 53");
  });

  it("never exceeds 1 or drops below 0, for any answer combination on any form", () => {
    for (const { unit, mode } of DENOMINATORS) {
      const answers = Object.fromEntries(
        keysFor(unit, mode).map((k, i) => [k, (i % 3 === 0 ? "defect" : "pass") as CheckState]),
      );
      for (const step of steps(answers, unit, mode)) {
        const f = progressFraction(step);
        expect(f).toBeGreaterThanOrEqual(0);
        expect(f).toBeLessThanOrEqual(1);
      }
    }
  });
});

// ---------------------------------------------------------------------------
// The per-section "All OK" shortcut (rev 3). The WRITE and its blanks-only rule are pinned in
// lib/inspectionStore.test.ts (markSectionOk); the DOM flow, the confirm list and the payload in
// components/screens/Inspection.test.tsx. This block pins WHEN it is offered and WHERE it lands.
// ---------------------------------------------------------------------------

/** The check steps of one sub-group on a form, in form order. */
function groupChecks(
  groupKey: string,
  answers: Record<string, CheckState> = {},
  unit: string | null = NL01,
  mode: InspectionFormMode = "PreTrip",
): CheckStep[] {
  return checks(answers, unit, mode).filter((c) => c.groupKey === groupKey);
}

const MAX = wizard.sectionMaxRows;

describe("sectionShortcut", () => {
  it("carries the sub-group's catalogue key on every check and defect step", () => {
    const groupKeys = new Set(NL_PTI_01.map((g) => g.key));
    const answers = Object.fromEntries(NL01_PRE_KEYS.map((k) => [k, "defect" as CheckState]));
    for (const step of steps(answers)) {
      if (step.kind === "check" || step.kind === "defect") {
        expect(groupKeys.has(step.groupKey)).toBe(true);
      }
    }
  });

  it("is offered on the first row of every multi-row sub-group of a fresh form, and only there", () => {
    for (const { unit, mode } of DENOMINATORS) {
      const all = steps({}, unit, mode);
      for (const group of itemsFor(unit, mode)) {
        const rows = groupChecks(group.key, {}, unit, mode);
        const first = sectionShortcut(all, {}, rows[0].id, MAX);
        if (rows.length >= SECTION_SHORTCUT_MIN) {
          expect(first?.groupKey).toBe(group.key);
          expect(first?.unanswered).toBe(rows.length);
        } else {
          // One row: the Pass tile IS the shortcut.
          expect(first).toBeNull();
        }
        for (const later of rows.slice(1)) {
          expect(sectionShortcut(all, {}, later.id, MAX)).toBeNull();
        }
      }
    }
  });

  it("lists EVERY row of the sub-group by label, in form order, with its current answer", () => {
    // The driver must see what they are certifying — including the rows they already answered,
    // which the shortcut will not touch.
    const rows = groupChecks("Controls & Instruments", {}, NL02);
    const answers: Record<string, CheckState> = { [rows[0].itemId]: "defect" };
    const s = sectionShortcut(steps(answers, NL02), answers, rows[1].id, MAX);
    if (!s) throw new Error("shortcut not offered on the first blank row");

    expect(s.rows.map((r) => r.label)).toEqual(rows.map((r) => r.label));
    expect(s.rows.map((r) => r.checkFor)).toEqual(rows.map((r) => r.checkFor));
    expect(s.rows[0].state).toBe("defect");
    expect(s.rows.slice(1).every((r) => r.state === null)).toBe(true);
    expect(s.unanswered).toBe(rows.length - 1);
    expect(s.title).toBe("Controls & Instruments");
  });

  it("moves to the next blank row once earlier rows are answered, counting only the blanks", () => {
    const rows = groupChecks("Tires & Wheels");
    const answers: Record<string, CheckState> = {
      [rows[0].itemId]: "pass",
      [rows[1].itemId]: "na",
    };
    const all = steps(answers);
    expect(sectionShortcut(all, answers, rows[0].id, MAX)).toBeNull();
    expect(sectionShortcut(all, answers, rows[1].id, MAX)).toBeNull();
    expect(sectionShortcut(all, answers, rows[2].id, MAX)?.unanswered).toBe(rows.length - 2);
  });

  it("is not offered on an answered row, even the first one", () => {
    const rows = groupChecks("Tires & Wheels");
    const answers: Record<string, CheckState> = { [rows[0].itemId]: "pass" };
    expect(sectionShortcut(steps(answers), answers, rows[0].id, MAX)).toBeNull();
  });

  it("is not offered when a single blank would be left — that is just the Pass tile", () => {
    const rows = groupChecks("Brakes — Functional Test");
    expect(rows.length).toBeGreaterThanOrEqual(2);
    const answers: Record<string, CheckState> = Object.fromEntries(
      rows.slice(0, -1).map((r) => [r.itemId, "pass" as CheckState]),
    );
    expect(sectionShortcut(steps(answers), answers, rows[rows.length - 1].id, MAX)).toBeNull();
  });

  it("is never offered on the odometer, a defect follow-up or the review step", () => {
    const target = NL01_PRE_KEYS[0];
    const answers: Record<string, CheckState> = { [target]: "defect" };
    const all = steps(answers);
    expect(sectionShortcut(all, answers, "odometer", MAX)).toBeNull();
    expect(sectionShortcut(all, answers, defectStepId(target), MAX)).toBeNull();
    expect(sectionShortcut(all, answers, "review", MAX)).toBeNull();
  });

  it("is withheld for a sub-group too large for the confirm panel, rather than clipped", () => {
    const rows = groupChecks("Controls & Instruments", {}, NL02);
    const all = steps({}, NL02);
    expect(sectionShortcut(all, {}, rows[0].id, rows.length)).not.toBeNull();
    expect(sectionShortcut(all, {}, rows[0].id, rows.length - 1)).toBeNull();
  });

  it("fits every sub-group of every form inside the confirm panel's height budget", () => {
    // THE BUDGET PIN. lib/tablet.ts budgets the label-only, two-column confirm list for
    // `wizard.sectionMaxRows` rows — the largest rev 3 sub-group, Controls & Instruments at 12
    // on NL-02. A catalogue change that grows a sub-group past it would silently lose that
    // section's shortcut; this makes it a visible failure instead, so somebody re-does the
    // height budget rather than nobody noticing.
    const largest = Math.max(
      ...DENOMINATORS.flatMap(({ unit, mode }) => itemsFor(unit, mode).map((g) => g.items.length)),
      ...itemsFor(null, "PreTrip").map((g) => g.items.length),
    );
    expect(largest).toBe(12);
    expect(largest).toBeLessThanOrEqual(MAX);
  });

  it("narrows to the unit: an NL-01 confirm list never shows an NL-02-only row", () => {
    const nl02Only = new Set(
      keysFor(NL02, "PreTrip").filter((k) => !keysFor(NL01, "PreTrip").includes(k)),
    );
    const all = steps({}, NL01);
    for (const group of itemsFor(NL01, "PreTrip")) {
      const s = sectionShortcut(all, {}, groupChecks(group.key)[0].id, MAX);
      for (const row of s?.rows ?? []) expect(nl02Only.has(row.itemId)).toBe(false);
    }
  });
});

describe("nextAfterSection", () => {
  it("lands on the next sub-group's first check after a fresh section is filled", () => {
    const groups = itemsFor(NL01, "PreTrip");
    const first = groups[0];
    const answers = Object.fromEntries(first.items.map((i) => [i.key, "pass" as CheckState]));
    const next = nextAfterSection(steps(answers), answers, first.key);
    expect(next.id).toBe(checkStepId(groups[1].items[0].key));
  });

  it("skips a later sub-group that is already fully answered", () => {
    const groups = itemsFor(NL01, "PreTrip");
    const answers = Object.fromEntries(
      [...groups[0].items, ...groups[1].items].map((i) => [i.key, "pass" as CheckState]),
    );
    const next = nextAfterSection(steps(answers), answers, groups[0].key);
    expect(next.id).toBe(checkStepId(groups[2].items[0].key));
  });

  it("goes to Review when nothing after the sub-group is blank", () => {
    const groups = itemsFor(NL01, "PreTrip");
    const last = groups[groups.length - 1];
    // Earlier rows left blank on purpose: they are NOT jumped back to — Review lists them.
    const answers = Object.fromEntries(last.items.map((i) => [i.key, "pass" as CheckState]));
    expect(nextAfterSection(steps(answers), answers, last.key).kind).toBe("review");
  });

  it("is not stopped by a defect follow-up inside the sub-group", () => {
    // A row answered Defect before the shortcut keeps its follow-up step; the shortcut moves on
    // past it, and the review step's ungraded count is what demands the severity.
    const groups = itemsFor(NL01, "PreTrip");
    const first = groups[0];
    const answers: Record<string, CheckState> = Object.fromEntries(
      first.items.map((i) => [i.key, "pass" as CheckState]),
    );
    answers[first.items[0].key] = "defect";
    const next = nextAfterSection(steps(answers), answers, first.key);
    expect(next.id).toBe(checkStepId(groups[1].items[0].key));
  });

  it("keeps the progress numbers honest: the landing step reports its own position", () => {
    const groups = itemsFor(NL01, "PreTrip");
    const first = groups[0];
    const answers = Object.fromEntries(first.items.map((i) => [i.key, "pass" as CheckState]));
    const next = nextAfterSection(steps(answers), answers, first.key);
    expect(progressLabel(next)).toBe(`Check ${first.items.length + 1} of 53`);
  });
});
