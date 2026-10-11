import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { EldMalfunctionPanel, PAPER_LOG_CHIP } from "./EldMalfunctionPanel";
import { eldMalfunction } from "./hosFixtures";

// ELD malfunctions: an open report is paper-log mode — the caution colour with the ✎ glyph,
// never the kind's default ◐, so it reads apart from "records incomplete" at a glance.
// RESOLVE is compliance-only; REPORT posts the exact body.

const { listEldMalfunctions, reportEldMalfunction, resolveEldMalfunction } = vi.hoisted(() => ({
  listEldMalfunctions: vi.fn(),
  reportEldMalfunction: vi.fn(),
  resolveEldMalfunction: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => ({ listEldMalfunctions, reportEldMalfunction, resolveEldMalfunction }));

function renderPanel(canCompliance: boolean, onChanged = vi.fn()) {
  render(<EldMalfunctionPanel driverId="driver-1" driverName="K. Beardy" version={0} canCompliance={canCompliance} onChanged={onChanged} />);
  return { onChanged };
}

beforeEach(() => {
  listEldMalfunctions.mockReset();
  reportEldMalfunction.mockReset();
  resolveEldMalfunction.mockReset();
});

afterEach(cleanup);

describe("EldMalfunctionPanel", () => {
  it("the paper-log chip is the caution kind with ✎", () => {
    expect(PAPER_LOG_CHIP).toEqual({ kind: "soon", glyph: "✎", label: "Paper-log mode" });
  });

  it("shows an open report as paper-log mode (✎) and a resolved one as ✓", async () => {
    listEldMalfunctions.mockResolvedValueOnce([
      eldMalfunction(),
      eldMalfunction({ id: "eld-0", code: "E-07", status: "Resolved", resolvedAtUtc: "2026-10-08T20:00:00Z", resolutionNote: "Unit swapped" }),
    ]);
    renderPanel(false);

    await waitFor(() => expect(screen.getAllByTestId("eld-malfunction-row")).toHaveLength(2));
    const open = screen.getByText("Open — paper-log mode");
    expect(open.parentElement?.textContent).toContain("✎");
    expect(open.parentElement?.textContent).not.toContain(statusMeta("soon").g);
    const resolved = screen.getByText(/^Resolved/);
    expect(resolved.parentElement?.textContent).toContain(statusMeta("ontime").g);
    expect(screen.getByText(/Resolution: Unit swapped/)).toBeTruthy();
  });

  it("offers Resolve on an open report to compliance roles only", async () => {
    listEldMalfunctions.mockResolvedValue([eldMalfunction()]);
    const { unmount } = render(<EldMalfunctionPanel driverId="driver-1" driverName="K. Beardy" version={0} canCompliance={false} onChanged={vi.fn()} />);
    await waitFor(() => expect(screen.getByText("P-01")).toBeTruthy());
    expect(screen.queryByText("Resolve")).toBeNull();
    unmount();

    const onChanged = vi.fn();
    render(<EldMalfunctionPanel driverId="driver-1" driverName="K. Beardy" version={0} canCompliance onChanged={onChanged} />);
    await waitFor(() => expect(screen.getByText("Resolve")).toBeTruthy());
    resolveEldMalfunction.mockResolvedValueOnce(eldMalfunction({ status: "Resolved" }));

    fireEvent.click(screen.getByText("Resolve"));
    fireEvent.change(screen.getByPlaceholderText(/Repaired/), { target: { value: "Reset by vendor" } });
    fireEvent.click(screen.getByText("RESOLVE"));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(resolveEldMalfunction).toHaveBeenCalledWith("driver-1", "eld-1", "Reset by vendor");
  });

  it("REPORT MALFUNCTION requires the code and description and posts the body", async () => {
    listEldMalfunctions.mockResolvedValueOnce([]);
    const { onChanged } = renderPanel(false);
    await waitFor(() => expect(screen.getByText(/No ELD malfunction reports/)).toBeTruthy());

    fireEvent.click(screen.getByText("+ REPORT MALFUNCTION"));
    fireEvent.click(screen.getByText("REPORT MALFUNCTION"));
    expect(reportEldMalfunction).not.toHaveBeenCalled();
    expect(screen.getByText(/Enter the malfunction code/)).toBeTruthy();

    fireEvent.change(screen.getByPlaceholderText("As displayed by the device"), { target: { value: " P-01 " } });
    fireEvent.change(screen.getByPlaceholderText(/What the device did/), { target: { value: "Display frozen" } });
    reportEldMalfunction.mockResolvedValueOnce(eldMalfunction());
    fireEvent.click(screen.getByText("REPORT MALFUNCTION"));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    const [driverId, body] = reportEldMalfunction.mock.calls[0];
    expect(driverId).toBe("driver-1");
    expect(body).toEqual({ code: "P-01", noticedAtUtc: expect.stringMatching(/Z$/), description: "Display frozen" });
  });
});
