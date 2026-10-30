import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { HoursOfServiceTab } from "./HoursOfServiceTab";
import { dutyDay, hosStatus, outOfService } from "./hos/hosFixtures";

// The tab's standing chips beside the gauge — out of service (✕), paper-log mode (✎) and
// records incomplete (◐) — each from the per-driver status, each its own glyph; and that the
// compliance-only RECORD OUT OF SERVICE button follows the client role gate.

const api = vi.hoisted(() => ({
  listDutyDays: vi.fn(),
  listHosExceptions: vi.fn(),
  listEldMalfunctions: vi.fn(),
  acknowledgeHosException: vi.fn(),
  rejectHosException: vi.fn(),
  withdrawHosException: vi.fn(),
  reportEldMalfunction: vi.fn(),
  resolveEldMalfunction: vi.fn(),
  attestPriorRecords: vi.fn(),
  recordDutyEvent: vi.fn(),
  recordOutOfService: vi.fn(),
  liftOutOfService: vi.fn(),
  transcribeDay: vi.fn(),
  amendDutyEvent: vi.fn(),
  declareHosException: vi.fn(),
  approveOffDutyDeferral: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => api);
vi.mock("@/lib/api/trips", () => ({ listTrips: vi.fn().mockResolvedValue({ items: [], page: null, pageSize: null, totalCount: 0 }) }));

function renderTab(status = hosStatus(), canCompliance = false) {
  return render(
    <HoursOfServiceTab driverId="driver-1" driverName="K. Beardy" today="2026-10-09" status={status} statusError={null} version={0} canCompliance={canCompliance} onLedgerChanged={vi.fn()} />,
  );
}

beforeEach(() => {
  api.listDutyDays.mockReset().mockResolvedValue([dutyDay()]);
  api.listHosExceptions.mockReset().mockResolvedValue([]);
  api.listEldMalfunctions.mockReset().mockResolvedValue([]);
});

afterEach(cleanup);

describe("HoursOfServiceTab — standing chips", () => {
  it("shows the paper-log chip (✎, caution colour) beside the gauge while an ELD malfunction is open", async () => {
    renderTab(hosStatus({ eldMalfunctionOpen: true }));
    const chip = screen.getByText("Paper-log mode");
    expect(chip.parentElement?.textContent).toContain("✎");
    expect(chip.parentElement?.textContent).not.toContain(statusMeta("soon").g);
    await waitFor(() => expect(api.listDutyDays).toHaveBeenCalled());
  });

  it("shows ✕ Out of service and ◐ Records incomplete from the status, and nothing when clear", async () => {
    const { unmount } = renderTab(hosStatus({ outOfService: outOfService(), recordsOnFile: false }));
    expect(screen.getByText("Out of service").parentElement?.textContent).toContain("✕");
    expect(screen.getByText("Records incomplete").parentElement?.textContent).toContain(statusMeta("soon").g);
    unmount();

    renderTab();
    expect(screen.queryByText("Paper-log mode")).toBeNull();
    expect(screen.queryByText("Out of service")).toBeNull();
    expect(screen.queryByText("Records incomplete")).toBeNull();
    await waitFor(() => expect(api.listHosExceptions).toHaveBeenCalled());
  });
});

describe("HoursOfServiceTab — role gate", () => {
  it("offers RECORD OUT OF SERVICE to compliance roles only, and only while none is in force", async () => {
    const { unmount } = renderTab(hosStatus(), false);
    expect(screen.queryByText("RECORD OUT OF SERVICE")).toBeNull();
    expect(screen.queryByText("APPROVE DEFERRAL")).toBeNull();
    unmount();

    renderTab(hosStatus(), true);
    expect(screen.getByText("RECORD OUT OF SERVICE")).toBeTruthy();
    await waitFor(() => expect(screen.getByText("APPROVE DEFERRAL")).toBeTruthy());
    cleanup();

    renderTab(hosStatus({ outOfService: outOfService() }), true);
    expect(screen.queryByText("RECORD OUT OF SERVICE")).toBeNull();
  });

  it("always offers the other-carrier entry and the transcription", async () => {
    renderTab();
    expect(screen.getByText("+ OTHER CARRIER HOURS")).toBeTruthy();
    expect(screen.getByText("+ TRANSCRIBE DAY")).toBeTruthy();
    await waitFor(() => expect(api.listEldMalfunctions).toHaveBeenCalled());
  });
});
