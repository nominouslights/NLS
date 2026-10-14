import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import Compliance, { type ComplianceTab } from "./Compliance";
import { driver } from "./compliance/complianceFixtures";
import { PLANNING_LINE } from "@/lib/hos/compliance";

// The frame: the planning-figures line, today's alerts (server sentences verbatim, each with
// its kind's chip), and the five tabs.

const api = vi.hoisted(() => ({
  getHosAlertsToday: vi.fn(),
  getHosAudit: vi.fn(),
  listComplianceFindings: vi.fn(),
  listOutOfService: vi.fn(),
  listEldMalfunctions: vi.fn(),
  listHosExceptions: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => api);
const { listDrivers } = vi.hoisted(() => ({ listDrivers: vi.fn() }));
vi.mock("@/lib/api/drivers", () => ({ listDrivers }));

beforeEach(() => {
  Object.values(api).forEach((f) => f.mockReset().mockResolvedValue([]));
  listDrivers.mockReset().mockResolvedValue([driver()]);
});
afterEach(cleanup);

describe("Compliance", () => {
  it("shows the planning line, the alerts verbatim with chips, and switches tabs", async () => {
    api.getHosAlertsToday.mockResolvedValue({
      asOfUtc: "2026-10-12T14:00:00Z",
      driversNearLimit: 0,
      driversOverLimit: 0,
      outOfService: 1,
      openMalfunctions: 0,
      uncertifiedYesterday: 0,
      unassignedExceptions: 0,
      items: [{ driverId: "driver-1", driverName: "K. Beardy", kind: "OutOfService", message: "K. Beardy is out of service until lifted — Roadside." }],
      label: "Planning figures",
    });
    const setTab = vi.fn();
    const onOpenDriver = vi.fn();
    const props = { setTab, period: { granularity: "month" as const, start: "2026-10-01", end: "2026-10-31" }, setPeriod: vi.fn(), onOpenDriver };
    const { rerender } = render(<Compliance tab="Audit" {...props} />);

    expect(screen.getByTestId("planning-line").textContent).toBe(PLANNING_LINE);
    await waitFor(() => expect(screen.getAllByTestId("alert-item")).toHaveLength(1));
    expect(screen.getByText("K. Beardy is out of service until lifted — Roadside.")).toBeTruthy();
    expect(screen.getByText("Out of service", { selector: "span" }).textContent).toContain("✕");

    fireEvent.click(screen.getAllByRole("button", { name: "K. Beardy" })[0]);
    expect(onOpenDriver).toHaveBeenCalledWith("driver-1");

    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(["Audit", "Findings", "Out of service", "ELD malfunctions", "Deferrals & exceptions"]);
    fireEvent.click(screen.getByRole("tab", { name: "Findings" }));
    expect(setTab).toHaveBeenCalledWith("Findings" satisfies ComplianceTab);

    rerender(<Compliance tab="Findings" {...props} />);
    await waitFor(() => expect(screen.getByText("No compliance findings on file.")).toBeTruthy());
    expect(api.listComplianceFindings).toHaveBeenCalledWith("driver-1");
  });

  it("keeps working when the alerts call fails", async () => {
    api.getHosAlertsToday.mockRejectedValue(new Error("down"));
    render(
      <Compliance tab="Audit" setTab={vi.fn()} period={{ granularity: "month", start: "2026-10-01", end: "2026-10-31" }} setPeriod={vi.fn()} onOpenDriver={vi.fn()} />,
    );
    await waitFor(() => expect(screen.getByText(/Today's alerts unavailable/)).toBeTruthy());
  });
});
