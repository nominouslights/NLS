import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import DefectsPanel from "./DefectsPanel";
import type { VehicleDefectWire } from "@/lib/api/maintenance";
import { vehicleDefect } from "@/lib/defect.fixtures";

// CREATE WORK ORDER is an action, offered only where the parent passes
// onCreateWorkOrder (the vehicle's Open Defects tab — never the trip detail), and
// only on an open defect that is not already on an open work order.

const { listVehicleDefects } = vi.hoisted(() => ({
  listVehicleDefects: vi.fn<(vehicleId: string, includeResolved?: boolean) => Promise<VehicleDefectWire[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  listVehicleDefects,
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const rows: VehicleDefectWire[] = [
  vehicleDefect({ inspectionId: "i-1", item: "Brakes", severity: "OutOfService" }),
  vehicleDefect({
    inspectionId: "i-1",
    item: "Mirror",
    severity: "Major",
    workOrderId: "wo-1",
    workOrderNumber: "WO-7",
    workOrderStatus: "InProgress",
  }),
  vehicleDefect({ inspectionId: "i-2", item: "Wipers", severity: "Minor" }),
  vehicleDefect({
    inspectionId: "i-3",
    item: "Horn",
    severity: "Minor",
    resolutionReason: "NoFaultFound",
    resolvedAtUtc: "2026-10-02T00:00:00Z",
    resolvedBy: "Dispatch",
    workOrderId: "wo-2",
    workOrderNumber: "WO-8",
    workOrderStatus: "Completed",
  }),
];

describe("DefectsPanel — create work order", () => {
  it("offers no create action when the prop is not passed", async () => {
    listVehicleDefects.mockResolvedValueOnce(rows);
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" />);

    await screen.findByText("Brakes");
    expect(screen.queryByText("CREATE WORK ORDER")).toBeNull();
    expect(screen.queryByRole("checkbox")).toBeNull();
  });

  it("offers it only on open rows with no open work order", async () => {
    listVehicleDefects.mockResolvedValueOnce(rows);
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" onCreateWorkOrder={() => {}} />);

    await screen.findByText("Brakes");
    // Brakes and Wipers — not Mirror (on WO-7, in progress) nor Horn (resolved).
    expect(screen.getAllByText("CREATE WORK ORDER")).toHaveLength(2);
    expect(screen.getByLabelText("Select Brakes for a work order")).toBeTruthy();
    expect(screen.getByLabelText("Select Wipers for a work order")).toBeTruthy();
    expect(screen.queryByLabelText("Select Mirror for a work order")).toBeNull();
    expect(screen.queryByLabelText("Select Horn for a work order")).toBeNull();
    // The per-defect work-order chip carries its glyph + label.
    expect(screen.getByText("WO-7 · In Progress")).toBeTruthy();
  });

  it("hands a single row to the parent", async () => {
    listVehicleDefects.mockResolvedValueOnce(rows);
    const onCreate = vi.fn();
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" onCreateWorkOrder={onCreate} />);

    await screen.findByText("Brakes");
    fireEvent.click(screen.getAllByText("CREATE WORK ORDER")[0]);
    expect(onCreate).toHaveBeenCalledWith([expect.objectContaining({ item: "Brakes" })]);
  });

  it("puts several ticked defects on one work order", async () => {
    listVehicleDefects.mockResolvedValueOnce(rows);
    const onCreate = vi.fn();
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" onCreateWorkOrder={onCreate} />);

    await screen.findByText("Brakes");
    fireEvent.click(screen.getByLabelText("Select Brakes for a work order"));
    fireEvent.click(screen.getByLabelText("Select Wipers for a work order"));
    fireEvent.click(screen.getByText("CREATE WORK ORDER FOR 2 SELECTED"));
    expect(onCreate).toHaveBeenCalledWith([
      expect.objectContaining({ inspectionId: "i-1", item: "Brakes" }),
      expect.objectContaining({ inspectionId: "i-2", item: "Wipers" }),
    ]);
  });

  it("does not re-offer a row a work order was just created for", async () => {
    listVehicleDefects.mockResolvedValueOnce(rows);
    render(
      <DefectsPanel
        vehicleId="veh-1"
        unit="NL-02"
        onCreateWorkOrder={() => {}}
        pendingWorkOrders={new Map([["i-1:Brakes", { workOrderId: "wo-9", recheckAfter: Number.MAX_SAFE_INTEGER }]])}
      />,
    );

    await screen.findByText("Brakes");
    expect(screen.getAllByText("CREATE WORK ORDER")).toHaveLength(1);
    expect(screen.getByText("Work order created · updating")).toBeTruthy();
  });
});
