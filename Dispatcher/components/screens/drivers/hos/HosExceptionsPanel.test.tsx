import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { statusMeta } from "@/lib/theme";
import { HosExceptionsPanel } from "./HosExceptionsPanel";
import { hosException } from "./hosFixtures";

// The exceptions panel: every status is colour + its own glyph + label; ACKNOWLEDGE is
// offered on a Declared one to anyone on the tab; APPROVE DEFERRAL, REJECT and WITHDRAW
// appear only for the compliance roles (client gate — the server's policy is the real one).

const { listHosExceptions, acknowledgeHosException, rejectHosException, withdrawHosException } = vi.hoisted(() => ({
  listHosExceptions: vi.fn(),
  acknowledgeHosException: vi.fn(),
  rejectHosException: vi.fn(),
  withdrawHosException: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => ({ listHosExceptions, acknowledgeHosException, rejectHosException, withdrawHosException }));
vi.mock("@/lib/api/trips", () => ({ listTrips: vi.fn().mockResolvedValue({ items: [], page: null, pageSize: null, totalCount: 0 }) }));

function renderPanel(canCompliance: boolean, onChanged = vi.fn()) {
  render(<HosExceptionsPanel driverId="driver-1" driverName="K. Beardy" today="2026-10-09" version={0} canCompliance={canCompliance} onChanged={onChanged} />);
  return { onChanged };
}

beforeEach(() => {
  listHosExceptions.mockReset();
  acknowledgeHosException.mockReset();
  rejectHosException.mockReset();
  withdrawHosException.mockReset();
});

afterEach(cleanup);

describe("HosExceptionsPanel — rows", () => {
  it("renders each exception with its kind, status chip (own glyph), window, reason and extension", async () => {
    listHosExceptions.mockResolvedValueOnce([
      hosException(),
      hosException({ id: "ex-2", kind: "Off-duty deferral (CO approved)", status: "Approved", extraDrivingMinutes: null, deferredOffDutyMinutes: 90, tripId: "11111111-2222-3333-4444-555555555555", reason: "Late return" }),
      hosException({ id: "ex-3", kind: "Emergency", status: "Rejected", extraDrivingMinutes: null, closureNote: "Not an emergency", reason: "Flat tire" }),
    ]);
    renderPanel(false);

    await waitFor(() => expect(screen.getAllByTestId("hos-exception-row")).toHaveLength(3));
    expect(screen.getByText("Adverse driving conditions")).toBeTruthy();
    expect(screen.getByText("Off-duty deferral")).toBeTruthy();
    expect(screen.getByText("Emergency")).toBeTruthy();

    const declared = screen.getByText("Declared");
    expect(declared.parentElement?.textContent).toContain("◐");
    const approved = screen.getByText("Approved");
    expect(approved.parentElement?.textContent).toContain(statusMeta("ontime").g);
    const rejected = screen.getByText("Rejected");
    expect(rejected.parentElement?.textContent).toContain(statusMeta("over").g);

    expect(screen.getByText("+60 min driving")).toBeTruthy();
    expect(screen.getByText("90 min off-duty deferred")).toBeTruthy();
    expect(screen.getByText("Trip 11111111")).toBeTruthy();
    expect(screen.getAllByText(/Oct 9, 10:00 – open/)).toHaveLength(3);
    expect(screen.getByText("Note — Not an emergency")).toBeTruthy();
  });

  it("says so when there are none, and shows the server's message when the list fails", async () => {
    listHosExceptions.mockResolvedValueOnce([]);
    renderPanel(false);
    await waitFor(() => expect(screen.getByText(/No exceptions declared/)).toBeTruthy());
  });
});

describe("HosExceptionsPanel — actions by role", () => {
  it("dispatch sees RECORD EXCEPTION and ACKNOWLEDGE, but no deferral, reject or withdraw", async () => {
    listHosExceptions.mockResolvedValueOnce([hosException()]);
    renderPanel(false);
    await waitFor(() => expect(screen.getByText("Acknowledge")).toBeTruthy());

    expect(screen.getByText("+ RECORD EXCEPTION")).toBeTruthy();
    expect(screen.queryByText("APPROVE DEFERRAL")).toBeNull();
    expect(screen.queryByText("Reject")).toBeNull();
    expect(screen.queryByText("Withdraw")).toBeNull();
  });

  it("compliance roles see APPROVE DEFERRAL, Reject and Withdraw — not on a closed one", async () => {
    listHosExceptions.mockResolvedValueOnce([hosException(), hosException({ id: "ex-2", status: "Withdrawn" })]);
    renderPanel(true);
    await waitFor(() => expect(screen.getAllByTestId("hos-exception-row")).toHaveLength(2));

    expect(screen.getByText("APPROVE DEFERRAL")).toBeTruthy();
    expect(screen.getAllByText("Reject")).toHaveLength(1);
    expect(screen.getAllByText("Withdraw")).toHaveLength(1);
  });

  it("ACKNOWLEDGE posts the acknowledgement and asks the parent to reload", async () => {
    listHosExceptions.mockResolvedValueOnce([hosException()]);
    acknowledgeHosException.mockResolvedValueOnce(hosException({ status: "Acknowledged" }));
    const { onChanged } = renderPanel(false);
    await waitFor(() => expect(screen.getByText("Acknowledge")).toBeTruthy());

    fireEvent.click(screen.getByText("Acknowledge"));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(acknowledgeHosException).toHaveBeenCalledWith("driver-1", "ex-1");
  });

  it("REJECT opens the note modal and posts the note", async () => {
    listHosExceptions.mockResolvedValueOnce([hosException()]);
    rejectHosException.mockResolvedValueOnce(hosException({ status: "Rejected" }));
    const { onChanged } = renderPanel(true);
    await waitFor(() => expect(screen.getByText("Reject")).toBeTruthy());

    fireEvent.click(screen.getByText("Reject"));
    fireEvent.change(screen.getByPlaceholderText("For the record (optional)"), { target: { value: "Forecast was published" } });
    fireEvent.click(screen.getByText("REJECT"));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(rejectHosException).toHaveBeenCalledWith("driver-1", "ex-1", "Forecast was published");
  });
});
