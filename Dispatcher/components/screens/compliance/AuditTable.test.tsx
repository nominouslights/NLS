import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { HosViolation } from "@/lib/hos/engine";
import { AuditTable, auditCell } from "./AuditTable";
import { auditDay, driver } from "./complianceFixtures";
import { hosException } from "@/components/screens/drivers/hos/hosFixtures";

// The audit grid: one row per driver, one cell per day; each cell spells out events ·
// ✓/◷ certified · ▲ violations · ◐ exceptions · PC km, and a click opens the driver's HOS.

const { getHosAudit } = vi.hoisted(() => ({ getHosAudit: vi.fn() }));
vi.mock("@/lib/api/hos", () => ({ getHosAudit }));

const OCTOBER = { granularity: "month" as const, start: "2026-10-01", end: "2026-10-31" };

const violation: HosViolation = {
  rule: "DailyDriving13h",
  date: "2026-10-09",
  atUnix: 0,
  figureSeconds: 14 * 3600,
  limitSeconds: 13 * 3600,
  suppressed: false,
  suppressedByExceptionId: null,
  message: "Drove 14h",
};

beforeEach(() => getHosAudit.mockReset());
afterEach(cleanup);

describe("auditCell", () => {
  it("is null (rendered —) for a day with no records — unknown, not clean", () => {
    expect(auditCell(undefined)).toBeNull();
    expect(auditCell(auditDay({ hasRecords: false }))).toBeNull();
  });

  it("a clean certified day is ✓ teal", () => {
    expect(auditCell(auditDay())).toMatchObject({ kind: "ontime", lines: ["4 ev", "✓ cert"] });
  });

  it("violations make it ▲ vermillion; exceptions ◐ and PC km are spelled out", () => {
    const cell = auditCell(auditDay({ violations: [violation], exceptions: [hosException()], personalConveyanceKm: 12.5 }))!;
    expect(cell.kind).toBe("over");
    expect(cell.lines).toEqual(["4 ev", "✓ cert", "▲1", "◐1", "PC 12.5 km"]);
    expect(cell.description).toContain("1 violation");
    expect(cell.description).toContain("personal conveyance 12.5 km");
  });

  it("an uncertified day is ◷ gold", () => {
    expect(auditCell(auditDay({ certified: false }))).toMatchObject({ kind: "soon", lines: ["4 ev", "◷ uncert"] });
  });
});

describe("AuditTable", () => {
  it("fetches each driver's month (capped at today), renders cells and totals, and opens the driver on click", async () => {
    getHosAudit.mockImplementation((id: string) =>
      Promise.resolve(
        id === "driver-1"
          ? [auditDay({ date: "2026-10-09" }), auditDay({ date: "2026-10-10", certified: false, violations: [violation] })]
          : [auditDay({ driverId: "driver-2", date: "2026-10-09", eventCount: 2 })],
      ),
    );
    const onOpenDriver = vi.fn();
    const roster = [driver(), driver({ id: "driver-2", name: "M. Spence" })];
    render(<AuditTable roster={roster} period={OCTOBER} onPeriodChange={vi.fn()} today="2026-10-12" version={0} onOpenDriver={onOpenDriver} />);

    await waitFor(() => expect(screen.getAllByTestId("audit-row")).toHaveLength(2));
    expect(getHosAudit).toHaveBeenCalledWith("driver-1", { from: "2026-10-01", to: "2026-10-12" });
    expect(getHosAudit).toHaveBeenCalledWith("driver-2", { from: "2026-10-01", to: "2026-10-12" });

    const cells = screen.getAllByTestId("audit-cell");
    expect(cells).toHaveLength(3);
    const bad = cells.find((c) => c.textContent?.includes("▲1"))!;
    expect(bad.textContent).toContain("◷ uncert");
    expect(bad.getAttribute("aria-label")).toMatch(/K\. Beardy, .*1 violation/);

    fireEvent.click(bad);
    expect(onOpenDriver).toHaveBeenCalledWith("driver-1");

    // Totals for the 9th: 4 + 2 events.
    const totals = screen.getByTestId("audit-totals");
    expect(within(totals).getAllByText("6 ev")).toHaveLength(1);
    expect(within(totals).getByText("▲1")).toBeTruthy();
    expect(within(totals).getByText("◷1")).toBeTruthy();
  });

  it("asks for nothing in a future month", async () => {
    render(
      <AuditTable
        roster={[driver()]}
        period={{ granularity: "month", start: "2026-11-01", end: "2026-11-30" }}
        onPeriodChange={vi.fn()}
        today="2026-10-12"
        version={0}
        onOpenDriver={vi.fn()}
      />,
    );
    expect(screen.getByText(/has not started/)).toBeTruthy();
    expect(getHosAudit).not.toHaveBeenCalled();
  });

  it("names a driver whose audit failed instead of hiding the gap", async () => {
    getHosAudit.mockImplementation((id: string) => (id === "driver-2" ? Promise.reject(new Error("boom")) : Promise.resolve([auditDay()])));
    render(
      <AuditTable
        roster={[driver(), driver({ id: "driver-2", name: "M. Spence" })]}
        period={OCTOBER}
        onPeriodChange={vi.fn()}
        today="2026-10-12"
        version={0}
        onOpenDriver={vi.fn()}
      />,
    );
    await waitFor(() => expect(screen.getByText(/Audit unavailable for 1 driver/)).toBeTruthy());
    expect(screen.getByText(/M\. Spence: Request failed\./)).toBeTruthy();
    expect(screen.getAllByTestId("audit-row")).toHaveLength(1);
  });

  it("steps months through PeriodNav", async () => {
    getHosAudit.mockResolvedValue([]);
    const onPeriodChange = vi.fn();
    render(<AuditTable roster={[driver()]} period={OCTOBER} onPeriodChange={onPeriodChange} today="2026-10-12" version={0} onOpenDriver={vi.fn()} />);
    fireEvent.click(screen.getByLabelText("Previous period"));
    expect(onPeriodChange).toHaveBeenCalledWith({ granularity: "month", start: "2026-09-01", end: "2026-09-30" });
  });
});
