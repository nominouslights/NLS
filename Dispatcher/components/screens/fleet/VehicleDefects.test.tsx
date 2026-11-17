import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import VehicleDefects from "./VehicleDefects";
import type { Vehicle } from "@/lib/api";
import type { VehicleDefectWire } from "@/lib/api/maintenance";
import { vehicleDefect } from "@/lib/defect.fixtures";
import { PROJECTION_RECONCILE_MS, notifyDefectsChanged } from "@/lib/defectSync";

// After a work order is raised from the Open Defects tab, the row reads "Work
// order created · updating" until the read model (≈5s behind) catches up. That
// mark must heal itself from the data: a second change inside the window (a
// re-report) must not strand it, the mark goes once the row shows the work
// order, and if that work order is later cancelled CREATE WORK ORDER returns.
//
// ONE change, ONE delayed reload: the tab owns no refresh timer of its own — the
// reload is DefectsPanel's useDefectsChanged, fed by the modals' own
// announcement (made after onSaved, exactly as the real modals do).

const { listVehicleDefects } = vi.hoisted(() => ({
  listVehicleDefects: vi.fn<(vehicleId: string, includeResolved?: boolean) => Promise<VehicleDefectWire[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  listVehicleDefects,
}));

// The modals are exercised elsewhere; here they only report a save and announce
// it, in the real modals' order (onSaved, then notifyDefectsChanged).
vi.mock("@/components/WorkOrderModal", () => ({
  default: ({ onSaved, onClose }: { onSaved: (id: string) => void; onClose: () => void }) => (
    <button
      onClick={() => {
        onSaved("wo-9");
        notifyDefectsChanged();
        onClose();
      }}
    >
      SAVE WORK ORDER
    </button>
  ),
}));
vi.mock("@/components/InspectionEntryModal", () => ({
  default: ({ onSaved, onClose }: { onSaved: () => void; onClose: () => void }) => (
    <button
      onClick={() => {
        onSaved();
        notifyDefectsChanged();
        onClose();
      }}
    >
      SAVE INSPECTION
    </button>
  ),
}));

const vehicle = { id: "veh-1", unitNumber: "NL-02", odometerKm: 120000 } as Vehicle;

const brakes = vehicleDefect({ inspectionId: "i-1", item: "Brakes", severity: "OutOfService" });
const horn = vehicleDefect({
  inspectionId: "i-0",
  item: "Horn",
  severity: "Minor",
  resolutionReason: "NoFaultFound",
  resolvedAtUtc: "2026-10-02T00:00:00Z",
  resolvedBy: "Dispatch",
});

let serverRows: VehicleDefectWire[];

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  serverRows = [brakes, horn];
  listVehicleDefects.mockImplementation(async () => serverRows);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.clearAllMocks();
});

const UPDATING = "Work order created · updating";

async function loadsSettle(calls: number) {
  await waitFor(() => expect(listVehicleDefects).toHaveBeenCalledTimes(calls));
  await act(async () => {});
}

async function reReportHorn() {
  fireEvent.click(screen.getByText("RE-REPORT"));
  fireEvent.click(await screen.findByText("SAVE INSPECTION"));
}

async function advance(ms: number) {
  await act(async () => {
    vi.advanceTimersByTime(ms);
  });
}

describe("VehicleDefects — pending work-order mark", () => {
  it("survives a second change inside the window, then heals from the data", async () => {
    render(<VehicleDefects vehicle={vehicle} />);
    await screen.findByText("Brakes");
    await loadsSettle(1);

    fireEvent.click(screen.getByText("CREATE WORK ORDER"));
    fireEvent.click(await screen.findByText("SAVE WORK ORDER"));
    // No immediate re-read: the projection is still stale.
    await act(async () => {});
    expect(listVehicleDefects).toHaveBeenCalledTimes(1);
    expect(screen.getByText(UPDATING)).toBeTruthy();
    expect(screen.queryByText("CREATE WORK ORDER")).toBeNull();

    // A re-report 3s in pushes the one pending reload back; still no read.
    await advance(3000);
    await reReportHorn();
    await act(async () => {});
    expect(listVehicleDefects).toHaveBeenCalledTimes(1);
    expect(screen.getByText(UPDATING)).toBeTruthy();

    // The projector catches up; the single reconcile reload shows the work order.
    serverRows = [
      { ...brakes, workOrderId: "wo-9", workOrderNumber: "WO-9", workOrderStatus: "Open" },
      horn,
    ];
    await advance(PROJECTION_RECONCILE_MS);
    await loadsSettle(2);
    expect(screen.queryByText(UPDATING)).toBeNull();
    expect(screen.getByText(/^WO-9 · /)).toBeTruthy();
    expect(screen.queryByText("CREATE WORK ORDER")).toBeNull();

    // The work order is cancelled; the next change's reload offers the defect again.
    serverRows = [{ ...brakes, workOrderId: "wo-9", workOrderNumber: "WO-9", workOrderStatus: "Cancelled" }, horn];
    await reReportHorn();
    await advance(PROJECTION_RECONCILE_MS);
    await loadsSettle(3);
    expect(screen.queryByText(UPDATING)).toBeNull();
    expect(screen.getByText("CREATE WORK ORDER")).toBeTruthy();
  });

  it("drops the mark on the reconcile reload, even if the data never shows the work order", async () => {
    render(<VehicleDefects vehicle={vehicle} />);
    await screen.findByText("Brakes");
    await loadsSettle(1);

    fireEvent.click(screen.getByText("CREATE WORK ORDER"));
    fireEvent.click(await screen.findByText("SAVE WORK ORDER"));
    await act(async () => {});
    expect(screen.getByText(UPDATING)).toBeTruthy();

    // The reload lands after the mark's own window, so the mark goes regardless.
    await advance(PROJECTION_RECONCILE_MS);
    await loadsSettle(2);
    expect(screen.queryByText(UPDATING)).toBeNull();
    expect(screen.getByText("CREATE WORK ORDER")).toBeTruthy();
  });

  it("makes exactly one delayed reload for one change", async () => {
    render(<VehicleDefects vehicle={vehicle} />);
    await screen.findByText("Brakes");
    await loadsSettle(1);

    fireEvent.click(screen.getByText("CREATE WORK ORDER"));
    fireEvent.click(await screen.findByText("SAVE WORK ORDER"));
    // (Slack for the real time that shouldAdvanceTime lets through.)
    await advance(PROJECTION_RECONCILE_MS - 500);
    expect(listVehicleDefects).toHaveBeenCalledTimes(1);

    await advance(500);
    await loadsSettle(2);
    await advance(PROJECTION_RECONCILE_MS * 3);
    await act(async () => {});
    expect(listVehicleDefects).toHaveBeenCalledTimes(2);
  });
});
