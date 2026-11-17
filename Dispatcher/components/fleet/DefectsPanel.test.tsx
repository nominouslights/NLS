import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import DefectsPanel, { RESOLVE_PREVIEW_TIMEOUT_MS, ResolveDefectModal } from "./DefectsPanel";
import type { ResolvedDefect, VehicleDefectWire } from "@/lib/api/maintenance";
import { vehicleDefect } from "@/lib/defect.fixtures";
import { DEFECTS_CHANGED_EVENT, PROJECTION_RECONCILE_MS, notifyDefectsChanged } from "@/lib/defectSync";

// CREATE WORK ORDER is an action, offered only where the parent passes
// onCreateWorkOrder (the vehicle's Open Defects tab — never the trip detail), and
// only on an open defect that is not already on an open work order.

const { listVehicleDefects, resolveInspectionDefect } = vi.hoisted(() => ({
  listVehicleDefects: vi.fn<(vehicleId: string, includeResolved?: boolean) => Promise<VehicleDefectWire[]>>(),
  resolveInspectionDefect: vi.fn<(inspectionId: string, body: unknown) => Promise<ResolvedDefect[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  listVehicleDefects,
  resolveInspectionDefect,
}));

afterEach(() => {
  cleanup();
  vi.useRealTimers();
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

// Resolving is announced to every other surface (lib/defectSync), and this
// panel's own reconcile rides the same seam: the row drops at once, and the
// panel re-reads ONCE, only after the projector has run — an immediate re-read
// would see the stale projection and bring the row back.
describe("DefectsPanel — linked resolve", () => {
  it("drops the row now, announces it once, and reloads once after the projector window", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    listVehicleDefects.mockResolvedValue(rows);
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: "NL-2026-0042", severity: "OutOfService" },
    ]);
    const announced = vi.fn();
    window.addEventListener(DEFECTS_CHANGED_EVENT, announced);
    try {
      render(<DefectsPanel vehicleId="veh-1" unit="NL-02" canResolve />);
      await screen.findByText("Brakes");
      expect(listVehicleDefects).toHaveBeenCalledTimes(1);

      fireEvent.click(screen.getAllByText("RESOLVE")[0]);
      // The modal's duplicate preview is the second read.
      await screen.findByText(/No other open reports of Brakes/);
      expect(listVehicleDefects).toHaveBeenCalledTimes(2);
      fireEvent.change(screen.getByRole("combobox"), { target: { value: "ReportedInError" } });
      fireEvent.click(screen.getByText("RESOLVE DEFECT"));

      await waitFor(() => expect(screen.queryByText("Brakes")).toBeNull());
      expect(resolveInspectionDefect).toHaveBeenCalledWith("i-1", expect.objectContaining({ item: "Brakes" }));
      expect(announced).toHaveBeenCalledTimes(1);
      // Not immediately — the projection is still stale.
      expect(listVehicleDefects).toHaveBeenCalledTimes(2);

      await act(async () => {
        vi.advanceTimersByTime(PROJECTION_RECONCILE_MS);
      });
      await waitFor(() => expect(listVehicleDefects).toHaveBeenCalledTimes(3));
      await act(async () => {
        vi.advanceTimersByTime(PROJECTION_RECONCILE_MS * 2);
      });
      expect(listVehicleDefects).toHaveBeenCalledTimes(3);
    } finally {
      window.removeEventListener(DEFECTS_CHANGED_EVENT, announced);
    }
  });

  it("re-reads when a defect changes on another surface", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    listVehicleDefects.mockResolvedValue(rows);
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" />);
    await screen.findByText("Brakes");

    // e.g. resolved from the inspection detail, or a work order completed.
    act(() => notifyDefectsChanged());
    expect(listVehicleDefects).toHaveBeenCalledTimes(1);
    listVehicleDefects.mockResolvedValue(rows.filter((r) => r.item !== "Brakes"));
    await act(async () => {
      vi.advanceTimersByTime(PROJECTION_RECONCILE_MS);
    });
    await waitFor(() => expect(screen.queryByText("Brakes")).toBeNull());
    expect(listVehicleDefects).toHaveBeenCalledTimes(2);
  });
});

// A known fault is re-reported on every daily pre-trip until it is fixed, so
// resolving one report clears every open duplicate of it (same vehicle, same
// item trimmed and case-insensitive). The modal previews that BEFORE the
// confirm, and never clears a more severe report without saying so.
describe("ResolveDefectModal — duplicate preview", () => {
  const addressed = vehicleDefect({ inspectionId: "i-1", item: "Brakes", severity: "Minor", tripNumber: "NL-2026-0050" });

  function renderModal(severity: VehicleDefectWire["severity"] | undefined = "Minor") {
    const onResolved = vi.fn();
    render(
      <ResolveDefectModal
        vehicleId="veh-1"
        inspectionId="i-1"
        item="Brakes"
        unit="NL-02"
        severity={severity}
        onClose={() => {}}
        onResolved={onResolved}
      />,
    );
    return { onResolved };
  }

  it("says plainly when there is nothing else to clear", async () => {
    listVehicleDefects.mockResolvedValueOnce([addressed, vehicleDefect({ inspectionId: "i-2", item: "Wipers" })]);
    renderModal();

    await screen.findByText("No other open reports of Brakes on NL-02 — this clears this report only.");
    expect(listVehicleDefects).toHaveBeenCalledWith("veh-1");
    expect(screen.queryAllByTestId("duplicate-report")).toHaveLength(0);
    expect(screen.getByText("RESOLVE DEFECT")).toBeTruthy();
  });

  it("lists every open duplicate (trimmed, case-insensitive) in server order, and counts them on the confirm", async () => {
    listVehicleDefects.mockResolvedValueOnce([
      vehicleDefect({ inspectionId: "i-3", item: " brakes ", severity: "Minor", tripNumber: "NL-2026-0048" }),
      addressed,
      vehicleDefect({ inspectionId: "i-2", item: "BRAKES", severity: "Minor", tripNumber: null }),
      // Not duplicates: another component, and a report already resolved.
      vehicleDefect({ inspectionId: "i-4", item: "Brake lights", severity: "Minor" }),
      vehicleDefect({
        inspectionId: "i-5",
        item: "Brakes",
        resolvedAtUtc: "2026-09-01T00:00:00Z",
        resolutionReason: "ReportedInError",
      }),
    ]);
    renderModal();

    await screen.findByText("This also clears 2 other open reports of Brakes on NL-02");
    const listed = screen.getAllByTestId("duplicate-report");
    expect(listed).toHaveLength(2);
    // Server order kept, never re-sorted.
    expect(listed[0].textContent).toContain("NL-2026-0048");
    expect(listed[1].textContent).toContain("no trip");
    // Each one carries its severity chip: colour + glyph + words.
    expect(listed[0].textContent).toContain("◐");
    expect(listed[0].textContent).toContain("Minor");
    expect(screen.getByText("RESOLVE 3 REPORTS")).toBeTruthy();
    // Same severity: no "more severe" warning.
    expect(screen.queryByText(/Includes/)).toBeNull();
  });

  it("never clears a more severe report silently", async () => {
    listVehicleDefects.mockResolvedValueOnce([
      vehicleDefect({ inspectionId: "i-2", item: "Brakes", severity: "OutOfService", tripNumber: "NL-2026-0049" }),
      vehicleDefect({ inspectionId: "i-3", item: "brakes", severity: "Major" }),
      addressed,
    ]);
    renderModal("Minor");

    await screen.findByText("This also clears 2 other open reports of Brakes on NL-02");
    // The worst one is named, with Out-of-Service's own ✕ glyph.
    const warning = screen.getByText(/Includes an Out-of-Service report/);
    expect(warning.textContent).toBe(
      "✕ Includes an Out-of-Service report — more severe than the Minor one being resolved.",
    );
    expect(screen.getByText("RESOLVE 3 REPORTS")).toBeTruthy();
  });

  it("names a Major duplicate when that is the worst", async () => {
    listVehicleDefects.mockResolvedValueOnce([
      vehicleDefect({ inspectionId: "i-2", item: "Brakes", severity: "Major" }),
      addressed,
    ]);
    renderModal("Minor");
    await screen.findByText("This also clears 1 other open report of Brakes on NL-02");
    expect(screen.getByText(/Includes a Major report/)).toBeTruthy();
    expect(screen.getByText("RESOLVE 2 REPORTS")).toBeTruthy();
  });

  it("keeps the resolve available with an honest line when the preview fails", async () => {
    listVehicleDefects.mockRejectedValueOnce(new Error("offline"));
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: "Minor" },
    ]);
    const { onResolved } = renderModal();

    await screen.findByText(/Couldn.t check for duplicate reports/);
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "PreviouslyRepaired" } });
    fireEvent.click(screen.getByText("RESOLVE DEFECT"));
    await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1));
    expect(resolveInspectionDefect).toHaveBeenCalledWith("i-1", expect.objectContaining({ item: "Brakes" }));
  });

  it("holds the confirm only while checking, and stops waiting after the timeout", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    listVehicleDefects.mockReturnValueOnce(new Promise(() => {}));
    renderModal();

    expect(screen.getByText("CHECKING…")).toBeTruthy();
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "PreviouslyRepaired" } });
    fireEvent.click(screen.getByText("CHECKING…"));
    expect(resolveInspectionDefect).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(RESOLVE_PREVIEW_TIMEOUT_MS);
    });
    await screen.findByText(/Couldn.t check for duplicate reports/);
    expect(screen.getByText("RESOLVE DEFECT")).toBeTruthy();
  });
});

describe("DefectsPanel — a resolve clears the duplicates too", () => {
  const brakes = [
    vehicleDefect({ inspectionId: "i-1", item: "Brakes", severity: "Major", tripNumber: "NL-2026-0050" }),
    vehicleDefect({ inspectionId: "i-2", item: "Wipers", severity: "Minor" }),
    vehicleDefect({ inspectionId: "i-3", item: "brakes", severity: "Minor", tripNumber: "NL-2026-0049" }),
    vehicleDefect({ inspectionId: "i-4", item: "Brakes ", severity: "Minor", tripNumber: "NL-2026-0048" }),
  ];
  const brakeRows = () => screen.queryAllByText(/^brakes\s*$/i);

  it("drops every row in the response at once, not just the addressed one", async () => {
    listVehicleDefects.mockResolvedValue(brakes);
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: "NL-2026-0050", severity: "Major" },
      { inspectionId: "i-4", item: "Brakes ", tripNumber: "NL-2026-0048", severity: "Minor" },
      { inspectionId: "i-3", item: "brakes", tripNumber: "NL-2026-0049", severity: "Minor" },
    ]);
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" canResolve />);
    await screen.findByText("Wipers");
    expect(brakeRows()).toHaveLength(3);

    fireEvent.click(screen.getAllByText("RESOLVE")[0]);
    await screen.findByText("This also clears 2 other open reports of Brakes on NL-02");
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "PreviouslyRepaired" } });
    fireEvent.click(screen.getByText("RESOLVE 3 REPORTS"));

    await waitFor(() => expect(brakeRows()).toHaveLength(0));
    expect(screen.getByText("Wipers")).toBeTruthy();
  });

  it("drops just the addressed row when an older API answers with no body", async () => {
    listVehicleDefects.mockResolvedValue(brakes);
    // What resolveInspectionDefect hands back for an empty body (see its own test).
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: null },
    ]);
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" canResolve />);
    await screen.findByText("Wipers");

    fireEvent.click(screen.getAllByText("RESOLVE")[0]);
    await screen.findByText(/This also clears/);
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "PreviouslyRepaired" } });
    fireEvent.click(screen.getByText("RESOLVE 3 REPORTS"));

    await waitFor(() => expect(brakeRows()).toHaveLength(2));
  });

  it("says a resolved row was cleared with another report, naming its trip from the loaded rows", async () => {
    listVehicleDefects.mockResolvedValue([
      vehicleDefect({
        inspectionId: "i-1",
        item: "Brakes",
        tripNumber: "NL-2026-0050",
        reportedAt: "2026-10-05T12:00:00Z",
        resolutionReason: "PreviouslyRepaired",
        resolvedAtUtc: "2026-10-06T00:00:00Z",
      }),
      vehicleDefect({
        inspectionId: "i-3",
        item: "brakes",
        tripNumber: "NL-2026-0049",
        reportedAt: "2026-10-03T12:00:00Z",
        resolutionReason: "PreviouslyRepaired",
        resolvedAtUtc: "2026-10-06T00:00:00Z",
        resolvedWithInspectionId: "i-1",
      }),
      vehicleDefect({
        inspectionId: "i-9",
        item: "Horn",
        resolutionReason: "PreviouslyRepaired",
        resolvedAtUtc: "2026-10-06T00:00:00Z",
        resolvedWithInspectionId: "i-unloaded",
      }),
    ]);
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" showResolvedToggle />);
    await screen.findByText("Horn");

    expect(screen.getByText("· cleared with the later report on NL-2026-0050")).toBeTruthy();
    // Not among the loaded rows: said generically, never fetched per row.
    expect(screen.getByText("· cleared with another report of this fault")).toBeTruthy();
    expect(screen.getAllByText(/cleared with/)).toHaveLength(2);
    expect(listVehicleDefects).toHaveBeenCalledTimes(1);
  });
});
