import { describe, expect, it } from "vitest";
import {
  buildSteps,
  CHECK_COUNT,
  checkStepId,
  defectStepId,
  progressFraction,
  progressLabel,
  resolveStep,
  type CheckStep,
  type DefectStep,
} from "./inspectionSteps";
import { dvirChecklist } from "./data";
import type { CheckState } from "./types";

// The DVIR wizard's step model.
//
// The assertions that matter are about the PROGRESS DENOMINATOR and the ITEM IDS. A driver
// answering a legal attestation must be told exactly where they are; a chip reading "23 of 22"
// or a follow-up presented as a 23rd question is a trust failure, not a cosmetic one. And the
// unique-ids test stands in for the bug this whole model replaces: answers used to be keyed by
// the display string, so two groups sharing an item name collided and the group was dropped
// from the payload even though ChecklistItemInput carries a Group field.

const ALL_ITEM_IDS: string[] = dvirChecklist.flatMap((g) => g.items.map((i) => i.id));

function checks(answers: Record<string, CheckState> = {}): CheckStep[] {
  return buildSteps(answers).filter((s): s is CheckStep => s.kind === "check");
}

describe("checklist item ids", () => {
  it("are unique across all five groups", () => {
    // THE regression guard. A duplicate id silently merges two different physical checks into
    // one answer — and one of them would never appear in the compliance record.
    expect(new Set(ALL_ITEM_IDS).size).toBe(ALL_ITEM_IDS.length);
  });

  it("number 22, matching CHECK_COUNT", () => {
    expect(ALL_ITEM_IDS).toHaveLength(22);
    expect(CHECK_COUNT).toBe(22);
  });

  it("gives every item a non-empty label distinct from its id", () => {
    for (const group of dvirChecklist) {
      for (const item of group.items) {
        expect(item.label.trim().length).toBeGreaterThan(0);
        expect(item.label).not.toBe(item.id);
      }
    }
  });
});

describe("buildSteps", () => {
  it("builds odometer + 22 checks + review with no defects", () => {
    const steps = buildSteps({});
    expect(steps).toHaveLength(1 + CHECK_COUNT + 1);
    expect(steps[0].kind).toBe("odometer");
    expect(steps[steps.length - 1].kind).toBe("review");
    expect(steps.filter((s) => s.kind === "defect")).toHaveLength(0);
  });

  it("injects exactly one defect step, immediately after its own check", () => {
    const target = ALL_ITEM_IDS[7];
    const steps = buildSteps({ [target]: "defect" });

    expect(steps).toHaveLength(1 + CHECK_COUNT + 1 + 1);
    const at = steps.findIndex((s) => s.id === checkStepId(target));
    expect(steps[at + 1].id).toBe(defectStepId(target));
  });

  it("keeps `of` at 22 on every check even when all 22 are defects", () => {
    // The denominator cannot be inflated by a branch. This is the assertion that makes
    // "23 of 22" unreachable rather than merely unlikely.
    const answers = Object.fromEntries(
      ALL_ITEM_IDS.map((id) => [id, "defect" as CheckState]),
    );
    const steps = buildSteps(answers);

    expect(steps).toHaveLength(1 + CHECK_COUNT + CHECK_COUNT + 1);
    for (const check of checks(answers)) expect(check.of).toBe(22);
    expect(checks(answers).map((c) => c.n)).toEqual(
      Array.from({ length: 22 }, (_, i) => i + 1),
    );
  });

  it("makes a defect step report its parent's n/of, not a number of its own", () => {
    const target = ALL_ITEM_IDS[6]; // the 7th check
    const steps = buildSteps({ [target]: "defect" });
    const defect = steps.find((s): s is DefectStep => s.kind === "defect");
    if (!defect) throw new Error("no defect step built");

    expect(defect.parentN).toBe(7);
    expect(defect.parentOf).toBe(22);
    expect(progressLabel(defect)).toBe("Follow-up · check 7 of 22");
    // Same position on the bar as its parent — the bar never moves backwards.
    expect(progressFraction(defect)).toBe(7 / 22);
  });

  it("carries the group onto every check and defect step", () => {
    // ChecklistItemInput has a Group field. Losing it was the old keying bug's second victim.
    const answers = Object.fromEntries(
      ALL_ITEM_IDS.map((id) => [id, "defect" as CheckState]),
    );
    for (const step of buildSteps(answers)) {
      if (step.kind === "check" || step.kind === "defect") {
        expect(step.group.trim().length).toBeGreaterThan(0);
        expect(dvirChecklist.some((g) => g.group === step.group)).toBe(true);
      }
    }
  });

  it("gives every step a unique id", () => {
    // The resume pointer is an id, so a collision resumes on the wrong step.
    const answers = Object.fromEntries(
      ALL_ITEM_IDS.map((id) => [id, "defect" as CheckState]),
    );
    const ids = buildSteps(answers).map((s) => s.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it("drops the injected step when the answer flips away from defect", () => {
    const target = ALL_ITEM_IDS[3];
    expect(buildSteps({ [target]: "defect" }).some((s) => s.kind === "defect")).toBe(true);
    expect(buildSteps({ [target]: "pass" }).some((s) => s.kind === "defect")).toBe(false);
    expect(buildSteps({ [target]: "na" }).some((s) => s.kind === "defect")).toBe(false);
  });

  it("is pure — it neither mutates its input nor returns a shared array", () => {
    const answers: Record<string, CheckState> = { [ALL_ITEM_IDS[0]]: "defect" };
    const first = buildSteps(answers);
    const second = buildSteps(answers);
    expect(answers).toEqual({ [ALL_ITEM_IDS[0]]: "defect" });
    expect(first).not.toBe(second);
    expect(first).toEqual(second);
  });
});

describe("resolveStep", () => {
  it("resolves a known id", () => {
    const steps = buildSteps({});
    const target = checkStepId(ALL_ITEM_IDS[5]);
    expect(resolveStep(steps, target).id).toBe(target);
  });

  it("opens on the odometer with no pointer", () => {
    expect(resolveStep(buildSteps({}), null).kind).toBe("odometer");
  });

  it("falls back to the parent check when a defect step disappears", () => {
    // The real sequence: the driver is on the follow-up for item 4, taps Back, changes the
    // answer to Pass. The follow-up no longer exists. Landing on item 4 is the only sane
    // outcome — throwing would lose the draft, and jumping to step 1 would lose their place.
    const target = ALL_ITEM_IDS[3];
    const steps = buildSteps({ [target]: "pass" });
    expect(resolveStep(steps, defectStepId(target)).id).toBe(checkStepId(target));
  });

  it("falls back to the first step for an id that means nothing", () => {
    expect(resolveStep(buildSteps({}), "check:CHK-NOPE-9").kind).toBe("odometer");
    expect(resolveStep(buildSteps({}), "defect:CHK-NOPE-9").kind).toBe("odometer");
  });
});

describe("progress", () => {
  it("labels the odometer, a check and the review without a fraction lie", () => {
    const steps = buildSteps({});
    expect(progressLabel(steps[0])).toBe("Odometer");
    expect(progressLabel(steps[1])).toBe("Check 1 of 22");
    expect(progressLabel(steps[steps.length - 1])).toBe("Review · 22 of 22");
  });

  it("never exceeds 1 or drops below 0, for any answer combination", () => {
    const answers = Object.fromEntries(
      ALL_ITEM_IDS.map((id, i) => [id, (i % 3 === 0 ? "defect" : "pass") as CheckState]),
    );
    for (const step of buildSteps(answers)) {
      const f = progressFraction(step);
      expect(f).toBeGreaterThanOrEqual(0);
      expect(f).toBeLessThanOrEqual(1);
    }
  });
});
