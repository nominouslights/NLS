import { describe, expect, it } from "vitest";
import { dvirSubmissions, hosEntries, vehicleDefects } from "./data";
import {
  checkStatePassed,
  checkStateToWire,
  INSPECTION_SOURCE_WIRE,
  severityToWire,
  type ChecklistItemStateWire,
  type DefectSeverityWire,
} from "./inspectionGate";
import type {
  CheckState,
  DefectSeverity,
  DutyState,
  HosSource,
  InspectionMode,
} from "./types";

// Wire-string pins.
//
// These strings cross the API boundary as literals. They mirror the constants in
// Backend/src/Drivers/Application/Hos/HosDisplay.cs — DutyToWire/DutyFromWire and
// SourceToWire/SourceFromWire — and nothing in TypeScript can catch them drifting: a renamed
// C# constant leaves this app compiling, building and rendering perfectly while every HOS
// submission is rejected, or worse, silently attributed to the wrong source.
//
// Same class of test as Budgeting's SERVICE_LINE_LABELS / TripServiceType pin, and the same
// silent failure mode without it. If one of these fails, the fix is to check the C# file and
// change BOTH sides deliberately — never to "tidy" the string here.

const DUTY_WIRE: DutyState[] = ["Off Duty", "On Duty", "Driving"];

const SOURCE_WIRE: HosSource[] = ["Driver App", "Manual (paper backup)"];

describe("HOS duty strings", () => {
  it("are exactly the three HosDisplay emits", () => {
    expect(DUTY_WIRE).toEqual(["Off Duty", "On Duty", "Driving"]);
  });

  it("keep their spacing and casing", () => {
    // "Offduty", "OFF DUTY" and "off duty" are all wrong, and all look fine on screen.
    expect(DUTY_WIRE).not.toContain("OffDuty");
    expect(DUTY_WIRE).not.toContain("off duty");
    expect(DUTY_WIRE.every((d) => d === d.trim())).toBe(true);
  });

  it("cover every duty value in the mock layer", () => {
    for (const e of hosEntries) {
      expect(DUTY_WIRE).toContain(e.duty);
    }
  });
});

describe("HOS source strings", () => {
  it("are exactly the two HosDisplay emits", () => {
    // "Manual (paper backup)" carries parentheses and a lower-case 'p'. It is the string most
    // likely to be "cleaned up" by someone who has never seen HosDisplay.
    expect(SOURCE_WIRE).toEqual(["Driver App", "Manual (paper backup)"]);
  });

  it("distinguish a driver submission from a dispatcher's paper backup", () => {
    // Until recently the API hardcoded EVERY submission to the manual value regardless of
    // origin, so a driver-app entry was recorded as a paper backup keyed by a dispatcher who
    // never touched it. That is a compliance record, not a label.
    expect(SOURCE_WIRE[0]).not.toBe(SOURCE_WIRE[1]);
    const appEntries = hosEntries.filter((e) => e.source === "Driver App");
    const manualEntries = hosEntries.filter((e) => e.source === "Manual (paper backup)");
    expect(appEntries.length).toBeGreaterThan(0);
    expect(manualEntries.length).toBeGreaterThan(0);
  });

  it("pairs a manual entry with the person who keyed it, and an app entry with nobody", () => {
    // EnteredBy is documented as the dispatcher's name, required for the manual path. A
    // driver-app submission has no dispatcher, so the field must be absent rather than
    // invented — the backend's validation branches on exactly this.
    for (const e of hosEntries) {
      if (e.source === "Manual (paper backup)") expect(e.enteredBy).toBeTruthy();
      else expect(e.enteredBy).toBeNull();
    }
  });
});

// --- inspections -----------------------------------------------------------
//
// The DVIR wire strings, mirroring three enums in Backend/src/Fleet/Domain/Inspections/:
// InspectionDefectSeverity.cs, InspectionType.cs and InspectionSource.cs. These are serialised
// by the default System.Text.Json enum converter, which matches MEMBER NAMES — so a display
// string with a space in it is rejected outright, and a plausible-looking near-miss like
// "Pre-Trip" is rejected the same way. Nothing in TypeScript can catch it: the app compiles,
// builds and renders perfectly while every submission 400s.

const SEVERITY_WIRE: DefectSeverityWire[] = ["Minor", "Major", "OutOfService"];

const INSPECTION_TYPE_WIRE: InspectionMode[] = ["PreTrip", "PostTrip"];

const ALL_SEVERITIES: DefectSeverity[] = ["Minor", "Major", "Out of Service"];

describe("defect severity strings", () => {
  it("are exactly InspectionDefectSeverity's three members", () => {
    expect(SEVERITY_WIRE).toEqual(["Minor", "Major", "OutOfService"]);
  });

  it("maps the display string 'Out of Service' to 'OutOfService'", () => {
    // THE latent bug this block was added for. lib/types.ts's DefectSeverity is a DISPLAY type
    // ("Out of Service", with spaces) because that is what a driver reads; the enum member has
    // none. Sending the display string straight through is a 400 nobody would see until a
    // driver in a dead zone could not file a defect.
    expect(severityToWire("Out of Service")).toBe("OutOfService");
    expect(severityToWire("Minor")).toBe("Minor");
    expect(severityToWire("Major")).toBe("Major");
  });

  it("emits no wire value containing a space", () => {
    for (const severity of ALL_SEVERITIES) {
      expect(severityToWire(severity)).not.toContain(" ");
      expect(SEVERITY_WIRE).toContain(severityToWire(severity));
    }
  });

  it("covers every severity in the mock layer", () => {
    // Same coverage shape as the duty-value check above: a severity that appears on screen but
    // cannot cross the boundary is a screen that cannot be wired up.
    for (const defect of vehicleDefects) {
      expect(SEVERITY_WIRE).toContain(severityToWire(defect.severity));
    }
  });
});

describe("inspection type strings", () => {
  it("are exactly InspectionType's two members", () => {
    expect(INSPECTION_TYPE_WIRE).toEqual(["PreTrip", "PostTrip"]);
  });

  it("are not the display prose the screen shows", () => {
    // DvirSubmission carries BOTH: `type` is prose for the driver, `mode` is the wire value the
    // boarding gate and the payload branch on. Sending the prose is the same class of failure
    // as sending "Out of Service".
    expect(INSPECTION_TYPE_WIRE).not.toContain("Pre-Trip" as unknown as InspectionMode);
    expect(INSPECTION_TYPE_WIRE).not.toContain("Post-Trip" as unknown as InspectionMode);
    expect(INSPECTION_TYPE_WIRE.every((t) => !t.includes("-"))).toBe(true);
  });

  it("covers every mode in the mock layer, and each row's prose matches its mode", () => {
    for (const submission of dvirSubmissions) {
      expect(INSPECTION_TYPE_WIRE).toContain(submission.mode);
      expect(submission.type.replace("-", "")).toBe(submission.mode);
    }
  });
});

const CHECKLIST_STATE_WIRE: ChecklistItemStateWire[] = ["Ok", "Defect", "NotApplicable"];

const ALL_CHECK_STATES: CheckState[] = ["pass", "defect", "na"];

describe("checklist item state strings", () => {
  it("are exactly ChecklistItemState's three members", () => {
    // Backend/src/Fleet/Domain/Inspections/ChecklistItemState.cs. NL-PTI-01's three boxes, in
    // declaration order. Before this enum existed there was only a `Passed` bool, and this app
    // OMITTED every N/A row from a submission rather than assert `passed: true` for a row that
    // does not apply — a false attestation in a compliance record.
    expect(CHECKLIST_STATE_WIRE).toEqual(["Ok", "Defect", "NotApplicable"]);
  });

  it("maps the screen's lowercase draft values onto them", () => {
    // The screen's values are lowercase because they key a localStorage draft; the wire's are
    // C# member names. Sending "na" or "pass" is the same class of failure as sending
    // "Out of Service".
    expect(checkStateToWire("pass")).toBe("Ok");
    expect(checkStateToWire("defect")).toBe("Defect");
    expect(checkStateToWire("na")).toBe("NotApplicable");
  });

  it("emits no wire value containing a space, for any answer", () => {
    for (const state of ALL_CHECK_STATES) {
      expect(checkStateToWire(state)).not.toContain(" ");
      expect(CHECKLIST_STATE_WIRE).toContain(checkStateToWire(state));
    }
  });

  it("derives `passed` exactly as VehicleInspection.NormalizeChecklist does", () => {
    // VehicleInspection.cs:510 — `Passed = state != ChecklistItemState.Defect`. ChecklistItemInput
    // still requires the bool, so the client sends both; deriving it any other way would let the
    // two disagree. NOTE THE N/A CASE: `passed: true`, which is only honest because `state`
    // travels with it.
    expect(checkStatePassed("pass")).toBe(true);
    expect(checkStatePassed("na")).toBe(true);
    expect(checkStatePassed("defect")).toBe(false);

    for (const state of ALL_CHECK_STATES) {
      expect(checkStatePassed(state)).toBe(checkStateToWire(state) !== "Defect");
    }
  });
});

describe("inspection source string", () => {
  it("is exactly 'DriverApp'", () => {
    // The backend defaults Source to Dispatcher. A driver submission must say so explicitly or
    // the inspection is attributed to a dispatcher who never touched the vehicle — the same
    // failure the HOS source pin above exists for, in a compliance record.
    expect(INSPECTION_SOURCE_WIRE).toBe("DriverApp");
    expect(INSPECTION_SOURCE_WIRE).not.toBe("Driver App");
    expect(INSPECTION_SOURCE_WIRE).not.toContain(" ");
  });
});
