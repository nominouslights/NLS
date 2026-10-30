import { describe, expect, it } from "vitest";
import { addDays, evaluate, HosCalendar, HosMessages, HOS_LIMITS, type DutyLedger } from "./engine";

// Hand tests for the parts of the mirror the fixture theory exercises only indirectly: the
// calendar's DST arithmetic (HosCalendar.cs), the prose formatter (HosMessages.cs), and the
// empty-ledger path. The fixture theory in engine.fixtures.test.ts is the mirror guarantee;
// these name the mechanism so a failure there has a smaller place to look.

const WINNIPEG = new HosCalendar();

/** Unix seconds of a wall-clock instant in America/Winnipeg, by brute force through the calendar. */
function winnipeg(date: string, secondsIntoDay: number): number {
  return WINNIPEG.dayStartUnix(date) + secondsIntoDay;
}

describe("HosCalendar in America/Winnipeg", () => {
  it("makes the spring-forward day 23 hours long and the fall-back day 25", () => {
    // HosCalendar.cs: DST days are 23 or 25 hours; the engine cuts pieces at these boundaries.
    expect(WINNIPEG.dayEndUnix("2026-03-08") - WINNIPEG.dayStartUnix("2026-03-08")).toBe(23 * 3600);
    expect(WINNIPEG.dayEndUnix("2026-11-01") - WINNIPEG.dayStartUnix("2026-11-01")).toBe(25 * 3600);
    expect(WINNIPEG.dayEndUnix("2026-09-14") - WINNIPEG.dayStartUnix("2026-09-14")).toBe(24 * 3600);
  });

  it("starts the fixtures' base day where the C# catalogue says (2026-09-14 00:00 CDT = UTC-5)", () => {
    // dd-01's first OnDuty event is at 06:00 on D and the JSON carries 1789383600; the day
    // start is therefore 1789383600 - 6h.
    expect(WINNIPEG.dayStartUnix("2026-09-14")).toBe(1789383600 - 6 * 3600);
    expect(WINNIPEG.dayStartUnix("2026-09-14")).toBe(Date.UTC(2026, 8, 14, 5) / 1000);
  });

  it("maps the last second of a day to that day and the next second to the next", () => {
    const end = WINNIPEG.dayEndUnix("2026-03-08");
    expect(WINNIPEG.dayOf(end - 1)).toBe("2026-03-08");
    expect(WINNIPEG.dayOf(end)).toBe("2026-03-09");
    const fallEnd = WINNIPEG.dayEndUnix("2026-11-01");
    expect(WINNIPEG.dayOf(fallEnd - 1)).toBe("2026-11-01");
    expect(WINNIPEG.dayOf(fallEnd)).toBe("2026-11-02");
  });

  it("honours a carrier-designated day start", () => {
    // s.1: the 24-hour period starts at the time the carrier designates. 04:00 local.
    const cal = new HosCalendar("America/Winnipeg", 4 * 3600);
    expect(cal.dayStartUnix("2026-09-14")).toBe(WINNIPEG.dayStartUnix("2026-09-14") + 4 * 3600);
    expect(cal.dayOf(WINNIPEG.dayStartUnix("2026-09-14") + 3 * 3600)).toBe("2026-09-13");
    expect(cal.dayOf(WINNIPEG.dayStartUnix("2026-09-14") + 4 * 3600)).toBe("2026-09-14");
  });

  it("moves a day start that falls inside the spring-forward gap to the first valid instant after it", () => {
    // 02:30 does not exist on 2026-03-08 in Winnipeg; HosCalendar.cs adds an hour (03:30 CDT).
    const cal = new HosCalendar("America/Winnipeg", 2 * 3600 + 1800);
    const expected = WINNIPEG.dayStartUnix("2026-03-08") + 2 * 3600 + 1800; // 02:30 CST wall = 03:30 CDT, the same instant
    expect(cal.dayStartUnix("2026-03-08")).toBe(expected);
  });

  it("resolves an ambiguous fall-back day start to standard time, as TimeZoneInfo.GetUtcOffset does", () => {
    // 01:30 happens twice on 2026-11-01. .NET picks the standard-offset instant (the later one).
    const cal = new HosCalendar("America/Winnipeg", 3600 + 1800);
    const firstOneThirty = WINNIPEG.dayStartUnix("2026-11-01") + 3600 + 1800; // CDT
    expect(cal.dayStartUnix("2026-11-01")).toBe(firstOneThirty + 3600);
  });

  it("rejects a day start outside one local day", () => {
    expect(() => new HosCalendar("America/Winnipeg", 86400)).toThrow();
    expect(() => new HosCalendar("America/Winnipeg", -1)).toThrow();
  });

  it("never resolves a zone through the machine's own clock", () => {
    // The same instant is a different date in Winnipeg and in Auckland; both are pure
    // functions of the IANA id, so neither depends on where the test runs.
    const t = winnipeg("2026-09-14", 23 * 3600);
    expect(WINNIPEG.dayOf(t)).toBe("2026-09-14");
    expect(new HosCalendar("Pacific/Auckland").dayOf(t)).toBe("2026-09-15");
  });
});

describe("addDays", () => {
  it("crosses month and year boundaries", () => {
    expect(addDays("2026-09-14", -13)).toBe("2026-09-01");
    expect(addDays("2026-12-31", 1)).toBe("2027-01-01");
    expect(addDays("2028-03-01", -1)).toBe("2028-02-29");
  });
});

describe("HosMessages.duration", () => {
  it("formats as the C# does, with seconds only when non-zero", () => {
    expect(HosMessages.duration(46800)).toBe("13h 0m");
    expect(HosMessages.duration(46801)).toBe("13h 0m 1s");
    expect(HosMessages.duration(0)).toBe("0h 0m");
    expect(HosMessages.duration(-90)).toBe("-0h 1m 30s");
  });

  it("writes the personal-conveyance limit in tenths of a kilometre", () => {
    expect(HosMessages.for("PcExceeds75km", 751, HOS_LIMITS.pcDailyLimitDecikm)).toBe(
      "Personal conveyance 75.1 km exceeds the 75.0 km daily limit; the remainder counts as driving (s.1).",
    );
  });
});

describe("evaluate on an empty ledger", () => {
  it("is band Off, blocked by NoLedger and RecordsIncomplete, with nothing to plan around", () => {
    const ledger: DutyLedger = {
      driverId: "d",
      knownFromUnix: winnipeg("2026-08-25", 0),
      events: [],
      otherCarrierDays: [],
      exceptions: [],
      certifiedDays: [],
      outOfService: [],
      malfunctions: [],
    };
    const e = evaluate(ledger, winnipeg("2026-09-14", 10 * 3600), WINNIPEG);
    expect(e.band).toBe("Off");
    expect(e.canDriveNow).toBe(false);
    expect(e.blockingReasons).toEqual(["NoLedger", "RecordsIncomplete"]);
    expect(e.nextRequiredOffDuty).toBeNull();
    expect(e.tightestRemainingSeconds).toBe(0);
    expect(e.violations.map((v) => v.rule)).toEqual(["RecordsIncomplete14d"]);
  });

  it("reports a known-from after as-of as a ledger problem rather than throwing", () => {
    const ledger: DutyLedger = {
      driverId: "d",
      knownFromUnix: winnipeg("2026-09-15", 0),
      events: [],
      otherCarrierDays: [],
      exceptions: [],
      certifiedDays: [],
      outOfService: [],
      malfunctions: [],
    };
    const e = evaluate(ledger, winnipeg("2026-09-14", 0), WINNIPEG);
    expect(e.ledgerProblems.map((p) => p.code)).toContain("KnownFromAfterAsOf");
  });
});
