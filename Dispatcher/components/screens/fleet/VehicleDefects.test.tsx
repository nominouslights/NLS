import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import VehicleDefects from "./VehicleDefects";
import type { Vehicle } from "@/lib/api";
import type { VehicleDefectWire } from "@/lib/api/maintenance";
import { vehicleDefect } from "@/lib/defect.fixtures";

// After a work order is raised from the Open Defects tab, the row reads "Work
// order created · updating" until the read model (≈5s behind) catches up. That
// mark must heal itself from the data: a second refresh inside the window (a
// re-report) must not strand it, the mark goes once the row shows the work
// order, and if that work order is later cancelled CREATE WORK ORDER returns.

const { listVehicleDefects } = vi.hoisted(() => ({
  listVehicleDefects: vi.fn<(vehicleId: string, includeResolved?: boolean) => Promise<VehicleDefectWire[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  listVehicleDefects,
}));

// The modals are exercised elsewhere; here they only report a save.
vi.mock("@/components/WorkOrderModal", () => ({
  default: ({ onSaved, onClose }: { onSaved: (id: string) => void; onClose: () => void }) => (
    <button
      onClick={() => {
        onSaved("wo-9");
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

describe("VehicleDefects — pending work-order mark", () => {
  it("survives a second refresh inside the window, then heals from the data", async () => {
    render(<VehicleDefects vehicle={vehicle} />);
    await screen.findByText("Brakes");
    await loadsSettle(1);

    fireEvent.click(screen.getByText("CREATE WORK ORDER"));
    fireEvent.click(await screen.findByText("SAVE WORK ORDER"));
    // The immediate refetch still reads the stale projection.
    await loadsSettle(2);
    expect(screen.getByText(UPDATING)).toBeTruthy();
    expect(screen.queryByText("CREATE WORK ORDER")).toBeNull();

    // A re-report 3s in refreshes again (still stale) and replaces the timer.
    await act(async () => {
      vi.advanceTimersByTime(3000);
    });
    await reReportHorn();
    await loadsSettle(3);
    expect(screen.getByText(UPDATING)).toBeTruthy();

    // The projector catches up; the reconcile refresh shows the work order.
    serverRows = [
      { ...brakes, workOrderId: "wo-9", workOrderNumber: "WO-9", workOrderStatus: "Open" },
      horn,
    ];
    await act(async () => {
      vi.advanceTimersByTime(6000);
    });
    await loadsSettle(4);
    expect(screen.queryByText(UPDATING)).toBeNull();
    expect(screen.getByText(/^WO-9 · /)).toBeTruthy();
    expect(screen.queryByText("CREATE WORK ORDER")).toBeNull();

    // The work order is cancelled; the next refresh offers the defect again.
    serverRows = [{ ...brakes, workOrderId: "wo-9", workOrderNumber: "WO-9", workOrderStatus: "Cancelled" }, horn];
    await reReportHorn();
    await loadsSettle(5);
    expect(screen.queryByText(UPDATING)).toBeNull();
    expect(screen.getByText("CREATE WORK ORDER")).toBeTruthy();
  });

  it("drops the mark on any refresh past its window, even if the data never shows the work order", async () => {
    render(<VehicleDefects vehicle={vehicle} />);
    await screen.findByText("Brakes");
    await loadsSettle(1);

    fireEvent.click(screen.getByText("CREATE WORK ORDER"));
    fireEvent.click(await screen.findByText("SAVE WORK ORDER"));
    await loadsSettle(2);
    expect(screen.getByText(UPDATING)).toBeTruthy();

    // Re-report 3s in: its timer replaces the work order's; when it fires, the
    // load is past the mark's own window, so the mark goes regardless.
    await act(async () => {
      vi.advanceTimersByTime(3000);
    });
    await reReportHorn();
    await loadsSettle(3);
    await act(async () => {
      vi.advanceTimersByTime(6000);
    });
    await loadsSettle(4);
    expect(screen.queryByText(UPDATING)).toBeNull();
    expect(screen.getByText("CREATE WORK ORDER")).toBeTruthy();
  });
});
