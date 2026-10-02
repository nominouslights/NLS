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

/**
 * Every key on the form as merged in PR #93 (rev 1, 80 rows), taken verbatim from
 * `origin/main` before rev 2. Stored inspections address their defects by these
 * strings, so none may be renamed or removed — rev 2 changes only `mode`, `checkFor`
 * and `categoryNote` on existing rows.
 */
const REV1_KEYS = [
  "Engine oil",
  "Coolant",
  "Power steering fluid (if equipped)",
  "Brake fluid reservoir (hydraulic)",
  "Washer fluid",
  "Drive belts",
  "Hoses (coolant / heater)",
  "Battery & terminals",
  "Wiring / harness",
  "Fuel / water separator",
  "Radiator / condenser",
  "Ground beneath the vehicle",
  "Block heater cord (winter)",
  "Springs (leaf / coil)",
  "Shock absorbers",
  "U-bolts & spring hangers",
  "Axles & mounting",
  "Wheel bearings",
  "Tread depth",
  "Tire condition",
  "Tire pressure",
  "Wheel nuts / studs",
  "Valve stems & caps",
  "Frame rails / crossmembers",
  "Body panels & doors (exterior)",
  "Windshield & windows",
  "Exterior mirrors (both sides)",
  "Exhaust system",
  "Fuel tank & cap",
  "Entry steps / passenger door",
  "Safety beacon & whip flag",
  "Headlights — low & high beam, both sides",
  "Tail lights",
  "Brake lights (incl. centre high-mount if equipped)",
  "Turn signals — front & rear, both sides",
  "Hazard (4-way) lights",
  "Marker / clearance lights (roof & side)",
  "Reflectors",
  "Back-up lights & reverse alarm (if equipped)",
  "Licence plate light",
  "Driver's seat & seatbelt",
  "Steering",
  "Horn",
  "Gauges (oil pressure, temperature, volt/ammeter, fuel)",
  "Wipers & washers",
  "Defrost / heater",
  "Interior mirrors",
  "Doors (from inside)",
  "Emergency exits / windows",
  "Interior / step lighting",
  "Interior clean & clear of debris",
  "Service brake pedal",
  "Parking brake",
  "Brake warning light",
  "Interior: Headlights (dash switch, low & high)",
  "Interior: Turn signals (left / right)",
  "Interior: Hazard lights",
  "Interior: Brake lights",
  "Fire extinguisher (min. 5 lb, BC-rated)",
  "First aid kit",
  "Warning triangles / reflectors (3)",
  "Passenger seatbelts, all fitted positions",
  "Spill kit (mine requirement)",
  "Survival kit",
  "Traction aids",
  "Extra fuel",
  "Jumper cables / booster pack",
  "Reflective safety vest",
  "Passenger seats",
  "Fitted cargo area — partition & tie-downs",
  "Cell phone charged",
  "Starlink / satellite comm connected",
  "GPS / navigation",
  "Emergency contacts list",
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

  it("still contains every rev-1 key, so no stored defect is orphaned", () => {
    const current = new Set(NL_PTI_01.flatMap((group) => group.items.map((item) => item.key)));
    expect(REV1_KEYS).toHaveLength(80);
    expect(REV1_KEYS.filter((key) => !current.has(key))).toEqual([]);
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

  it.each(MODES)("includes every applicable NL02Only item for NL-02 (%s)", (mode) => {
    const excluded = mode === "PreTrip" ? "PostTripOnly" : "PreTripOnly";
    const nl02Keys = NL_PTI_01.flatMap((group) =>
      group.items
        .filter((item) => item.scope === "NL02Only" && item.mode !== excluded)
        .map((item) => item.key),
    );
    const got = new Set(flatten("NL-02", mode).map((item) => item.key));

    // Rev 2 keeps no NL02Only row on the post-trip, so only the pre-trip list is
    // required to be non-empty.
    if (mode === "PreTrip") expect(nl02Keys.length).toBeGreaterThan(0);
    for (const key of nl02Keys) expect(got.has(key)).toBe(true);
  });

  it("matches the unit trimmed and case-insensitively", () => {
    const canonical = flatten("NL-01", "PreTrip").map((item) => item.key);
    expect(flatten("  nl-01 ", "PreTrip").map((i) => i.key)).toEqual(canonical);
    expect(flatten("Nl-01", "PreTrip").map((i) => i.key)).toEqual(canonical);
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
      // And strictly more than the NL-01 pre-trip, which is the narrowed one. The
      // post-trip has no NL02Only row, so there the two are equal.
      if (mode === "PreTrip") {
        expect(got.length).toBeGreaterThan(flatten("NL-01", mode).length);
      } else {
        expect(got.length).toBe(flatten("NL-01", mode).length);
      }
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
    "excludes PreTripOnly items from the post-trip form (%s)",
    (unit) => {
      const items = flatten(unit, "PostTrip");
      expect(items.length).toBeGreaterThan(0);
      expect(items.filter((item) => item.mode === "PreTripOnly")).toEqual([]);
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

  it.each(["NL-01", "NL-02", null] as const)(
    "puts En-Route Observations on the post-trip only, right before Close-Out (%s)",
    (unit) => {
      // The end-of-day duty under Man. Reg. 95/2008 s.17(2): record defects found
      // while driving. It has nothing to answer before the run.
      expect(itemsFor(unit, "PreTrip").map((g) => g.key)).not.toContain("En-Route Observations");
      const post = itemsFor(unit, "PostTrip").map((g) => g.key);
      expect(post).toContain("En-Route Observations");
      expect(post.indexOf("En-Route Observations")).toBe(post.indexOf("Close-Out") - 1);
      expect(flatten(unit, "PostTrip").map((i) => i.key)).toContain(
        "Defects noticed while driving",
      );
    },
  );

  it("gives every unit the identical post-trip form", () => {
    // No kept post-trip row is NL02Only, so NL-01, NL-02 and an unknown unit answer
    // exactly the same rows in the same order.
    const nl02 = flatten("NL-02", "PostTrip").map((item) => item.key);
    expect(flatten("NL-01", "PostTrip").map((item) => item.key)).toEqual(nl02);
    expect(flatten(null, "PostTrip").map((item) => item.key)).toEqual(nl02);
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

  it("keeps Seating & Cargo on the NL-01 pre-trip for the dangerous goods row", () => {
    // Rev 1's Seating & Cargo was all NL02Only and vanished for NL-01. Rev 2 adds the
    // dangerous-goods row (scope All), so NL-01 keeps the heading with that one row.
    const nl01 = itemsFor("NL-01", "PreTrip").find((g) => g.key === "Seating & Cargo");
    expect(nl01?.items.map((i) => i.key)).toEqual([
      "Dangerous goods documents & placards (if carried)",
    ]);
    // Nothing in Seating & Cargo is on the post-trip, for any unit.
    expect(itemsFor("NL-02", "PostTrip").map((g) => g.key)).not.toContain("Seating & Cargo");
  });

  it("keeps the catalogue's group order", () => {
    const all = NL_PTI_01.map((group) => group.key);
    // An unknown unit's pre-trip has every group except the two post-trip-only ones.
    expect(itemsFor(null, "PreTrip").map((group) => group.key)).toEqual(
      all.filter((key) => key !== "En-Route Observations" && key !== "Close-Out"),
    );
    // The post-trip is a subsequence of the catalogue order.
    const post = itemsFor(null, "PostTrip").map((group) => group.key);
    expect(post).toEqual(all.filter((key) => post.includes(key)));
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

    // Rev 2: the post-trip is the short "can change while driving" set.
    expect(counts["NL-01 post-trip"]).toBeLessThan(counts["NL-01 pre-trip"]);
    expect(counts["NL-02 post-trip"]).toBeLessThan(counts["NL-02 pre-trip"]);
    expect(counts["NL-01 pre-trip"]).toBeLessThan(counts["NL-02 pre-trip"]);
    expect(counts["NL-01 post-trip"]).toBe(counts["NL-02 post-trip"]);
  });

  it("matches the approved NL-PTI-01 row counts exactly", () => {
    // The backstop for silent row loss. Every other count assertion in this file is
    // relational, so removing rows keeps them all green. These literals are the only
    // thing standing between an accidental deletion and a pre-trip that quietly stops
    // asking about the brakes.
    //
    // Rev 2 (NSC 13 Schedule 2 / Man. Reg. 95/2008):
    //   Pre-trip: 82 rows; 11 of them NL-02-only (fuel/water separator, entry steps,
    //   marker lights, emergency exits, passenger seatbelts, passenger seats, cargo
    //   partition, passenger floor, overhead racks, accessibility lift, mobility
    //   restraints), which is the entire NL-01 difference.
    //   Post-trip: 21 "Both" rows that can change while driving + 1 En-Route
    //   Observations row + 6 Close-Out = 28, identical for every unit.
    //
    // If the owner changes the form, change these numbers in the same commit and say
    // so in the message. Never "fix" a failure here by relaxing the assertion.
    expect(checkCount("NL-01", "PreTrip")).toBe(71);
    expect(checkCount("NL-01", "PostTrip")).toBe(28);
    expect(checkCount("NL-02", "PreTrip")).toBe(82);
    expect(checkCount("NL-02", "PostTrip")).toBe(28);
    expect(checkCount(null, "PreTrip")).toBe(82);
    expect(checkCount(null, "PostTrip")).toBe(28);

    // The arithmetic those numbers encode, stated so a future edit that changes one
    // without the others fails loudly rather than drifting.
    const rows = NL_PTI_01.flatMap((g) => g.items);
    expect(checkCount("NL-02", "PreTrip") - checkCount("NL-01", "PreTrip")).toBe(11);
    expect(rows.filter((i) => i.mode === "Both")).toHaveLength(21);
    expect(rows.filter((i) => i.mode === "PostTripOnly")).toHaveLength(7);
    expect(checkCount("NL-02", "PostTrip")).toBe(21 + 7);
  });
});
