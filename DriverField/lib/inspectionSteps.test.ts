import { describe, expect, it } from "vitest";
import {
  buildSteps,
  checkStepId,
  defectStepId,
  progressFraction,
  progressLabel,
  resolveStep,
  type CheckStep,
  type DefectStep,
  type InspectionStep,
} from "./inspectionSteps";
import {
  checkCount,
  itemsFor,
  NL_PTI_01,
  type InspectionFormMode,
} from "./inspectionForm";
import type { CheckState } from "./types";

// The DVIR wizard's step model, over form NL-PTI-01.
//
// The assertions that matter are about the PROGRESS DENOMINATOR and the ITEM KEYS. A driver
// answering a legal attestation must be told exactly where they are; a chip reading "68 of 67"
// or a follow-up presented as a 68th question is a trust failure, not a cosmetic one.
//
// THE DENOMINATOR IS NO LONGER ONE NUMBER, AND THAT IS WHAT THIS FILE EXISTS TO PIN.
// It depends on the unit and on the half of the form, so there are four right answers. The
// counts below are written out because they are the ACCEPTANCE CRITERION — what the paper form
// says — but every one of them is also asserted to equal what the code DERIVES from the
// catalogue. The code must never carry a literal; the test must. If the catalogue legitimately
// changes, exactly these four numbers move, and somebody has to look at the form to move them.
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
  { unit: NL01, mode: "PreTrip", expected: 67 },
  { unit: NL01, mode: "PostTrip", expected: 73 },
  { unit: NL02, mode: "PreTrip", expected: 74 },
  { unit: NL02, mode: "PostTrip", expected: 80 },
];

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

  it("differs between the two units and between the two halves of the form", () => {
    // The pin against a module-level constant coming back. A cached denominator would make at
    // least three of these four equal, and nothing else in the suite would notice.
    const observed = DENOMINATORS.map(({ unit, mode }) => checkCount(unit, mode));
    expect(new Set(observed).size).toBe(4);
  });

  it("gives an unknown or unassigned unit the FULL superset, never NL-01's narrower form", () => {
    // itemsFor's fail-safe direction: more questions when we do not know what is being
    // inspected. Defaulting an unknown unit to NL-01 would silently drop seven rows from a
    // compliance form.
    expect(checkCount(null, "PostTrip")).toBe(80);
    expect(checkCount("", "PostTrip")).toBe(80);
    expect(checkCount("NL-99", "PostTrip")).toBe(80);
    expect(checkCount(null, "PreTrip")).toBe(74);
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
  it("builds odometer + every check + review with no defects", () => {
    for (const { unit, mode, expected } of DENOMINATORS) {
      const all = steps({}, unit, mode);
      expect(all).toHaveLength(1 + expected + 1);
      expect(all[0].kind).toBe("odometer");
      expect(all[all.length - 1].kind).toBe("review");
      expect(all.filter((s) => s.kind === "defect")).toHaveLength(0);
    }
  });

  it("injects exactly one defect step, immediately after its own check", () => {
    const target = NL01_PRE_KEYS[7];
    const all = steps({ [target]: "defect" });

    expect(all).toHaveLength(1 + 67 + 1 + 1);
    const at = all.findIndex((s) => s.id === checkStepId(target));
    expect(all[at + 1].id).toBe(defectStepId(target));
  });

  it("keeps `of` at the derived count on every check even when EVERY item is a defect", () => {
    // The denominator cannot be inflated by a branch. This is the assertion that makes
    // "68 of 67" unreachable rather than merely unlikely.
    for (const { unit, mode, expected } of DENOMINATORS) {
      const answers = Object.fromEntries(
        keysFor(unit, mode).map((k) => [k, "defect" as CheckState]),
      );
      const all = steps(answers, unit, mode);

      expect(all).toHaveLength(1 + expected + expected + 1);
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
    expect(defect.parentOf).toBe(67);
    expect(progressLabel(defect)).toBe("Follow-up · check 7 of 67");
    // Same position on the bar as its parent — the bar never moves backwards.
    expect(progressFraction(defect)).toBe(7 / 67);
  });

  it("carries the sub-group, the area and the Check For text onto every step that needs them", () => {
    // ChecklistItemInput has a Group field, and CheckStep.tsx renders the area + sub-group line
    // that makes a 67-to-80-row walk-around locatable. Losing either is a silent regression.
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
      keysFor(NL02, "PostTrip").map((k) => [k, "defect" as CheckState]),
    );
    const ids = steps(answers, NL02, "PostTrip").map((s) => s.id);
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
    const nl02Only = keysFor(NL02, "PostTrip").filter((k) => !keysFor(NL01, "PreTrip").includes(k));
    expect(nl02Only.length).toBeGreaterThan(0);

    const answers = Object.fromEntries(nl02Only.map((k) => [k, "defect" as CheckState]));
    const all = steps(answers, NL01, "PreTrip");
    expect(all).toHaveLength(1 + 67 + 1);
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
    const nl02Only = keysFor(NL02, "PostTrip").find((k) => !NL01_PRE_KEYS.includes(k));
    if (!nl02Only) throw new Error("the catalogue has no NL-02-only row");
    expect(resolveStep(steps(), checkStepId(nl02Only)).kind).toBe("odometer");
  });
});

describe("progress", () => {
  it("labels the odometer, a check and the review without a fraction lie", () => {
    const all = steps();
    expect(progressLabel(all[0])).toBe("Odometer");
    expect(progressLabel(all[1])).toBe("Check 1 of 67");
    expect(progressLabel(all[all.length - 1])).toBe("Review · 67 of 67");
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
