import { beforeEach, describe, expect, it } from "vitest";
import {
  boardingGate,
  deriveResult,
  INSPECTION_LOCATION_MAX,
  inspectionDue,
  locationError,
  normalizeLocation,
  odometerError,
  preTripDefectItems,
  severityGlyph,
  severityKind,
} from "./inspectionGate";
import { recordCertification, setAnswer } from "./inspectionStore";
import { dvirSubmissions, eligibility, today } from "./data";
import { statusMeta } from "./theme";
import type { DefectSeverity, Trip } from "./types";

// The boarding gate, and the result rule it rests on.
//
// THE C# THIS MIRRORS, per DriverField/CLAUDE.md's governing rule:
//   VehicleInspection.DeriveResult — Backend/src/Fleet/Domain/Inspections/VehicleInspection.cs
//   (the derivation is also stated on InspectionResult.cs's doc comment).
// A compliance threshold gets tested on BOTH sides of every boundary, the same discipline
// lib/hos.test.ts applies to the CVDHS bands. The boundary that matters here is Minor→Major:
// one Major fails an inspection outright, it does not merely downgrade it.

const cert = {
  commandId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
  mode: "PreTrip" as const,
  vehicleId: "VEH-11",
  onDate: today,
  certifiedAt: `${today}T06:12:00.000Z`,
  result: "Pass" as const,
  defectCount: 0,
  defectItems: [] as string[],
  outOfService: false,
};

beforeEach(() => {
  window.localStorage.clear();
});

describe("deriveResult", () => {
  it("returns Pass with no defects", () => {
    expect(deriveResult([])).toBe("Pass");
  });

  it("returns PassWithDefects for Minor defects only", () => {
    expect(deriveResult(["Minor"])).toBe("PassWithDefects");
    expect(deriveResult(["Minor", "Minor", "Minor"])).toBe("PassWithDefects");
  });

  it("returns Fail for ANY Major — both sides of the boundary", () => {
    expect(deriveResult(["Major"])).toBe("Fail");
    expect(deriveResult(["Minor", "Major"])).toBe("Fail");
    expect(deriveResult(["Major", "Minor"])).toBe("Fail");
  });

  it("returns Fail for an Out of Service defect", () => {
    expect(deriveResult(["Out of Service"])).toBe("Fail");
    expect(deriveResult(["Minor", "Out of Service"])).toBe("Fail");
  });

  it("never returns PassWithDefects once a Major is present", () => {
    // Guards the misreading that "PassWithDefects" is the majority verdict. It is not: it
    // requires that EVERY defect be Minor.
    for (const extra of ["Minor", "Major", "Out of Service"] as DefectSeverity[]) {
      expect(deriveResult(["Major", extra])).toBe("Fail");
    }
  });
});

describe("odometerError", () => {
  // Mirrors Vehicle.RecordOdometer — Backend/src/Fleet/Domain/Vehicles/Vehicle.cs:198-213,
  // whose guard is `odometerKm < OdometerKm` → VehicleErrors.OdometerRollback. Tested on BOTH
  // sides of that boundary, the same discipline lib/hos.test.ts applies: the off-by-one here
  // would either block a legitimate unmoved vehicle or let a rollback through to a 400.
  it("accepts a reading equal to the last recorded one", () => {
    // The guard is `<`, not `<=`. A pre-trip right after a post-trip is the normal case.
    expect(odometerError(184_920, 184_920)).toBeNull();
  });

  it("accepts a reading above the last recorded one", () => {
    expect(odometerError(184_921, 184_920)).toBeNull();
  });

  it("rejects one kilometre below, and names the last reading", () => {
    const reason = odometerError(184_919, 184_920);
    expect(reason).not.toBeNull();
    expect(reason).toContain("184,920");
  });

  it("rejects a negative reading and an empty one, each with its own reason", () => {
    expect(odometerError(-1, 0)).toContain("negative");
    expect(odometerError(null, 184_920)).toContain("Enter the odometer reading");
  });

  it("rejects a fractional reading", () => {
    expect(odometerError(184_920.5, 0)).toContain("whole kilometres");
  });
});

describe("locationError / normalizeLocation", () => {
  // Mirrors VehicleInspection.Create — Backend/src/Fleet/Domain/Inspections/VehicleInspection.cs —
  // which Normalize()s the location (trim, blank → null) and then fails with
  // InspectionErrors.LocationTooLong when `normalizedLocation.Length > LocationMaxLength` (200).
  // Both sides of that boundary, and on the TRIMMED length, because that is what the server
  // measures: padding a 200-character town must not be rejected here when the API accepts it.
  it("pins the limit to VehicleInspection.LocationMaxLength", () => {
    expect(INSPECTION_LOCATION_MAX).toBe(200);
  });

  it("accepts exactly 200 characters and rejects 201", () => {
    expect(locationError("a".repeat(200))).toBeNull();
    expect(locationError("a".repeat(201))).toContain("201 characters");
  });

  it("measures the trimmed value, as the server does", () => {
    expect(locationError(`   ${"a".repeat(200)}   `)).toBeNull();
    expect(normalizeLocation("  Lynn Lake  ")).toBe("Lynn Lake");
  });

  it("requires a location — blank and whitespace-only are both refused", () => {
    // Man. Reg. 95/2008 s.12(1). Nullable on the wire for old-record amends; never blank here.
    expect(locationError("")).toContain("Enter where");
    expect(locationError("   \t ")).toContain("Enter where");
    expect(normalizeLocation("   ")).toBeNull();
  });

  it("accepts a highway description", () => {
    expect(locationError("PTH 391, km 42 north of Thompson")).toBeNull();
  });
});

describe("severity presentation", () => {
  it("maps Major and Out of Service to the same colour", () => {
    // lib/theme.ts is a protected copy — a fifth StatusKind or a new hex is not an option.
    expect(severityKind("Major")).toBe("over");
    expect(severityKind("Out of Service")).toBe("over");
    expect(severityKind("Minor")).toBe("soon");
  });

  it("gives Major and Out of Service DIFFERENT glyphs despite that shared colour", () => {
    // THE assertion for this class of error. Two distinct states sharing a colour AND a glyph
    // would leave the text label as the only differentiator — colour + glyph + label requires
    // all three, and grayscale collapses the colour.
    expect(severityKind("Major")).toBe(severityKind("Out of Service"));
    expect(severityGlyph("Major")).not.toBe(severityGlyph("Out of Service"));
    expect(severityGlyph("Out of Service")).toBe("✕");
  });

  it("takes the kind's default glyph everywhere else, rather than duplicating theme.ts", () => {
    expect(severityGlyph("Minor")).toBe(statusMeta("soon").g);
    expect(severityGlyph("Major")).toBe(statusMeta("over").g);
  });

  it("gives all three severities a distinct glyph", () => {
    const glyphs = (["Minor", "Major", "Out of Service"] as DefectSeverity[]).map(severityGlyph);
    expect(new Set(glyphs).size).toBe(3);
  });
});

describe("boardingGate", () => {
  it("is CLOSED for the assigned vehicle on the seeded data", () => {
    // Also the pin on DVR-8101's date. That row is dated 2026-09-11, not today, deliberately:
    // a gate already satisfied on first paint cannot be demonstrated. Restore the old
    // 2026-09-12 date and this test fails and says why.
    const gate = boardingGate("VEH-11", today);
    expect(gate.open).toBe(false);
    expect(gate.requires).toBe("PreTrip");
  });

  it("names the unit and the day, and admits the server does not enforce it", () => {
    const gate = boardingGate("VEH-11", today);
    expect(gate.reason).toContain("NL-01");
    expect(gate.reason).toContain(today);
    expect(gate.reason).toContain("the server does not enforce it");
  });

  it("gives a non-empty title, reason and shortReason in BOTH directions", () => {
    // EligibilityRule.reason's convention: a rule that cannot say why is a bug, not a gap.
    const closed = boardingGate("VEH-11", today);
    recordCertification(cert);
    const open = boardingGate("VEH-11", today);

    expect(open.open).toBe(true);
    for (const verdict of [closed, open]) {
      expect(verdict.title.trim().length).toBeGreaterThan(0);
      expect(verdict.reason.trim().length).toBeGreaterThan(0);
      expect(verdict.shortReason.trim().length).toBeGreaterThan(0);
    }
  });

  it("opens on a local Pass certification", () => {
    recordCertification(cert);
    expect(boardingGate("VEH-11", today).open).toBe(true);
  });

  it("opens on PassWithDefects — minor defects do not block boarding", () => {
    recordCertification({
      ...cert,
      result: "PassWithDefects",
      defectCount: 2,
      defectItems: ["Tire pressure", "Horn"],
    });
    expect(boardingGate("VEH-11", today).open).toBe(true);
  });

  it("stays CLOSED on a Fail, with requires === null because re-inspecting is not the remedy", () => {
    recordCertification({
      ...cert,
      result: "Fail",
      defectCount: 1,
      defectItems: ["Service brake pedal"],
      outOfService: true,
    });

    const gate = boardingGate("VEH-11", today);
    expect(gate.open).toBe(false);
    expect(gate.requires).toBeNull();
    expect(gate.resumable).toBe(false);
    expect(gate.title).toContain("NL-01");
    expect(gate.reason).toContain("out-of-service");
    expect(gate.reason).toContain("dispatch");
  });

  it("does not let another vehicle's certification open this one", () => {
    recordCertification({ ...cert, vehicleId: "VEH-14" });
    expect(boardingGate("VEH-11", today).open).toBe(false);
    expect(boardingGate("VEH-14", today).open).toBe(true);
  });

  it("does not let yesterday's certification open today", () => {
    recordCertification({ ...cert, onDate: "2026-09-11" });
    expect(boardingGate("VEH-11", today).open).toBe(false);
  });

  it("does not let a POST-trip certification open boarding", () => {
    recordCertification({ ...cert, mode: "PostTrip" });
    expect(boardingGate("VEH-11", today).open).toBe(false);
  });

  it("reports resumable once a draft exists, so the CTA can read Resume", () => {
    expect(boardingGate("VEH-11", today).resumable).toBe(false);
    setAnswer("PreTrip", "VEH-11", "CHK-UH-1", "pass");
    expect(boardingGate("VEH-11", today).resumable).toBe(true);
  });

  it("reads the mock history by mode and vehicleId, not by prose or unit", () => {
    // VEH-16 has a Pre-Trip row dated 2026-09-08 with result "Fail". Asked as of that day the
    // gate must find it and report the failure — which only works if the match is on `mode` and
    // `vehicleId` rather than on `type` ("Pre-Trip") or `unit` ("NL-06").
    const gate = boardingGate("VEH-16", "2026-09-08");
    expect(gate.open).toBe(false);
    expect(gate.requires).toBeNull();
    expect(gate.title).toContain("NL-06");
  });
});

describe("preTripDefectItems — what the post-trip treats as already reported (rev 4)", () => {
  // Mirrors newDefectOptions' `alreadyReported` in Dispatcher/components/inspection/
  // checklistRows.ts, which reads the trip's saved pre-trip. Here "the trip's pre-trip" is the
  // SAME record boardingGate reads, so the gate and the post-trip cannot disagree about it.

  it("is empty when no pre-trip is on record for the vehicle and day", () => {
    // The mock history has no row dated today on purpose (see DVR-8101's comment), so on first
    // paint nothing is excluded — more choices, never fewer.
    expect([...preTripDefectItems("VEH-11", today)]).toEqual([]);
  });

  it("reads this device's pre-trip certification for the vehicle and day", () => {
    recordCertification({
      ...cert,
      result: "PassWithDefects",
      defectCount: 2,
      defectItems: ["Tire pressure", "Horn"],
    });
    expect(preTripDefectItems("VEH-11", today)).toEqual(new Set(["Tire pressure", "Horn"]));
  });

  it("ignores a POST-trip certification, another vehicle and another day", () => {
    recordCertification({ ...cert, mode: "PostTrip", defectItems: ["Tire pressure"] });
    recordCertification({ ...cert, vehicleId: "VEH-14", defectItems: ["Horn"] });
    recordCertification({ ...cert, onDate: "2026-09-11", defectItems: ["Steering"] });
    expect([...preTripDefectItems("VEH-11", today)]).toEqual([]);
  });

  it("reads the mock history's pre-trip by mode, vehicle and day", () => {
    // DVR-8101: VEH-11's pre-trip on 2026-09-11, one Minor defect against "Wipers & washers".
    expect(preTripDefectItems("VEH-11", "2026-09-11")).toEqual(new Set(["Wipers & washers"]));
    // DVR-8102 is that day's POST-trip and must not contribute.
    const post = dvirSubmissions.find((s) => s.id === "DVR-8102");
    expect(post?.mode).toBe("PostTrip");
  });

  it("prefers this device's certification over the mock history, as the gate does", () => {
    recordCertification({ ...cert, onDate: "2026-09-11", defectItems: [] });
    expect([...preTripDefectItems("VEH-11", "2026-09-11")]).toEqual([]);
  });

  it("treats a certification stored before rev 4 (no defectItems) as reporting nothing", () => {
    // CERTIFIED_STORE_VERSION was deliberately not bumped — that would drop this morning's
    // certifications and re-block boarding. A pre-rev-4 entry reads as `defectItems: []`.
    const { defectItems: _omit, ...legacy } = cert;
    void _omit;
    window.localStorage.setItem(
      "nl.driverfield.inspectionCertified",
      JSON.stringify({ v: 2, items: [{ ...legacy, defectCount: 1, result: "PassWithDefects" }] }),
    );
    expect(boardingGate("VEH-11", today).open).toBe(true);
    expect([...preTripDefectItems("VEH-11", today)]).toEqual([]);
  });
});

describe("the mock inspection history", () => {
  it("agrees with deriveResult — each row's prose result matches the defects it lists", () => {
    // The rows now carry their defects (the post-trip reads them), so a row whose `result`
    // contradicts them would demonstrate a rule VehicleInspection.DeriveResult does not have.
    for (const s of dvirSubmissions) {
      const derived = deriveResult(s.defects.map((d) => d.severity));
      const expected =
        derived === "Pass" ? "Pass" : derived === "PassWithDefects" ? "Pass with defects" : "Fail";
      expect(s.result).toBe(expected);
    }
  });
});

describe("inspectionDue", () => {
  it("asks for a pre-trip first, gating, in vermillion", () => {
    const due = inspectionDue("VEH-11", today);
    expect(due.mode).toBe("PreTrip");
    expect(due.label).toBe("Pre-Trip");
    expect(due.badge).toBe("DUE");
    expect(due.badgeKind).toBe("over");
    expect(due.gating).toBe(true);
  });

  it("softens to OPEN in gold once a draft is in progress", () => {
    setAnswer("PreTrip", "VEH-11", "CHK-UH-1", "pass");
    const due = inspectionDue("VEH-11", today);
    expect(due.badge).toBe("OPEN");
    expect(due.badgeKind).toBe("soon");
    expect(due.gating).toBe(true);
  });

  it("flips to a post-trip once the pre-trip is certified, and stops gating", () => {
    recordCertification(cert);
    const due = inspectionDue("VEH-11", today);
    expect(due.mode).toBe("PostTrip");
    expect(due.label).toBe("Post-Trip");
    expect(due.badge).toBe("DUE");
    // Gold, not vermillion: a post-trip owed at end of shift does not block work.
    expect(due.badgeKind).toBe("soon");
    expect(due.gating).toBe(false);
  });

  it("drops the badge entirely once both are done", () => {
    recordCertification(cert);
    recordCertification({ ...cert, mode: "PostTrip", commandId: "post-1" });
    const due = inspectionDue("VEH-11", today);
    expect(due.badge).toBeNull();
    expect(due.label).toBe("Inspection");
    expect(due.gating).toBe(false);
  });
});

describe("the negative pin — this is NOT a sixth §5.4 rule", () => {
  const base: Trip = {
    id: "TRP-TEST",
    tripNumber: "T-TEST",
    mode: "Open",
    serviceType: "Community",
    clientName: "Community Service",
    origin: "Thompson",
    destination: "Leaf Rapids",
    startsAt: "2026-09-12T11:00:00",
    endsAt: "2026-09-12T13:00:00",
    estimatedHours: 2,
    vehicleId: "VEH-11",
    requiredLicenceClass: "4",
    requiresClearance: null,
    seats: 7,
    status: "Open",
    tk: "info",
  };

  it("adds no inspection or boarding rule to eligibility()", () => {
    // DELIBERATE, and the reasons are in lib/inspectionGate.ts's header. The short version:
    // §5.4 asks whether a driver may CLAIM an Open trip; this gate asks whether they may START
    // MOVING CREW on one they already hold. A driver at 21:00 with no pre-trip done is
    // perfectly eligible to claim tomorrow's 06:30 run, and a sixth rule would wrongly block
    // that — as well as greying out every Open trip and destroying the demonstration of rules
    // 1–5. If a future author "tidies" this in, this test is what should stop them.
    const names = eligibility(base).rules.map((r) => r.rule);
    expect(names).toEqual([
      "Hours of service",
      "Licence class",
      "Vehicle status",
      "Schedule conflict",
      "Client clearance",
    ]);
    for (const name of names) {
      expect(name.toLowerCase()).not.toContain("inspection");
      expect(name.toLowerCase()).not.toContain("boarding");
      expect(name.toLowerCase()).not.toContain("pre-trip");
    }
  });

  it("leaves eligibility() unmoved by a local certification", () => {
    // eligibility() never reads dvirSubmissions or inspectionStore, so certifying here cannot
    // change a verdict. If that ever stops being true, the two rules have been entangled.
    const before = eligibility(base).eligible;
    recordCertification({ ...cert, result: "Fail", outOfService: true });
    expect(eligibility(base).eligible).toBe(before);
  });
});
