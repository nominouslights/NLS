import { describe, expect, it } from "vitest";
import { dvirSubmissions, vehicleDefects } from "./data";
import { dutyCommandPayload, dutyEventInputFrom } from "./hos/dutyCommand";
import { DUTY_STATES, dutyStateOf, engineStatusOf } from "./hos/display";
import {
  BLOCKING_REASONS,
  DUTY_STATUSES,
  EVENT_SOURCES,
  HOS_BANDS,
  HOS_RULES,
  HosWire,
} from "./hos/engine";
import {
  checkStatePassed,
  checkStateToWire,
  INSPECTION_SOURCE_WIRE,
  severityToWire,
  type ChecklistItemStateWire,
  type DefectSeverityWire,
} from "./inspectionGate";
import { enqueue, pending } from "./sync/queue";
import { dutyMeta } from "./theme";
import type {
  CheckState,
  DefectSeverity,
  DutyState,
  InspectionMode,
} from "./types";

// Wire-string pins.
//
// These strings cross the API boundary as literals. The HOS block mirrors the constants in
// Backend/src/Shared/HoursOfService/HosWire.cs — the duty and source strings the API emits and
// accepts, the exception kinds, and the HosRule member names that travel verbatim as violation
// ids (HosWire.RuleId). The Drivers module's HosDisplay delegates to the same file. Nothing in
// TypeScript can catch them drifting: a renamed C# constant leaves this app compiling, building
// and rendering perfectly while every HOS submission is rejected, or worse, silently attributed
// to the wrong source.
//
// Same class of test as Budgeting's SERVICE_LINE_LABELS / TripServiceType pin, and the same
// silent failure mode without it. If one of these fails, the fix is to check the C# file and
// change BOTH sides deliberately — never to "tidy" the string here. The enum member lists are
// pinned too, in declaration order, because that order is the engine's tie-break order and the
// fixture JSON spells them. This mirrors Dispatcher/lib/hos/wire.test.ts against this app's
// copy of the engine.

const DUTY_WIRE: DutyState[] = ["Off Duty", "On Duty", "Driving", "Personal Conveyance"];

/** The two sources the legacy per-day route (POST …/hos) accepts; the engine's five are pinned below. */
const LEGACY_SOURCE_WIRE: string[] = ["Driver App", "Manual (paper backup)"];

describe("duty status strings (HosWire.Status)", () => {
  it("are exactly the four HosWire emits, in DutyStatus order", () => {
    expect(DUTY_STATUSES.map((s) => HosWire.status(s))).toEqual(DUTY_WIRE);
    expect(DUTY_WIRE).toEqual(["Off Duty", "On Duty", "Driving", "Personal Conveyance"]);
  });

  it("keep their spacing and casing", () => {
    // "Offduty", "OFF DUTY" and "off duty" are all wrong, and all look fine on screen.
    expect(DUTY_WIRE).not.toContain("OffDuty");
    expect(DUTY_WIRE).not.toContain("off duty");
    expect(DUTY_WIRE.every((d) => d === d.trim())).toBe(true);
  });

  it("round-trip through StatusFrom, trimming as the C# does", () => {
    for (const s of DUTY_STATUSES) {
      expect(HosWire.statusFrom(HosWire.status(s))).toBe(s);
      expect(HosWire.statusFrom(`  ${HosWire.status(s)}  `)).toBe(s);
    }

    expect(HosWire.statusFrom("OffDuty")).toBeNull();
    expect(HosWire.statusFrom("off duty")).toBeNull();
    expect(HosWire.statusFrom(null)).toBeNull();
  });

  it("are the keys the copied theme's dutyMeta() renders, through lib/hos/display.ts both ways", () => {
    // The design system's DutyStatus (lib/theme.ts, a protected copy) spells the same four
    // strings. display.ts's typed Records are the only crossing between the engine's members
    // and those strings, so they are held equal to HosWire here.
    for (const s of DUTY_STATUSES) {
      expect(dutyStateOf(s)).toBe(HosWire.status(s));
      expect(engineStatusOf(dutyStateOf(s))).toBe(s);
    }

    expect([...DUTY_STATES]).toEqual(DUTY_WIRE);
  });

  it("are what the API's DutyStatusWire union spells — the ledger maps server events through statusFrom", () => {
    // lib/hos/ledger.ts turns every server event into an engine event with HosWire.statusFrom;
    // a string the API emits that this cannot parse is an event the tablet silently drops.
    for (const wire of DUTY_WIRE) {
      expect(HosWire.statusFrom(wire)).not.toBeNull();
    }
  });

  it("give every state a glyph and a label, and tell Personal Conveyance from Off Duty without colour", () => {
    // Colour + glyph + label, always all three. PC is off-duty time under the regulation and
    // shares Off Duty's grey by design, so the glyph is the only thing besides the word that
    // separates them — it must differ.
    for (const s of DUTY_WIRE) {
      expect(dutyMeta(s).glyph.trim().length).toBeGreaterThan(0);
    }

    expect(dutyMeta("Personal Conveyance").color).toBe(dutyMeta("Off Duty").color);
    expect(dutyMeta("Personal Conveyance").glyph).not.toBe(dutyMeta("Off Duty").glyph);
  });
});

describe("event source strings (HosWire.Source)", () => {
  it("are exactly the five HosWire emits, in EventSource order", () => {
    // "Manual (paper backup)" is the legacy PaperLog string, kept verbatim — parentheses and a
    // lower-case 'p'. It is the one most likely to be "cleaned up".
    expect(EVENT_SOURCES.map((s) => HosWire.source(s))).toEqual([
      "Driver App",
      "ELD transcription",
      "Manual (paper backup)",
      "Other carrier (declared)",
      "System",
    ]);
  });

  it("round-trip through SourceFrom", () => {
    for (const s of EVENT_SOURCES) {
      expect(HosWire.sourceFrom(HosWire.source(s))).toBe(s);
    }

    expect(HosWire.sourceFrom("Paper backup")).toBeNull();
    expect(HosWire.sourceFrom(undefined)).toBeNull();
  });

  it("include the two the legacy per-day route accepts", () => {
    expect(LEGACY_SOURCE_WIRE).toEqual([HosWire.DriverAppSource, HosWire.PaperLogSource]);
    for (const s of LEGACY_SOURCE_WIRE) {
      expect(EVENT_SOURCES.map((m) => HosWire.source(m))).toContain(s);
    }
  });

  it("distinguish a driver submission from a dispatcher's paper backup", () => {
    // Until recently the API hardcoded EVERY submission to the manual value regardless of
    // origin, so a driver-app entry was recorded as a paper backup keyed by a dispatcher who
    // never touched it. That is a compliance record, not a label.
    expect(HosWire.DriverAppSource).not.toBe(HosWire.PaperLogSource);
    expect(HosWire.sourceFrom(HosWire.DriverAppSource)).toBe("DriverApp");
    expect(HosWire.sourceFrom(HosWire.PaperLogSource)).toBe("PaperLog");
  });
});

describe("the hos.duty command payload", () => {
  it("always carries source 'Driver App' and the queued command's id as clientEventId", async () => {
    // THE MISLABEL BUG, fixed at the only place the payload is built. The tablet never
    // transcribes an ELD or keys a paper log; every event it records is "Driver App". And
    // clientEventId must be the command's own id — the Idempotency-Key the drain() sends and
    // the key the ledger store reconciles the optimistic event on — which only enqueue() can
    // supply, hence the payload factory.
    const id = await enqueue("hos.duty", (commandId) => dutyCommandPayload("Driving", "2026-09-12T08:30:00.000Z", commandId));
    const held = pending().find((c) => c.id === id);
    expect(held?.kind).toBe("hos.duty");
    expect(held?.payload).toEqual({
      duty: "Driving",
      atUtc: "2026-09-12T08:30:00.000Z",
      source: "Driver App",
      clientEventId: id,
    });
  });

  it("spells the duty as the wire string, never the enum member", () => {
    for (const s of DUTY_STATUSES) {
      const payload = dutyCommandPayload(s, "2026-09-12T08:30:00.000Z", "cmd");
      expect(DUTY_WIRE).toContain(payload.duty);
      expect(payload.duty).not.toBe(s === "Driving" ? "driving" : s);
      expect(payload.source).toBe("Driver App");
    }
  });

  it("maps onto DutyEventRequest with the command id as eventId and the source written again", () => {
    // The drain's crossing onto POST …/hos/duty-events. `duty` becomes `status`; the stored
    // payload's source is NOT trusted — "Driver App" is written at send time, so a payload
    // tampered with (or captured by an older build) still cannot reach the API as a paper log.
    const body = dutyEventInputFrom(
      { duty: "Driving", atUtc: "2026-09-12T08:30:00.000Z", source: "Manual (paper backup)", clientEventId: "cmd-9" },
      "cmd-9",
    );
    expect(body).toEqual({ eventId: "cmd-9", atUtc: "2026-09-12T08:30:00.000Z", status: "Driving", source: "Driver App" });
    expect(() => dutyEventInputFrom({ duty: "driving", atUtc: "x", clientEventId: "c" }, "c")).toThrow(/not a duty status/);
    expect(() => dutyEventInputFrom({ tripId: "TRP-1" }, "c")).toThrow(/Not a duty-status/);
  });
});

describe("exception kinds (HosWire.ExceptionKind)", () => {
  it("are exactly the three HosWire emits", () => {
    expect(HosWire.AdverseDrivingConditionsKind).toBe("Adverse driving conditions");
    expect(HosWire.EmergencyKind).toBe("Emergency");
    expect(HosWire.OffDutyDeferralKind).toBe("Off-duty deferral (CO approved)");
    expect(HosWire.OutOfServiceLabel).toBe("Out of service");
  });

  it("resolve from the JSON discriminator the fixtures carry", () => {
    const base = { id: "x", declaredAtUnix: 0, tripId: null, reason: "r" };
    expect(HosWire.exceptionKind({ ...base, kind: "AdverseDrivingConditions", extensionSeconds: 1 })).toBe("Adverse driving conditions");
    expect(HosWire.exceptionKind({ ...base, kind: "Emergency", endedAtUnix: null })).toBe("Emergency");
    expect(HosWire.exceptionKind({ ...base, kind: "OffDutyDeferral", dayOne: "2026-09-13", deferredSeconds: 1, approvedByComplianceOfficer: "co" })).toBe(
      "Off-duty deferral (CO approved)",
    );
  });
});

describe("HosRule member names (HosWire.RuleId)", () => {
  it("are the seventeen members of HosRule, in declaration order", () => {
    // Declaration order is the tie-break order wherever two rules fire at the same instant.
    expect([...HOS_RULES]).toEqual([
      "DailyDriving13h",
      "DailyOnDuty14h",
      "ShiftDriving13h",
      "ShiftOnDuty14h",
      "ShiftElapsed16h",
      "DailyOffDuty10h",
      "Rest24hIn14d",
      "Cycle1_70hIn7d",
      "DeferralConditions",
      "PcExceeds75km",
      "PcOdometerMissing",
      "PcWhileOutOfService",
      "DroveWhileOutOfService",
      "RodsNotCertified",
      "RecordsIncomplete14d",
      "AdverseDeclaredOutsideShift",
      "ConflictingSources",
    ]);
  });

  it("travel verbatim as violation ids and parse back case-sensitively", () => {
    for (const rule of HOS_RULES) {
      expect(HosWire.ruleId(rule)).toBe(rule);
      expect(HosWire.ruleFrom(rule)).toBe(rule);
    }

    expect(HosWire.ruleFrom("dailydriving13h")).toBeNull();
    expect(HosWire.ruleFrom("")).toBeNull();
    expect(HosWire.ruleFrom(null)).toBeNull();
  });
});

describe("the remaining enums the snapshots spell", () => {
  it("pin BlockingReason in reporting order", () => {
    expect([...BLOCKING_REASONS]).toEqual([
      "NoLedger",
      "RecordsIncomplete",
      "OutOfService",
      "Rest24Expired",
      "CycleExhausted",
      "DailyDrivingExhausted",
      "DailyOnDutyExhausted",
      "ShiftDrivingExhausted",
      "ShiftOnDutyExhausted",
      "ShiftWindowClosed",
    ]);
  });

  it("pin HosBand", () => {
    expect([...HOS_BANDS]).toEqual(["Ontime", "Soon", "Over", "Off"]);
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
