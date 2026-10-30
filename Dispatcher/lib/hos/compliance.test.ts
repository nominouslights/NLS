import { describe, expect, it } from "vitest";
import type { HosAlertsToday } from "../api/hos";
import { alertsTileSpec, datesBetween, daysOnPaper, fanOut, FINDING_RULES, nextFindingStatuses } from "./compliance";

function alerts(overrides: Partial<HosAlertsToday> = {}): HosAlertsToday {
  return {
    asOfUtc: "2026-10-12T14:00:00Z",
    driversNearLimit: 0,
    driversOverLimit: 0,
    outOfService: 0,
    openMalfunctions: 0,
    uncertifiedYesterday: 0,
    unassignedExceptions: 0,
    items: [],
    label: "Planning figures",
    ...overrides,
  };
}

describe("fanOut", () => {
  it("keeps input order, caps concurrency, and captures failures per item", async () => {
    let inFlight = 0;
    let peak = 0;
    const results = await fanOut(
      [1, 2, 3, 4, 5, 6, 7, 8, 9, 10],
      async (n) => {
        inFlight++;
        peak = Math.max(peak, inFlight);
        await new Promise((r) => setTimeout(r, 5));
        inFlight--;
        if (n === 4) throw new Error("Driver not found.");
        return n * 10;
      },
      3,
    );
    expect(peak).toBe(3);
    expect(results.map((r) => r.item)).toEqual([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
    expect(results[3]).toEqual({ item: 4, ok: false, error: "Driver not found." });
    expect(results[9]).toEqual({ item: 10, ok: true, value: 100 });
  });

  it("returns nothing for an empty roster", async () => {
    expect(await fanOut([], async () => 1)).toEqual([]);
  });
});

describe("alertsTileSpec — the board tile", () => {
  it("sums all six counts", () => {
    const spec = alertsTileSpec(
      alerts({ driversNearLimit: 1, driversOverLimit: 2, outOfService: 1, openMalfunctions: 1, uncertifiedYesterday: 3, unassignedExceptions: 1 }),
    );
    expect(spec.total).toBe(9);
    expect(spec.breakdown).toBe("1 out of service · 2 over limit · 1 near limit · 1 ELD malfunction · 3 uncertified · 1 exception");
  });

  it("is vermillion ▲ when anyone is over a limit or out of service", () => {
    expect(alertsTileSpec(alerts({ driversOverLimit: 1 }))).toMatchObject({ kind: "over", glyph: "▲", total: 1 });
    expect(alertsTileSpec(alerts({ outOfService: 1, driversNearLimit: 2 }))).toMatchObject({ kind: "over", total: 3 });
  });

  it("is gold ◐ for near-limit, malfunctions, uncertified or exceptions alone", () => {
    for (const o of [{ driversNearLimit: 1 }, { openMalfunctions: 2 }, { uncertifiedYesterday: 1 }, { unassignedExceptions: 1 }]) {
      expect(alertsTileSpec(alerts(o))).toMatchObject({ kind: "soon", glyph: "◐" });
    }
  });

  it("is teal ✓ \"0\" — All clear — when nothing is raised", () => {
    expect(alertsTileSpec(alerts())).toEqual({ total: 0, kind: "ontime", glyph: "✓", breakdown: "All clear" });
  });
});

describe("findings", () => {
  it("only moves forward: Open → Corrected/Closed, Corrected → Closed, Closed is final", () => {
    expect(nextFindingStatuses("Open")).toEqual(["Corrected", "Closed"]);
    expect(nextFindingStatuses("Corrected")).toEqual(["Closed"]);
    expect(nextFindingStatuses("Closed")).toEqual([]);
  });

  it("offers the engine's rules plus the four non-engine ones", () => {
    expect(FINDING_RULES).toContain("DailyDriving13h");
    expect(FINDING_RULES.slice(-4)).toEqual(["Falsification", "Tampering", "TwoRecords", "OutOfServiceDeclared"]);
  });
});

describe("dates", () => {
  it("daysOnPaper counts local (Winnipeg) days from noticed to today", () => {
    // 2026-10-09 23:30 CDT is 2026-10-10 04:30Z — still the 9th locally.
    expect(daysOnPaper("2026-10-10T04:30:00Z", "2026-10-12")).toBe(3);
    expect(daysOnPaper("2026-10-12T15:00:00Z", "2026-10-12")).toBe(0);
  });

  it("datesBetween is inclusive and crosses month ends", () => {
    expect(datesBetween("2026-09-29", "2026-10-02")).toEqual(["2026-09-29", "2026-09-30", "2026-10-01", "2026-10-02"]);
  });
});
