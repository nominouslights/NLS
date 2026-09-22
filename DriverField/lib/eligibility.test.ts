import { describe, expect, it } from "vitest";
import { eligibility, trips } from "./data";
import type { Trip } from "./types";

// The five §5.4 eligibility rules. This is a CLIENT-SIDE MIRROR of a rule the server must own,
// so the test's job is not to prove the rule is enforced — it isn't, anywhere, yet — but to
// prove each rule can fail independently and that the verdict is conjunctive.
//
// When the backend grows a real eligibility engine and an atomic claim endpoint, this file
// becomes the pin that catches the two drifting apart, exactly as Budgeting's previewPeriod
// tests name the C# method they mirror.

const base: Trip = {
  id: "TRP-TEST",
  tripNumber: "T-TEST",
  mode: "Open",
  serviceType: "Community",
  clientName: "Community Service",
  origin: "Thompson",
  destination: "Leaf Rapids",
  // Well clear of the seeded assigned trips (06:30–09:15 and 17:00–19:45), so the schedule
  // rule passes unless a test deliberately overlaps.
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

function ruleFor(trip: Trip, name: string) {
  const rule = eligibility(trip).rules.find((r) => r.rule === name);
  if (!rule) throw new Error(`no rule named ${name}`);
  return rule;
}

describe("eligibility", () => {
  it("passes every rule for a trip this driver can take", () => {
    const verdict = eligibility(base);
    expect(verdict.rules.every((r) => r.pass)).toBe(true);
    expect(verdict.eligible).toBe(true);
  });

  it("checks exactly the five rules §5.4 specifies", () => {
    // Named rather than counted: a rule silently disappearing is the failure mode that matters,
    // and a count alone would be satisfied by a rename.
    expect(eligibility(base).rules.map((r) => r.rule)).toEqual([
      "Hours of service",
      "Licence class",
      "Vehicle status",
      "Schedule conflict",
      "Client clearance",
    ]);
  });

  it("fails on hours of service alone", () => {
    // The seeded driver has 2h45m driving logged against a 13h limit, so a 13-hour trip cannot
    // fit however else it qualifies.
    const trip = { ...base, estimatedHours: 13 };
    expect(ruleFor(trip, "Hours of service").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("fails on licence class alone", () => {
    // Rule 2 reads the requirement off the trip/vehicle, never a hardcoded class — a future
    // vehicle needing Class 2 must be caught without changing this rule.
    const trip = { ...base, requiredLicenceClass: "2", vehicleId: "VEH-14" };
    expect(ruleFor(trip, "Licence class").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("fails on vehicle status alone", () => {
    // VEH-16 is Out of Service with an outstanding failed DVIR.
    const trip = { ...base, vehicleId: "VEH-16", requiredLicenceClass: "4" };
    expect(ruleFor(trip, "Vehicle status").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("fails on a schedule conflict alone", () => {
    // Overlaps the seeded 06:30–09:15 assigned trip.
    const trip = { ...base, startsAt: "2026-09-12T07:00:00", endsAt: "2026-09-12T08:00:00" };
    expect(ruleFor(trip, "Schedule conflict").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("fails on a missing client clearance alone", () => {
    // The rule that traces to a real incident: a driver without active Alamos clearance must
    // never be eligible for an Alamos-routed trip, automatically, rather than depending on a
    // dispatcher remembering to check.
    const trip = { ...base, requiresClearance: "Vale" };
    expect(ruleFor(trip, "Client clearance").pass).toBe(false);
    expect(eligibility(trip).eligible).toBe(false);
  });

  it("treats a trip needing a clearance the driver holds as eligible", () => {
    expect(ruleFor({ ...base, requiresClearance: "Alamos" }, "Client clearance").pass).toBe(true);
  });

  it("is conjunctive — eligible only when every rule passes", () => {
    const trip = { ...base, estimatedHours: 13, requiredLicenceClass: "2" };
    const verdict = eligibility(trip);
    expect(verdict.rules.filter((r) => !r.pass)).toHaveLength(2);
    expect(verdict.eligible).toBe(false);
  });

  it("gives every rule a reason, passing or failing", () => {
    // The scaffold greys ineligible trips and names the failing rule rather than hiding them
    // (a deliberate deviation from §5.4 — see lib/data.ts). That only works if every rule can
    // say why, so an empty reason is a bug, not a cosmetic gap.
    for (const trip of trips) {
      for (const rule of eligibility(trip).rules) {
        expect(rule.reason.trim().length).toBeGreaterThan(0);
      }
    }
  });
});
