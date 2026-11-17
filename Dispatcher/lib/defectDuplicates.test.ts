import { describe, expect, it } from "vitest";
import { sameDefectItem } from "./api/maintenance";
import {
  knownDefectFor,
  knownDefectsByItem,
  moreSevereDuplicate,
  openReports,
} from "./defectDuplicates";
import { vehicleDefect } from "./defect.fixtures";
import { clearedWithText } from "./workOrderDisplay";

describe("sameDefectItem — the backend's duplicate rule", () => {
  it("is trimmed and case-insensitive, and nothing looser", () => {
    expect(sameDefectItem("Brakes", " brakes ")).toBe(true);
    expect(sameDefectItem("Passenger seats", "PASSENGER SEATS")).toBe(true);
    expect(sameDefectItem("Brakes", "Brake lights")).toBe(false);
    expect(sameDefectItem("Passenger seats", "Passenger seats (NL-02)")).toBe(false);
  });
});

describe("moreSevereDuplicate", () => {
  const minor = vehicleDefect({ item: "Brakes", severity: "Minor" });
  const major = vehicleDefect({ item: "Brakes", severity: "Major" });
  const oos = vehicleDefect({ item: "Brakes", severity: "OutOfService" });

  it("names the worst duplicate strictly more severe than the one resolved", () => {
    expect(moreSevereDuplicate([major, oos], "Minor")).toBe("OutOfService");
    expect(moreSevereDuplicate([minor, major], "Minor")).toBe("Major");
    expect(moreSevereDuplicate([minor, major], "Major")).toBeNull();
    expect(moreSevereDuplicate([oos], "OutOfService")).toBeNull();
    expect(moreSevereDuplicate([], "Minor")).toBeNull();
  });

  it("still flags an Out-of-Service duplicate when the resolved severity is unknown", () => {
    expect(moreSevereDuplicate([minor, oos], undefined)).toBe("OutOfService");
    expect(moreSevereDuplicate([minor, major], null)).toBeNull();
  });
});

describe("knownDefectsByItem — the inspection forms' hints", () => {
  const rows = [
    vehicleDefect({ inspectionId: "i-3", item: "Passenger seats", severity: "Major", reportedAt: "2026-10-03T12:00:00Z", tripNumber: "T-3" }),
    vehicleDefect({ inspectionId: "i-1", item: "passenger seats ", severity: "Minor", reportedAt: "2026-10-01T12:00:00Z", tripNumber: "T-1" }),
    vehicleDefect({ inspectionId: "i-2", item: "Horn", severity: "Minor", reportedAt: "2026-10-02T12:00:00Z" }),
    vehicleDefect({ inspectionId: "i-0", item: "Wipers", resolvedAtUtc: "2026-10-04T00:00:00Z" }),
  ];

  it("groups by normalised item KEY: worst first in server order, open since the earliest report", () => {
    const known = knownDefectsByItem(rows);
    const seats = knownDefectFor(known, "Passenger seats");
    expect(seats?.count).toBe(2);
    expect(seats?.worst.severity).toBe("Major");
    expect(seats?.first.tripNumber).toBe("T-1");
    expect(knownDefectFor(known, "Horn")?.count).toBe(1);
    // Resolved reports are not known faults.
    expect(knownDefectFor(known, "Wipers")).toBeUndefined();
    // The row's LABEL never matches — only its key does.
    expect(knownDefectFor(known, "Passenger seats (NL-02)")).toBeUndefined();
  });

  it("leaves out the defects of the inspection being edited", () => {
    const known = knownDefectsByItem(rows, "i-3");
    expect(knownDefectFor(known, "Passenger seats")?.count).toBe(1);
    expect(knownDefectFor(known, "Passenger seats")?.worst.inspectionId).toBe("i-1");
    expect(openReports(rows, "i-3").map((r) => r.inspectionId)).toEqual(["i-1", "i-2"]);
  });
});

describe("clearedWithText", () => {
  it("names the trip and whether the resolved report was earlier or later", () => {
    expect(clearedWithText({ tripNumber: "T-9", at: "2026-10-05T12:00:00Z" }, "2026-10-03T12:00:00Z")).toBe(
      "cleared with the later report on T-9",
    );
    expect(clearedWithText({ tripNumber: "T-1", at: "2026-10-01T12:00:00Z" }, "2026-10-03T12:00:00Z")).toBe(
      "cleared with the earlier report on T-1",
    );
  });

  it("falls back to the date with no trip, and to plain words when the report is not loaded", () => {
    expect(clearedWithText({ tripNumber: null, at: "2026-10-01T12:00:00Z" }, "2026-10-03T12:00:00Z")).toMatch(
      /^cleared with an earlier report \(/,
    );
    expect(clearedWithText(undefined, "2026-10-03T12:00:00Z")).toBe("cleared with another report of this fault");
  });
});
