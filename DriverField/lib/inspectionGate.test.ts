import { beforeEach, describe, expect, it } from "vitest";
import {
  boardingGate,
  deriveResult,
  inspectionDue,
  odometerError,
  severityGlyph,
  severityKind,
} from "./inspectionGate";
import { recordCertification, setAnswer } from "./inspectionStore";
import { eligibility, today } from "./data";
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
    recordCertification({ ...cert, result: "PassWithDefects", defectCount: 2 });
    expect(boardingGate("VEH-11", today).open).toBe(true);
  });

  it("stays CLOSED on a Fail, with requires === null because re-inspecting is not the remedy", () => {
    recordCertification({
      ...cert,
      result: "Fail",
      defectCount: 1,
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
