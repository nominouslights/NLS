import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import InspectionDetailModal from "./InspectionDetailModal";
import { ApiError } from "@/lib/api";
import type { InspectionDefectWire, ShopWire, VehicleInspection, WorkOrderStatusWire } from "@/lib/api/maintenance";

// The inspection list is a projected read model, so for ~5s after a work order
// is raised from this modal the reloaded inspection still shows the defect as
// open. The modal must not offer it again in that gap (a second click is a 409),
// and must let the overlay go once the refreshed data carries the link.

const { createWorkOrder, listShops } = vi.hoisted(() => ({
  createWorkOrder: vi.fn<(input: unknown) => Promise<string>>(),
  listShops: vi.fn<() => Promise<ShopWire[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  createWorkOrder,
  listShops,
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const defect = (over: Partial<InspectionDefectWire> & { item: string }): InspectionDefectWire => ({
  severity: "Major",
  note: null,
  workOrderId: null,
  resolutionReason: null,
  resolvedAtUtc: null,
  ...over,
});

const inspectionWith = (defects: InspectionDefectWire[]): VehicleInspection =>
  ({
    id: "insp-1",
    type: "PreTrip",
    source: "DriverApp",
    tripNumber: "NL-2026-0042",
    manifestId: null,
    vehicleId: "veh-1",
    unit: "NL-02",
    driverName: "R. Okimaw",
    enteredBy: null,
    performedAt: "2026-10-01T12:00:00Z",
    odometerKm: null,
    location: null,
    result: "Fail",
    checklist: [],
    defects,
  }) as unknown as VehicleInspection;

const stale = inspectionWith([defect({ item: "Brakes" }), defect({ item: "Wipers", severity: "Minor" })]);

function renderModal(
  inspection: VehicleInspection,
  workOrderNumberOf?: (id: string) => string | undefined,
  workOrderStatusOf?: (id: string) => WorkOrderStatusWire | undefined,
) {
  const props = {
    vehicleId: "veh-1",
    vehicles: [{ id: "veh-1", unit: "NL-02", label: "NL-02" }],
    workOrderNumberOf,
    workOrderStatusOf,
    onWorkOrderCreated: vi.fn(),
    onClose: vi.fn(),
  };
  const view = render(<InspectionDetailModal inspection={inspection} {...props} />);
  return {
    ...props,
    rerender: (next: VehicleInspection) => view.rerender(<InspectionDetailModal inspection={next} {...props} />),
  };
}

/** Opens the work-order form for the first offered defect and submits it. */
async function createForFirstDefect() {
  fireEvent.click(screen.getAllByText("CREATE WORK ORDER")[0]);
  // The form's own submit button carries the same words — it renders last.
  const buttons = screen.getAllByText("CREATE WORK ORDER");
  fireEvent.click(buttons[buttons.length - 1]);
}

describe("InspectionDetailModal — work order just created", () => {
  it("stops offering the defect before the inspection data refreshes", async () => {
    listShops.mockResolvedValue([]);
    createWorkOrder.mockResolvedValueOnce("wo-new");
    const { onWorkOrderCreated, onClose } = renderModal(stale);

    expect(screen.getAllByText("CREATE WORK ORDER")).toHaveLength(2);
    expect(screen.getByText("CREATE WORK ORDER FOR ALL 2 OPEN DEFECTS")).toBeTruthy();

    await createForFirstDefect();

    await screen.findByText("Work order created · updating");
    expect(createWorkOrder).toHaveBeenCalledWith(
      expect.objectContaining({ defects: [{ inspectionId: "insp-1", item: "Brakes" }] }),
    );
    expect(onWorkOrderCreated).toHaveBeenCalled();
    expect(onClose).not.toHaveBeenCalled();
    // Only Wipers is still offered — and "all open defects" no longer counts Brakes.
    expect(screen.getAllByText("CREATE WORK ORDER")).toHaveLength(1);
    expect(screen.getAllByText("RESOLVE")).toHaveLength(1);
    expect(screen.queryByText(/CREATE WORK ORDER FOR ALL/)).toBeNull();
  });

  it("drops the overlay once the refreshed data carries the work order", async () => {
    listShops.mockResolvedValue([]);
    createWorkOrder.mockResolvedValueOnce("wo-new");
    const { rerender } = renderModal(stale, (id) => (id === "wo-new" ? "WO-9" : undefined));

    await createForFirstDefect();
    // The work-order list already knows the number; the inspection does not yet.
    await screen.findByText("On WO-9 · updating");

    rerender(inspectionWith([defect({ item: "Brakes", workOrderId: "wo-new" }), defect({ item: "Wipers", severity: "Minor" })]));
    expect(screen.getByText("On WO-9")).toBeTruthy();
    expect(screen.queryByText(/updating/)).toBeNull();
  });

  it("shows a 409's own message inline, not a generic error", async () => {
    listShops.mockResolvedValue([]);
    createWorkOrder.mockRejectedValueOnce(
      new ApiError(
        "Fleet.Inspection.DefectAlreadyOnWorkOrder",
        "Defect 'Brakes' is already on an open work order.",
        409,
      ),
    );
    renderModal(stale);

    await createForFirstDefect();

    await screen.findByText("Defect 'Brakes' is already on an open work order.");
    await waitFor(() => expect(screen.queryByText(/Failed to create/)).toBeNull());
  });
});

// A work order from before per-defect links sits on the INSPECTION
// (`generatedWorkOrderId`), not on each defect. While it is open it holds every
// open defect of the inspection — the Open Defects tab shows them on it — so
// the modal must not offer a second work order (the server would move the
// defect, and the old work order's completion would then skip it).
describe("InspectionDetailModal — legacy inspection-level work order", () => {
  const legacy = (status?: WorkOrderStatusWire) => {
    const insp = { ...stale, generatedWorkOrderId: "wo-legacy" } as VehicleInspection;
    return renderModal(
      insp,
      (id) => (id === "wo-legacy" ? "WO-3" : undefined),
      status ? (id) => (id === "wo-legacy" ? status : undefined) : undefined,
    );
  };

  it("offers no CREATE/RESOLVE while the legacy work order is open, and shows it as the defects' work order", () => {
    legacy("InProgress");
    expect(screen.queryByText("CREATE WORK ORDER")).toBeNull();
    expect(screen.queryByText("RESOLVE")).toBeNull();
    expect(screen.queryByText(/CREATE WORK ORDER FOR ALL/)).toBeNull();
    expect(screen.getAllByText("On WO-3")).toHaveLength(2);
  });

  it("treats an unknown legacy status as still open", () => {
    legacy(undefined);
    expect(screen.queryByText("CREATE WORK ORDER")).toBeNull();
    expect(screen.getAllByText("On WO-3")).toHaveLength(2);
  });

  it.each(["Completed", "Cancelled"] as const)("offers CREATE/RESOLVE again once the legacy work order is %s", (status) => {
    legacy(status);
    expect(screen.getAllByText("CREATE WORK ORDER")).toHaveLength(2);
    expect(screen.getAllByText("RESOLVE")).toHaveLength(2);
    expect(screen.getByText("CREATE WORK ORDER FOR ALL 2 OPEN DEFECTS")).toBeTruthy();
    expect(screen.queryByText("On WO-3")).toBeNull();
  });

  it("a defect's own work order still wins over a closed legacy one", () => {
    const insp = {
      ...inspectionWith([defect({ item: "Brakes", workOrderId: "wo-own" }), defect({ item: "Wipers", severity: "Minor" })]),
      generatedWorkOrderId: "wo-legacy",
    } as VehicleInspection;
    renderModal(insp, (id) => ({ "wo-own": "WO-7", "wo-legacy": "WO-3" })[id], () => "Completed");
    expect(screen.getByText("On WO-7")).toBeTruthy();
    expect(screen.getAllByText("CREATE WORK ORDER")).toHaveLength(1);
    expect(screen.queryByText(/CREATE WORK ORDER FOR ALL/)).toBeNull();
  });
});
