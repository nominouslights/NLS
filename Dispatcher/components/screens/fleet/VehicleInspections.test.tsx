import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import VehicleInspections from "./VehicleInspections";
import type { Vehicle } from "@/lib/api";
import type { InspectionDefectWire, VehicleInspection, WorkOrderWire } from "@/lib/api/maintenance";
import { PROJECTION_RECONCILE_MS, notifyDefectsChanged } from "@/lib/defectSync";

// The vehicle's Inspections tab: the result chip is the frozen record of what
// the driver found; beside it, a live chip says whether those defects are still
// open, and each cleared defect line says so. A resolve anywhere else in the
// console reaches this tab once the projector has run.

const { listInspections, listVehicleWorkOrders } = vi.hoisted(() => ({
  listInspections: vi.fn<(params?: { unit?: string; tripNumber?: string }) => Promise<VehicleInspection[]>>(),
  listVehicleWorkOrders: vi.fn<(vehicleId: string) => Promise<WorkOrderWire[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  listInspections,
  listVehicleWorkOrders,
}));

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.clearAllMocks();
});

const vehicle = { id: "veh-1", unitNumber: "NL-02", odometerKm: 120000 } as Vehicle;

const defect = (over: Partial<InspectionDefectWire> & { item: string }): InspectionDefectWire => ({
  severity: "Major",
  note: null,
  workOrderId: null,
  resolutionReason: null,
  resolvedAtUtc: null,
  resolvedWithInspectionId: null,
  ...over,
});

const inspection = (defects: InspectionDefectWire[]): VehicleInspection =>
  ({
    id: "insp-1",
    type: "PreTrip",
    source: "DriverApp",
    tripNumber: "NL-2026-0042",
    vehicleId: "veh-1",
    unit: "NL-02",
    driverName: "R. Okimaw",
    enteredBy: null,
    performedAt: "2026-10-01T12:00:00Z",
    odometerKm: null,
    result: "Fail",
    checklist: [],
    defects,
    generatedWorkOrderId: null,
  }) as unknown as VehicleInspection;

describe("VehicleInspections — live defect state", () => {
  it("keeps the result chip and adds the open count; a cleared line names its reason", async () => {
    listVehicleWorkOrders.mockResolvedValue([]);
    listInspections.mockResolvedValue([
      inspection([
        defect({ item: "Brakes", severity: "OutOfService" }),
        defect({ item: "Wipers", severity: "Minor", resolutionReason: "AcceptedMonitoring", resolvedAtUtc: "2026-10-02T00:00:00Z" }),
      ]),
    ]);
    render(<VehicleInspections vehicle={vehicle} vehicles={[]} />);

    expect(await screen.findByText("Fail")).toBeTruthy();
    expect(screen.getByText("1 open defect")).toBeTruthy();
    expect(screen.getByText("Resolved · Accepted, monitoring")).toBeTruthy();
  });

  it("re-reads once, after the projector window, when a defect is resolved elsewhere", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    listVehicleWorkOrders.mockResolvedValue([]);
    listInspections.mockResolvedValue([inspection([defect({ item: "Brakes" })])]);
    render(<VehicleInspections vehicle={vehicle} vehicles={[]} />);
    await screen.findByText("1 open defect");
    expect(listInspections).toHaveBeenCalledTimes(1);

    listInspections.mockResolvedValue([
      inspection([defect({ item: "Brakes", resolutionReason: "ReportedInError", resolvedAtUtc: "2026-10-02T00:00:00Z" })]),
    ]);
    act(() => notifyDefectsChanged());
    expect(listInspections).toHaveBeenCalledTimes(1);

    await act(async () => {
      vi.advanceTimersByTime(PROJECTION_RECONCILE_MS);
    });
    await screen.findByText("Defects resolved");
    expect(screen.getByText("Fail")).toBeTruthy();
    await waitFor(() => expect(listInspections).toHaveBeenCalledTimes(2));
  });

  it("says a duplicate was cleared with another report, naming the trip from the loaded list", async () => {
    listVehicleWorkOrders.mockResolvedValue([]);
    const resolved = { resolutionReason: "PreviouslyRepaired" as const, resolvedAtUtc: "2026-10-06T00:00:00Z" };
    listInspections.mockResolvedValue([
      // The report that was resolved by hand — the LATER one.
      {
        ...inspection([defect({ item: "Brakes", ...resolved })]),
        id: "insp-2",
        tripNumber: "NL-2026-0050",
        performedAt: "2026-10-05T12:00:00Z",
      },
      // Its duplicate, cleared with it.
      inspection([defect({ item: "brakes", ...resolved, resolvedWithInspectionId: "insp-2" })]),
    ]);
    render(<VehicleInspections vehicle={vehicle} vehicles={[]} />);

    expect(await screen.findByText("· cleared with the later report on NL-2026-0050")).toBeTruthy();
    expect(screen.getAllByText(/cleared with/)).toHaveLength(1);
  });
});
