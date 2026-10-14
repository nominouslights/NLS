import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { colors, statusMeta } from "@/lib/theme";
import type { HosAlertsToday } from "@/lib/api/hos";
import DispatchBoard from "@/components/screens/DispatchBoard";

// The board's Compliance alerts tile, end to end through DispatchBoard: the sum of the six
// counts, vermillion/gold/teal by what is raised (always with a glyph and the breakdown
// text), "—" greyed on a failed fetch, and the click handed to the app.

const { getHosAlertsToday, listTrips, listInvoices } = vi.hoisted(() => ({
  getHosAlertsToday: vi.fn(),
  listTrips: vi.fn(),
  listInvoices: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => ({ getHosAlertsToday }));
vi.mock("@/lib/api/billing", () => ({ listInvoices }));
vi.mock("@/lib/api/trips", async (importOriginal) => ({ ...(await importOriginal<typeof import("@/lib/api/trips")>()), listTrips }));

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

/** jsdom normalises colours to rgb(); compare like with like. */
function rgb(hex: string): string {
  const n = parseInt(hex.slice(1), 16);
  return `rgb(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255})`;
}

function renderBoard() {
  const onOpenCompliance = vi.fn();
  render(<DispatchBoard onOpenTrip={vi.fn()} onOpenCompliance={onOpenCompliance} />);
  return { onOpenCompliance };
}

beforeEach(() => {
  getHosAlertsToday.mockReset();
  listTrips.mockReset().mockResolvedValue({ items: [] });
  listInvoices.mockReset().mockResolvedValue([]);
});
afterEach(cleanup);

describe("Compliance alerts tile", () => {
  it("sums the six counts and is vermillion ▲ when anyone is over a limit", async () => {
    getHosAlertsToday.mockResolvedValue(alerts({ driversOverLimit: 1, driversNearLimit: 2, uncertifiedYesterday: 1 }));
    const { onOpenCompliance } = renderBoard();

    await waitFor(() => expect(screen.getByTestId("compliance-alerts-value").textContent).toBe("4"));
    const tile = screen.getByTestId("compliance-alerts-tile");
    expect(tile.getAttribute("data-kind")).toBe("over");
    expect(tile.textContent).toContain("▲");
    expect(screen.getByTestId("compliance-alerts-value").style.color).toBe(rgb(statusMeta("over").t));
    expect(screen.getByTestId("compliance-alerts-sub").textContent).toBe("1 over limit · 2 near limit · 1 uncertified");

    fireEvent.click(tile);
    expect(onOpenCompliance).toHaveBeenCalledTimes(1);
  });

  it("is gold ◐ for a malfunction alone", async () => {
    getHosAlertsToday.mockResolvedValue(alerts({ openMalfunctions: 1 }));
    renderBoard();
    await waitFor(() => expect(screen.getByTestId("compliance-alerts-tile").getAttribute("data-kind")).toBe("soon"));
    expect(screen.getByTestId("compliance-alerts-value").textContent).toBe("1");
    expect(screen.getByTestId("compliance-alerts-tile").textContent).toContain("◐");
    expect(screen.getByTestId("compliance-alerts-sub").textContent).toBe("1 ELD malfunction");
  });

  it("is teal ✓ 0 — All clear — when nothing is raised", async () => {
    getHosAlertsToday.mockResolvedValue(alerts());
    renderBoard();
    await waitFor(() => expect(screen.getByTestId("compliance-alerts-tile").getAttribute("data-kind")).toBe("ontime"));
    expect(screen.getByTestId("compliance-alerts-value").textContent).toBe("0");
    expect(screen.getByTestId("compliance-alerts-tile").textContent).toContain("✓");
    expect(screen.getByTestId("compliance-alerts-sub").textContent).toBe("All clear");
  });

  it("greys to — on a failed fetch, without blocking the board", async () => {
    getHosAlertsToday.mockRejectedValue(new Error("network"));
    renderBoard();
    await waitFor(() => expect(screen.getByTestId("compliance-alerts-sub").textContent).toBe("Unavailable"));
    expect(screen.getByTestId("compliance-alerts-value").textContent).toBe("—");
    expect(screen.getByTestId("compliance-alerts-value").style.color).toBe(rgb(colors.textDim));
    await waitFor(() => expect(screen.getByText("No trips scheduled today")).toBeTruthy());
  });
});
