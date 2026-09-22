import { describe, expect, it } from "vitest";
import {
  NL_PTI_01,
  checkCount,
  itemsFor,
  type InspectionFormMode,
  type InspectionItem,
} from "./inspectionForm";

// NL-PTI-01 is a compliance form, not a UI list. Three things about it can break
// silently, and each one has a real-world cost, so each is pinned here:
//
//   1. A duplicate `key`. Keys are wire values — the backend addresses a defect by
//      `(InspectionId, Item)`. Two rows sharing a key collide onto one address and
//      `Enter` fails with `DuplicateDefectItem`, i.e. the driver cannot record the
//      defect at all. This is the most important assertion in the file, and it is
//      the one a copy-paste while transcribing the paper form will trip.
//   2. A filter that narrows the form. Dropping a row the driver should have
//      answered is an unanswered NSC 13 check that nobody notices until an audit.
//   3. `checkCount` drifting from `itemsFor`. The Driver Field App uses it as a
//      progress denominator; if it is ever reimplemented rather than derived, the
//      tablet says "24 of 23 complete" or hides the last checks.
//
// The counts ARE pinned as literals, and the tradeoff is deliberate. Every other
// assertion here is relational (`toBeGreaterThan`, superset), so deleting five Area A
// rows would leave the whole suite green. On a form that exists to satisfy NSC
// Standard 13, a row silently disappearing is the single worst failure mode — worse
// than a red test. Adding a row on purpose costs one number here; losing one by
// accident costs an audit finding. Update these when the form genuinely changes.

const MODES: InspectionFormMode[] = ["PreTrip", "PostTrip"];

function flatten(unit: string | null, mode: InspectionFormMode): InspectionItem[] {
  return itemsFor(unit, mode).flatMap((group) => group.items);
}

/** The six Close-Out labels, verbatim — already on the wire, must not be reworded. */
const CLOSE_OUT_LABELS = [
  "Vehicle exterior — no new damage",
  "All passengers disembarked safely",
  "Vehicle secured / plugged in",
  "Interior cleaned & checked",
  "All cargo delivered / accounted for",
  "Keys returned / secured",
];

describe("wire keys", () => {
  it("is unique across the entire catalogue", () => {
    // A duplicate key collides two rows onto one defect address (InspectionId, Item)
    // and makes the backend's Enter command fail with DuplicateDefectItem. Uniqueness
    // is required across the WHOLE form, not per sub-group, because the address does
    // not include the group.
    const keys = NL_PTI_01.flatMap((group) => group.items.map((item) => item.key));
    const duplicates = keys.filter((key, index) => keys.indexOf(key) !== index);

    expect(duplicates).toEqual([]);
    expect(new Set(keys).size).toBe(keys.length);
  });

  it("keeps the interior light checks distinct from the exterior ones", () => {
    // Same lamps, checked twice (from outside, then from the driver's seat). The
    // labels repeat on purpose; only the "Interior: " key prefix separates them.
    const interior = NL_PTI_01.find((g) => g.key === "Lights & Signals — Interior");
    const exterior = NL_PTI_01.find((g) => g.key === "Lights & Signals — Exterior");

    expect(interior).toBeDefined();
    expect(exterior).toBeDefined();

    const exteriorKeys = new Set(exterior!.items.map((item) => item.key));
    for (const item of interior!.items) {
      expect(item.key.startsWith("Interior: ")).toBe(true);
      expect(exteriorKeys.has(item.key)).toBe(false);
    }
  });

  it("gives every item a non-empty label, key and checkFor", () => {
    for (const group of NL_PTI_01) {
      expect(group.key.trim()).not.toBe("");
      expect(group.title.trim()).not.toBe("");
      for (const item of group.items) {
        expect(item.key.trim(), `key for ${item.label}`).not.toBe("");
        expect(item.label.trim(), `label for ${item.key}`).not.toBe("");
        expect(item.checkFor.trim(), `checkFor for ${item.key}`).not.toBe("");
      }
    }
  });
});

describe("itemsFor — unit scoping", () => {
  it.each(MODES)("excludes every NL02Only item for NL-01 (%s)", (mode) => {
    const items = flatten("NL-01", mode);
    expect(items.length).toBeGreaterThan(0);
    expect(items.filter((item) => item.scope === "NL02Only")).toEqual([]);
  });

  it.each(MODES)("includes every NL02Only item for NL-02 (%s)", (mode) => {
    const nl02Keys = NL_PTI_01.flatMap((group) =>
      group.items
        .filter((item) => item.scope === "NL02Only" && (mode === "PostTrip" || item.mode !== "PostTripOnly"))
        .map((item) => item.key),
    );
    const got = new Set(flatten("NL-02", mode).map((item) => item.key));

    expect(nl02Keys.length).toBeGreaterThan(0);
    for (const key of nl02Keys) expect(got.has(key)).toBe(true);
  });

  it("matches the unit trimmed and case-insensitively", () => {
    const canonical = flatten("NL-01", "PostTrip").map((item) => item.key);
    expect(flatten("  nl-01 ", "PostTrip").map((i) => i.key)).toEqual(canonical);
    expect(flatten("Nl-01", "PostTrip").map((i) => i.key)).toEqual(canonical);
  });

  it.each(MODES)("returns the full superset for null and unknown units (%s)", (mode) => {
    // A null unit must NEVER narrow a compliance form. TripRecord.vehicleUnit is
    // string | null, so an unassigned trip has no unit, and a blank printed form
    // carries "Unit: NL-01 / NL-02" for the driver to tick — inapplicable rows get
    // marked N/A by hand. The fail-safe direction is always MORE questions: an
    // unknown unit must not silently default to the narrower NL-01 form.
    const nl02 = new Set(flatten("NL-02", mode).map((item) => item.key));

    for (const unit of [null, "", "   ", "NL-99", "not-a-unit"]) {
      const got = flatten(unit, mode);
      const gotKeys = new Set(got.map((item) => item.key));
      for (const key of nl02) {
        expect(gotKeys.has(key), `${String(unit)} is missing ${key}`).toBe(true);
      }
      expect(got.length).toBeGreaterThanOrEqual(nl02.size);
      // And strictly more than the NL-01 form, which is the narrowed one.
      expect(got.length).toBeGreaterThan(flatten("NL-01", mode).length);
    }
  });
});

describe("itemsFor — mode scoping", () => {
  it.each(["NL-01", "NL-02", null] as const)(
    "excludes PostTripOnly items from the pre-trip form (%s)",
    (unit) => {
      const items = flatten(unit, "PreTrip");
      expect(items.length).toBeGreaterThan(0);
      expect(items.filter((item) => item.mode === "PostTripOnly")).toEqual([]);
    },
  );

  it.each(["NL-01", "NL-02", null] as const)(
    "includes all six close-out labels on the post-trip form (%s)",
    (unit) => {
      const labels = new Set(flatten(unit, "PostTrip").map((item) => item.label));
      for (const label of CLOSE_OUT_LABELS) expect(labels.has(label)).toBe(true);
    },
  );

  it("drops the close-out group itself from the pre-trip form", () => {
    const preTripGroups = itemsFor("NL-02", "PreTrip").map((group) => group.key);
    expect(preTripGroups).not.toContain("Close-Out");
    expect(itemsFor("NL-02", "PostTrip").map((g) => g.key)).toContain("Close-Out");
  });
});

describe("itemsFor — sub-group shape", () => {
  it("never returns an empty sub-group", () => {
    for (const unit of ["NL-01", "NL-02", null, "NL-99"]) {
      for (const mode of MODES) {
        for (const group of itemsFor(unit, mode)) {
          expect(group.items.length, `${String(unit)}/${mode}: ${group.key}`).toBeGreaterThan(0);
        }
      }
    }
  });

  it("drops the all-NL02 Seating & Cargo group for NL-01", () => {
    // Both of that group's rows are NL02Only, so on NL-01 the whole heading must go
    // rather than print with nothing under it.
    expect(itemsFor("NL-01", "PostTrip").map((g) => g.key)).not.toContain("Seating & Cargo");
    expect(itemsFor("NL-02", "PostTrip").map((g) => g.key)).toContain("Seating & Cargo");
  });

  it("keeps the catalogue's group order", () => {
    const order = NL_PTI_01.map((group) => group.key);
    const got = itemsFor(null, "PostTrip").map((group) => group.key);
    expect(got).toEqual(order);
  });
});

describe("checkCount", () => {
  it.each([
    ["NL-01", "PreTrip"],
    ["NL-01", "PostTrip"],
    ["NL-02", "PreTrip"],
    ["NL-02", "PostTrip"],
  ] as const)("equals the flattened itemsFor length (%s / %s)", (unit, mode) => {
    expect(checkCount(unit, mode)).toBe(flatten(unit, mode).length);
  });

  it("matches the full superset for a null unit", () => {
    for (const mode of MODES) {
      expect(checkCount(null, mode)).toBe(flatten(null, mode).length);
      expect(checkCount(null, mode)).toBe(checkCount("NL-02", mode));
    }
  });

  it("reports the four unit x mode counts", () => {
    const counts = {
      "NL-01 pre-trip": checkCount("NL-01", "PreTrip"),
      "NL-01 post-trip": checkCount("NL-01", "PostTrip"),
      "NL-02 pre-trip": checkCount("NL-02", "PreTrip"),
      "NL-02 post-trip": checkCount("NL-02", "PostTrip"),
    };
    console.log("NL-PTI-01 check counts:", counts);

    expect(counts["NL-01 pre-trip"]).toBeLessThan(counts["NL-01 post-trip"]);
    expect(counts["NL-01 post-trip"]).toBeLessThan(counts["NL-02 post-trip"]);
    expect(counts["NL-02 pre-trip"]).toBeLessThan(counts["NL-02 post-trip"]);
  });

  it("matches the approved NL-PTI-01 row counts exactly", () => {
    // The backstop for silent row loss. Every other count assertion in this file is
    // relational, so removing rows keeps them all green. These four literals are the
    // only thing standing between an accidental deletion and a pre-trip that quietly
    // stops asking about the brakes.
    //
    // 74 mechanical rows; 7 of them NL-02-only (fuel/water separator, entry steps,
    // marker lights, emergency exits, passenger seatbelts, passenger seats, cargo
    // partition), which is the entire NL-01 difference; Close-Out adds 6 post-trip.
    //
    // If the owner changes the form, change these numbers in the same commit and say
    // so in the message. Never "fix" a failure here by relaxing the assertion.
    expect(checkCount("NL-01", "PreTrip")).toBe(67);
    expect(checkCount("NL-01", "PostTrip")).toBe(73);
    expect(checkCount("NL-02", "PreTrip")).toBe(74);
    expect(checkCount("NL-02", "PostTrip")).toBe(80);

    // The arithmetic those four numbers encode, stated so a future edit that changes
    // one without the others fails loudly rather than drifting.
    expect(checkCount("NL-02", "PreTrip") - checkCount("NL-01", "PreTrip")).toBe(7);
    expect(checkCount("NL-02", "PostTrip") - checkCount("NL-02", "PreTrip")).toBe(6);
  });
});
