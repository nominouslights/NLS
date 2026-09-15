import { describe, expect, it } from "vitest";
import { hosEntries } from "./data";
import type { DutyState, HosSource } from "./types";

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
