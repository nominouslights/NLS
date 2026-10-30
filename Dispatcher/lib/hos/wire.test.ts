import { describe, expect, it } from "vitest";
import { BLOCKING_REASONS, DUTY_STATUSES, EVENT_SOURCES, HOS_BANDS, HOS_RULES, HosWire } from "./engine";

// Wire-string pins for the HOS seam.
//
// These strings mirror the constants in Backend/src/Shared/HoursOfService/HosWire.cs — the
// duty and source strings the API emits and accepts, the exception kinds, and the HosRule
// member names that travel verbatim as violation ids (HosWire.RuleId). The Drivers module's
// HosDisplay delegates to the same file. Nothing in TypeScript can catch them drifting: a
// renamed C# constant leaves this app compiling, building and rendering perfectly while every
// HOS read mis-labels a status or every violation id fails to resolve.
//
// If one of these fails, the fix is to check HosWire.cs and change BOTH sides deliberately —
// never to "tidy" the string here. The enum member lists are pinned too, in declaration
// order, because that order is the engine's tie-break order and the fixture JSON spells them.

describe("duty status strings (HosWire.Status)", () => {
  it("are exactly the four HosWire emits, in DutyStatus order", () => {
    expect(DUTY_STATUSES.map((s) => HosWire.status(s))).toEqual(["Off Duty", "On Duty", "Driving", "Personal Conveyance"]);
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
