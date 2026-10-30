import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError } from "@/lib/api/transport";
import { OosRegister } from "./OosRegister";
import { EldMalfunctions } from "./EldMalfunctions";
import { DeferralQueue, inQueue, queueRange } from "./DeferralQueue";
import { driver, oosRecord } from "./complianceFixtures";
import { eldMalfunction, hosException } from "@/components/screens/drivers/hos/hosFixtures";

// The three cross-driver registers: out of service (LIFT), open ELD malfunctions (RESOLVE),
// and the deferral/exception queue (REJECT). Each fans out over the roster and posts to the
// row's own driver.

const api = vi.hoisted(() => ({
  listOutOfService: vi.fn(),
  liftOutOfService: vi.fn(),
  listEldMalfunctions: vi.fn(),
  resolveEldMalfunction: vi.fn(),
  listHosExceptions: vi.fn(),
  rejectHosException: vi.fn(),
  approveOffDutyDeferral: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => api);

const ROSTER = [driver(), driver({ id: "driver-2", name: "M. Spence" })];

beforeEach(() => Object.values(api).forEach((f) => f.mockReset()));
afterEach(cleanup);

describe("OosRegister", () => {
  it("splits in-force (✕) from history and LIFTs with a reason on the row's driver", async () => {
    api.listOutOfService.mockImplementation((id: string) =>
      Promise.resolve(
        id === "driver-2"
          ? [oosRecord({ id: "oos-2", driverId: "driver-2" })]
          : [oosRecord({ id: "oos-0", status: "Lifted", active: false, liftedAtUtc: "2026-10-01T12:00:00Z", liftReason: "Rest taken" })],
      ),
    );
    const onChanged = vi.fn();
    render(<OosRegister roster={ROSTER} version={0} onChanged={onChanged} onOpenDriver={vi.fn()} />);

    await waitFor(() => expect(screen.getAllByTestId("oos-active-row")).toHaveLength(1));
    expect(screen.getAllByTestId("oos-history-row")).toHaveLength(1);
    const inForce = screen.getByText("In force until lifted");
    expect(inForce.textContent).toContain("✕");
    expect(screen.getByText(/Lift: Rest taken/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Lift out-of-service for M. Spence" }));
    fireEvent.change(screen.getByPlaceholderText(/Required when lifting/), { target: { value: "Inspection cleared" } });
    api.liftOutOfService.mockRejectedValueOnce(new ApiError("Drivers.Hos.LiftReasonRequired", "A reason is required to lift early.", 400));
    fireEvent.click(screen.getByText("LIFT"));
    await waitFor(() => expect(screen.getByText("A reason is required to lift early.")).toBeTruthy());

    api.liftOutOfService.mockResolvedValueOnce(oosRecord({ id: "oos-2", status: "Lifted", active: false }));
    fireEvent.click(screen.getByText("LIFT"));
    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(api.liftOutOfService).toHaveBeenLastCalledWith("driver-2", "oos-2", "Inspection cleared");
  });

  it("says so when nobody is out of service", async () => {
    api.listOutOfService.mockResolvedValue([]);
    render(<OosRegister roster={ROSTER} version={0} onChanged={vi.fn()} onOpenDriver={vi.fn()} />);
    await waitFor(() => expect(screen.getByText("No driver is out of service")).toBeTruthy());
  });
});

describe("EldMalfunctions", () => {
  it("lists only open reports with days on paper (✎), and RESOLVEs with a note", async () => {
    api.listEldMalfunctions.mockImplementation((id: string) =>
      Promise.resolve(
        id === "driver-1"
          ? [eldMalfunction(), eldMalfunction({ id: "eld-0", status: "Resolved" })] // noticed 2026-10-09 07:00 local
          : [],
      ),
    );
    const onChanged = vi.fn();
    render(<EldMalfunctions roster={ROSTER} today="2026-10-12" version={0} onChanged={onChanged} onOpenDriver={vi.fn()} />);

    await waitFor(() => expect(screen.getAllByTestId("eld-open-row")).toHaveLength(1));
    const chip = screen.getByText("3 days on paper");
    expect(chip.textContent).toContain("✎");

    fireEvent.click(screen.getByRole("button", { name: "Resolve ELD malfunction P-01 for K. Beardy" }));
    fireEvent.change(screen.getByPlaceholderText(/Repaired/), { target: { value: "Unit replaced; paper transcribed" } });
    api.resolveEldMalfunction.mockResolvedValueOnce(eldMalfunction({ status: "Resolved" }));
    fireEvent.click(screen.getByText("RESOLVE"));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(api.resolveEldMalfunction).toHaveBeenCalledWith("driver-1", "eld-1", "Unit replaced; paper transcribed");
  });
});

describe("DeferralQueue", () => {
  it("queues in-force deferrals and unacknowledged declarations only", () => {
    expect(inQueue(hosException())).toBe(true); // Declared adverse
    expect(inQueue(hosException({ status: "Acknowledged" }))).toBe(false);
    expect(inQueue(hosException({ kind: "Off-duty deferral (CO approved)", status: "Approved" }))).toBe(true);
    expect(inQueue(hosException({ kind: "Off-duty deferral (CO approved)", status: "Rejected" }))).toBe(false);
    expect(queueRange("2026-10-12")).toEqual({ from: "2026-09-29", to: "2026-10-19" });
  });

  it("REJECTs with a note on the row's driver and links to the driver", async () => {
    api.listHosExceptions.mockImplementation((id: string) =>
      Promise.resolve(
        id === "driver-2"
          ? [hosException({ id: "ex-9", driverId: "driver-2", kind: "Off-duty deferral (CO approved)", status: "Approved", deferredOffDutyMinutes: 90, extraDrivingMinutes: null })]
          : [hosException(), hosException({ id: "ex-ack", status: "Acknowledged" })],
      ),
    );
    const onChanged = vi.fn();
    const onOpenDriver = vi.fn();
    render(<DeferralQueue roster={ROSTER} today="2026-10-12" version={0} onChanged={onChanged} onOpenDriver={onOpenDriver} />);

    await waitFor(() => expect(screen.getAllByTestId("deferral-row")).toHaveLength(2));
    expect(api.listHosExceptions).toHaveBeenCalledWith("driver-1", { from: "2026-09-29", to: "2026-10-19" });
    expect(screen.getByText("Awaiting acknowledgement").textContent).toContain("◐");
    expect(screen.getByText("90 min deferred")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "M. Spence" }));
    expect(onOpenDriver).toHaveBeenCalledWith("driver-2");

    fireEvent.click(screen.getByRole("button", { name: "Reject Off-duty deferral for M. Spence" }));
    fireEvent.change(screen.getByPlaceholderText("For the record (optional)"), { target: { value: "Trip cancelled" } });
    api.rejectHosException.mockResolvedValueOnce(hosException({ status: "Rejected" }));
    fireEvent.click(screen.getByText("REJECT"));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(api.rejectHosException).toHaveBeenCalledWith("driver-2", "ex-9", "Trip cancelled");
  });
});
