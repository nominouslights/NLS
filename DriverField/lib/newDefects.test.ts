import { describe, expect, it } from "vitest";
import {
  newDefectOptions,
  newDefectSeverities,
  newDefectsProblem,
  newDefectsWire,
  optionsForRow,
  reportedNewDefects,
} from "./newDefects";
import { deriveResult } from "./inspectionGate";
import { itemsFor, NL_PTI_01, WITHDRAWN_KEYS } from "./inspectionForm";
import { dvirSubmissions } from "./data";
import type { NewDefectDraft } from "./types";

// The post-trip's "New defects since the pre-trip" (NL-PTI-01 rev 4).
//
// THE CODE THIS MIRRORS, per DriverField/CLAUDE.md's governing rule — client-side, because the
// console's editor is client-side too:
//   Dispatcher/components/inspection/checklistRows.ts — newDefectOptions, newDefectsProblem,
//   newDefectsWire, inspectionResult (and TripInspectionModal's empty-Yes check).
// And the SERVER rules behind them:
//   VehicleInspection.Enter rejects two defects on one item — InspectionErrors.DuplicateDefectItem.
//   VehicleInspection.DeriveResult — any Major fails, Minor only → PassWithDefects.
//
// Two deliberate tablet deltas, pinned below so nobody "aligns" them with the console: the No/Yes
// answer starts unanswered (null), and a new defect's severity starts ungraded (null).

const NL01 = "NL-01";
const NL02 = "NL-02";
const NONE: ReadonlySet<string> = new Set();

function keys(unit: string | null, reported: ReadonlySet<string> = NONE): string[] {
  return newDefectOptions(unit, reported).map((o) => o.key);
}

function preKeys(unit: string | null): string[] {
  return itemsFor(unit, "PreTrip").flatMap((g) => g.items.map((i) => i.key));
}

const OPTIONS_NL01 = newDefectOptions(NL01, NONE);

function d(over: Partial<NewDefectDraft> = {}): NewDefectDraft {
  return { itemKey: "Tire condition", severity: "Minor", note: "Sidewall scuff.", ...over };
}

describe("newDefectOptions", () => {
  it("is every PRE-trip row of the unit, in form order, by catalogue KEY", () => {
    // Filed against the real item key, never free text or a label, so work orders and
    // recurrence tracking still match it.
    expect(keys(NL01)).toEqual(preKeys(NL01));
    expect(keys(NL02)).toEqual(preKeys(NL02));
    const labels = new Map(
      NL_PTI_01.flatMap((g) => g.items.map((i) => [i.key, i.label] as const)),
    );
    for (const o of newDefectOptions(NL02, NONE)) expect(o.label).toBe(labels.get(o.key));
  });

  it("narrows by unit like itemsFor — and gives an unknown unit the superset", () => {
    const nl02Only = preKeys(NL02).filter((k) => !preKeys(NL01).includes(k));
    expect(nl02Only.length).toBeGreaterThan(0);
    for (const k of nl02Only) expect(keys(NL01)).not.toContain(k);
    expect(keys(null)).toEqual(preKeys(null));
    expect(keys("NL-99")).toHaveLength(keys(NL02).length);
  });

  it("never offers a Close-Out row or a withdrawn key", () => {
    // A new defect concerns the vehicle — the pre-trip rows. Close-Out is the post-trip's own
    // checklist, and "Defects noticed while driving" is exactly what rev 4 replaced.
    const closeOut = itemsFor(null, "PostTrip").flatMap((g) => g.items.map((i) => i.key));
    for (const k of [...closeOut, ...WITHDRAWN_KEYS]) expect(keys(null)).not.toContain(k);
  });

  it("leaves out what the pre-trip already reported — a defect it recorded is not new", () => {
    const reported = new Set(["Tire condition", "Horn"]);
    const offered = keys(NL01, reported);
    expect(offered).not.toContain("Tire condition");
    expect(offered).not.toContain("Horn");
    expect(offered).toHaveLength(preKeys(NL01).length - 2);
  });

  it("carries the form's classification for the row as display text", () => {
    const tire = OPTIONS_NL01.find((o) => o.key === "Tire condition");
    expect(tire?.category).toBe("Major");
    expect(tire?.categoryNote).toBe("Major if cord exposed");
    expect(tire?.group).toBe("Tires & Wheels");
  });
});

describe("optionsForRow", () => {
  it("hides an item picked on ANOTHER row — the backend rejects a duplicate (DuplicateDefectItem)", () => {
    const rows = [d({ itemKey: "Tire condition" }), d({ itemKey: "" })];
    expect(optionsForRow(OPTIONS_NL01, rows, 1).map((o) => o.key)).not.toContain("Tire condition");
  });

  it("keeps the row's OWN pick, so its picker still shows what it holds", () => {
    const rows = [d({ itemKey: "Tire condition" }), d({ itemKey: "Horn" })];
    const own = optionsForRow(OPTIONS_NL01, rows, 0).map((o) => o.key);
    expect(own).toContain("Tire condition");
    expect(own).not.toContain("Horn");
  });
});

describe("newDefectsProblem", () => {
  it("blocks an UNANSWERED question — the tablet never defaults it to No", () => {
    // Delta from the console, which starts at No: there the dispatcher transcribes a report the
    // driver already made; here the driver IS making it.
    expect(newDefectsProblem(null, [], OPTIONS_NL01)).toMatch(/Any defect found after the pre-trip/);
  });

  it("passes No — whatever rows were entered before the driver changed their mind", () => {
    expect(newDefectsProblem(false, [], OPTIONS_NL01)).toBeNull();
    expect(newDefectsProblem(false, [d({ itemKey: "", severity: null, note: "" })], OPTIONS_NL01)).toBeNull();
  });

  it("blocks Yes with an empty list — an empty list is not a No", () => {
    expect(newDefectsProblem(true, [], OPTIONS_NL01)).toMatch(/Add the new defect, or answer No/);
  });

  it("blocks a row with no item picked", () => {
    expect(newDefectsProblem(true, [d({ itemKey: "" })], OPTIONS_NL01)).toBe(
      "Pick the item each new defect concerns.",
    );
  });

  it("blocks an item the pre-trip already reported, or one off this unit's pre-trip", () => {
    const reported = newDefectOptions(NL01, new Set(["Tire condition"]));
    expect(newDefectsProblem(true, [d({ itemKey: "Tire condition" })], reported)).toMatch(
      /not a new-defect item/,
    );
    const nl02Only = preKeys(NL02).find((k) => !preKeys(NL01).includes(k)) as string;
    expect(newDefectsProblem(true, [d({ itemKey: nl02Only })], OPTIONS_NL01)).toMatch(
      /not a new-defect item/,
    );
    // The same item, allowed, is fine — both sides of the rule.
    expect(newDefectsProblem(true, [d({ itemKey: "Tire condition" })], OPTIONS_NL01)).toBeNull();
  });

  it("blocks two new defects on one item", () => {
    const rows = [d({ itemKey: "Horn" }), d({ itemKey: "Horn", note: "Again." })];
    expect(newDefectsProblem(true, rows, OPTIONS_NL01)).toMatch(/only one new defect/);
  });

  it("blocks an ungraded row — the tablet never defaults a severity to Minor", () => {
    expect(newDefectsProblem(true, [d({ severity: null })], OPTIONS_NL01)).toBe(
      "Grade each new defect Minor or Major.",
    );
  });

  it("requires a note, and whitespace is not one", () => {
    expect(newDefectsProblem(true, [d({ note: "" })], OPTIONS_NL01)).toMatch(/need a note/);
    expect(newDefectsProblem(true, [d({ note: "   " })], OPTIONS_NL01)).toMatch(/need a note/);
    expect(newDefectsProblem(true, [d({ note: " x " })], OPTIONS_NL01)).toBeNull();
  });

  it("passes a complete list", () => {
    const rows = [d(), d({ itemKey: "Parking brake", severity: "Major", note: "Rolls on grade." })];
    expect(newDefectsProblem(true, rows, OPTIONS_NL01)).toBeNull();
  });
});

describe("reportedNewDefects", () => {
  it("counts the list on Yes only — never on No or unanswered", () => {
    const rows = [d()];
    expect(reportedNewDefects(true, rows)).toEqual(rows);
    expect(reportedNewDefects(false, rows)).toEqual([]);
    expect(reportedNewDefects(null, rows)).toEqual([]);
  });
});

describe("the result over new defects", () => {
  // Mirrors inspectionResult in the console: the checklist's defects and the new ones together,
  // through the same DeriveResult rule. Both sides of the Minor→Major boundary.
  it("is PassWithDefects for a Minor new defect and Fail for a Major one", () => {
    expect(deriveResult(newDefectSeverities([d({ severity: "Minor" })]))).toBe("PassWithDefects");
    expect(deriveResult(newDefectSeverities([d({ severity: "Major" })]))).toBe("Fail");
    expect(deriveResult(newDefectSeverities([d(), d({ severity: "Major" })]))).toBe("Fail");
    expect(deriveResult(newDefectSeverities([]))).toBe("Pass");
  });

  it("ignores an ungraded row rather than guessing its grade", () => {
    expect(newDefectSeverities([d({ severity: null })])).toEqual([]);
  });
});

describe("newDefectsWire", () => {
  it("is an ordinary DefectInput row against the real item key, with the wire severity", () => {
    expect(
      newDefectsWire([
        d({ itemKey: "Tire condition", severity: "Minor", note: "  Sidewall scuff.  " }),
        d({ itemKey: "Parking brake", severity: "Major", note: "Rolls on grade." }),
      ]),
    ).toEqual([
      { item: "Tire condition", severity: "Minor", note: "Sidewall scuff." },
      { item: "Parking brake", severity: "Major", note: "Rolls on grade." },
    ]);
  });

  it("carries nothing beyond item, severity and note — no recurrence pointer, no answer flag", () => {
    for (const row of newDefectsWire([d()])) {
      expect(Object.keys(row).sort()).toEqual(["item", "note", "severity"]);
    }
  });
});

describe("the mock history's defects", () => {
  it("are filed against current pre-trip KEYS for their unit", () => {
    // The post-trip compares these with catalogue keys; a label or a retired string here would
    // silently fail to exclude anything.
    for (const s of dvirSubmissions) {
      for (const defect of s.defects) expect(preKeys(s.unit)).toContain(defect.item);
    }
  });
});
