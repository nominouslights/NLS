import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import DefectsPanel, { RESOLVE_PREVIEW_TIMEOUT_MS, ResolveDefectModal } from "./DefectsPanel";
import type { DefectDuplicateWire, ResolvedDefect, VehicleDefectWire } from "@/lib/api/maintenance";
import { vehicleDefect } from "@/lib/defect.fixtures";
import { DEFECTS_CHANGED_EVENT, PROJECTION_RECONCILE_MS, notifyDefectsChanged } from "@/lib/defectSync";

// CREATE WORK ORDER is an action, offered only where the parent passes
// onCreateWorkOrder (the vehicle's Open Defects tab — never the trip detail), and
// only on an open defect that is not already on an open work order.

const { listVehicleDefects, previewDefectDuplicates, resolveInspectionDefect } = vi.hoisted(() => ({
  listVehicleDefects: vi.fn<(vehicleId: string, includeResolved?: boolean) => Promise<VehicleDefectWire[]>>(),
  previewDefectDuplicates: vi.fn<(inspectionId: string, item: string) => Promise<DefectDuplicateWire[]>>(),
  resolveInspectionDefect: vi.fn<(inspectionId: string, body: unknown) => Promise<ResolvedDefect[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  listVehicleDefects,
  previewDefectDuplicates,
  resolveInspectionDefect,
}));

/** One row of the server's duplicate preview. */
const dup = (over: Partial<DefectDuplicateWire> & { inspectionId: string }): DefectDuplicateWire => ({
  item: "Brakes",
  tripNumber: null,
  severity: "Minor",
  reportedAt: "2026-10-01T12:00:00Z",
  ...over,
});

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
    previewDefectDuplicates.mockResolvedValueOnce([]);
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
      // The modal's duplicate preview asks the server, not the projection.
      await screen.findByText(/No other open reports of Brakes/);
      expect(previewDefectDuplicates).toHaveBeenCalledWith("i-1", "Brakes");
      expect(listVehicleDefects).toHaveBeenCalledTimes(1);
      fireEvent.change(screen.getByRole("combobox"), { target: { value: "ReportedInError" } });
      fireEvent.click(screen.getByText("RESOLVE DEFECT"));

      await waitFor(() => expect(screen.queryByText("Brakes")).toBeNull());
      expect(resolveInspectionDefect).toHaveBeenCalledWith("i-1", expect.objectContaining({ item: "Brakes" }));
      expect(announced).toHaveBeenCalledTimes(1);
      // Not immediately — the projection is still stale.
      expect(listVehicleDefects).toHaveBeenCalledTimes(1);

      await act(async () => {
        vi.advanceTimersByTime(PROJECTION_RECONCILE_MS);
      });
      await waitFor(() => expect(listVehicleDefects).toHaveBeenCalledTimes(2));
      await act(async () => {
        vi.advanceTimersByTime(PROJECTION_RECONCILE_MS * 2);
      });
      expect(listVehicleDefects).toHaveBeenCalledTimes(2);
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

// A known fault is re-reported on every daily pre-trip until it is fixed. Only a
// REPAIR clears the other open reports of it; "Reported in error" and "Accepted,
// monitoring" clear the addressed report alone. The modal asks the SERVER which
// other reports exist (never the projection), says what the chosen reason will
// do with them before the confirm, and sends exactly what it showed.
describe("ResolveDefectModal — duplicate preview", () => {
  function renderModal(severity: VehicleDefectWire["severity"] | undefined = "Minor") {
    const onResolved = vi.fn();
    render(
      <ResolveDefectModal
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
  const pick = (reason: string) => fireEvent.change(screen.getByRole("combobox"), { target: { value: reason } });
  const sentBody = () => resolveInspectionDefect.mock.calls[0][1] as { clearDuplicates: unknown };

  // Server order is oldest first; the client never re-sorts it.
  const twoDuplicates = [
    dup({ inspectionId: "i-3", item: " brakes ", tripNumber: "NL-2026-0048" }),
    dup({ inspectionId: "i-2", item: "BRAKES", tripNumber: null }),
  ];

  it("asks the server, not the projection, and says plainly when there is nothing else", async () => {
    previewDefectDuplicates.mockResolvedValueOnce([]);
    renderModal();

    await screen.findByText("No other open reports of Brakes on NL-02 — this clears this report only.");
    expect(previewDefectDuplicates).toHaveBeenCalledWith("i-1", "Brakes");
    expect(listVehicleDefects).not.toHaveBeenCalled();
    expect(screen.queryAllByTestId("duplicate-report")).toHaveLength(0);
    expect(screen.getByText("RESOLVE DEFECT")).toBeTruthy();
  });

  it("shows a neutral count, in server order, until a reason is picked", async () => {
    previewDefectDuplicates.mockResolvedValueOnce(twoDuplicates);
    renderModal();

    await screen.findByText("2 other open reports of Brakes on NL-02");
    expect(
      screen.getByText("Previously repaired clears them with this one; the other reasons clear only this report."),
    ).toBeTruthy();
    const listed = screen.getAllByTestId("duplicate-report");
    expect(listed).toHaveLength(2);
    expect(listed[0].textContent).toContain("NL-2026-0048");
    expect(listed[1].textContent).toContain("no trip");
    // Each one carries its severity chip: colour + glyph + words.
    expect(listed[0].textContent).toContain("◐");
    expect(listed[0].textContent).toContain("Minor");
    expect(screen.getByText("RESOLVE DEFECT")).toBeTruthy();
    expect(screen.queryByText(/Includes/)).toBeNull();
  });

  it("Previously repaired clears them too, counts them, and sends exactly the previewed list", async () => {
    previewDefectDuplicates.mockResolvedValueOnce(twoDuplicates);
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: "Minor" },
    ]);
    const { onResolved } = renderModal();

    await screen.findByText("2 other open reports of Brakes on NL-02");
    pick("PreviouslyRepaired");
    expect(screen.getByText("This also clears 2 other open reports of Brakes on NL-02")).toBeTruthy();
    expect(screen.getAllByTestId("duplicate-report")).toHaveLength(2);
    fireEvent.click(screen.getByText("RESOLVE 3 REPORTS"));

    await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1));
    expect(resolveInspectionDefect).toHaveBeenCalledWith("i-1", {
      item: "Brakes",
      reason: "PreviouslyRepaired",
      note: null,
      resolvedBy: "Dispatch",
      // The items exactly as the server spelled them, in its order.
      clearDuplicates: [
        { inspectionId: "i-3", item: " brakes " },
        { inspectionId: "i-2", item: "BRAKES" },
      ],
    });
  });

  it.each(["ReportedInError", "AcceptedMonitoring"])(
    "%s clears this report only, says the others stay open, and sends null",
    async (reason) => {
      previewDefectDuplicates.mockResolvedValueOnce(twoDuplicates);
      resolveInspectionDefect.mockResolvedValueOnce([
        { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: "Minor" },
      ]);
      const { onResolved } = renderModal();

      await screen.findByText("2 other open reports of Brakes on NL-02");
      pick(reason);
      expect(
        screen.getByText("Only this report is cleared — 2 other open reports of Brakes on NL-02 stay open"),
      ).toBeTruthy();
      expect(screen.queryByText(/This also clears/)).toBeNull();
      expect(screen.queryByText("RESOLVE 3 REPORTS")).toBeNull();
      fireEvent.click(screen.getByText("RESOLVE DEFECT"));

      await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1));
      expect(sentBody().clearDuplicates).toBeNull();
    },
  );

  it("words a single other report in the singular", async () => {
    previewDefectDuplicates.mockResolvedValueOnce([dup({ inspectionId: "i-2" })]);
    renderModal();

    await screen.findByText("1 other open report of Brakes on NL-02");
    pick("AcceptedMonitoring");
    expect(
      screen.getByText("Only this report is cleared — 1 other open report of Brakes on NL-02 stays open"),
    ).toBeTruthy();
    pick("PreviouslyRepaired");
    expect(screen.getByText("This also clears 1 other open report of Brakes on NL-02")).toBeTruthy();
    expect(screen.getByText("RESOLVE 2 REPORTS")).toBeTruthy();
  });

  it("never clears a more severe report silently, and names it only when it would be cleared", async () => {
    previewDefectDuplicates.mockResolvedValueOnce([
      dup({ inspectionId: "i-2", severity: "Major" }),
      dup({ inspectionId: "i-3", item: "brakes", severity: "OutOfService", tripNumber: "NL-2026-0049" }),
    ]);
    renderModal("Minor");

    await screen.findByText("2 other open reports of Brakes on NL-02");
    expect(screen.queryByText(/Includes/)).toBeNull();

    pick("PreviouslyRepaired");
    // The worst one is named, with Out-of-Service's own ✕ glyph.
    expect(screen.getByText(/Includes an Out-of-Service report/).textContent).toBe(
      "✕ Includes an Out-of-Service report — more severe than the Minor one being resolved.",
    );
    expect(screen.getByText("RESOLVE 3 REPORTS")).toBeTruthy();

    pick("ReportedInError");
    expect(screen.queryByText(/Includes/)).toBeNull();
  });

  it("names a Major duplicate when that is the worst", async () => {
    previewDefectDuplicates.mockResolvedValueOnce([dup({ inspectionId: "i-2", severity: "Major" })]);
    renderModal("Minor");
    await screen.findByText("1 other open report of Brakes on NL-02");
    pick("PreviouslyRepaired");
    expect(screen.getByText(/Includes a Major report/)).toBeTruthy();
  });

  it("keeps the resolve available when the preview fails, and clears this report only", async () => {
    previewDefectDuplicates.mockRejectedValueOnce(new Error("offline"));
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: "Minor" },
    ]);
    const { onResolved } = renderModal();

    const line = await screen.findByTestId("duplicate-preview");
    expect(line.textContent).toBe("◐ Couldn't check for other reports — only this report will be cleared.");
    // Even a repair sends nothing to clear: nothing else was shown.
    pick("PreviouslyRepaired");
    fireEvent.click(screen.getByText("RESOLVE DEFECT"));
    await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1));
    expect(sentBody().clearDuplicates).toBeNull();
  });

  it("holds the confirm only while checking, stops waiting after the timeout, and ignores a late answer", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    let answer: (d: DefectDuplicateWire[]) => void = () => {};
    previewDefectDuplicates.mockReturnValueOnce(new Promise((r) => (answer = r)));
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: "Minor" },
    ]);
    const { onResolved } = renderModal();

    expect(screen.getByText("CHECKING…")).toBeTruthy();
    pick("PreviouslyRepaired");
    fireEvent.click(screen.getByText("CHECKING…"));
    expect(resolveInspectionDefect).not.toHaveBeenCalled();

    await act(async () => {
      vi.advanceTimersByTime(RESOLVE_PREVIEW_TIMEOUT_MS);
    });
    await screen.findByText(/Couldn.t check for other reports/);
    expect(screen.getByText("RESOLVE DEFECT")).toBeTruthy();

    // The preview answers after all — too late: the confirm must not change
    // under the dispatcher's cursor.
    await act(async () => answer([dup({ inspectionId: "i-2" })]));
    expect(screen.getByText(/Couldn.t check for other reports/)).toBeTruthy();
    expect(screen.getByText("RESOLVE DEFECT")).toBeTruthy();

    fireEvent.click(screen.getByText("RESOLVE DEFECT"));
    await waitFor(() => expect(onResolved).toHaveBeenCalledTimes(1));
    expect(sentBody().clearDuplicates).toBeNull();
  });
});

describe("DefectsPanel — only what the response lists drops", () => {
  const brakes = [
    vehicleDefect({ inspectionId: "i-1", item: "Brakes", severity: "Major", tripNumber: "NL-2026-0050" }),
    vehicleDefect({ inspectionId: "i-2", item: "Wipers", severity: "Minor" }),
    vehicleDefect({ inspectionId: "i-3", item: "brakes", severity: "Minor", tripNumber: "NL-2026-0049" }),
    vehicleDefect({ inspectionId: "i-4", item: "Brakes ", severity: "Minor", tripNumber: "NL-2026-0048" }),
  ];
  const previewed = [
    dup({ inspectionId: "i-4", item: "Brakes ", tripNumber: "NL-2026-0048" }),
    dup({ inspectionId: "i-3", item: "brakes", tripNumber: "NL-2026-0049" }),
  ];
  const brakeRows = () => screen.queryAllByText(/^brakes\s*$/i);

  async function repairFirstBrakes() {
    render(<DefectsPanel vehicleId="veh-1" unit="NL-02" canResolve />);
    await screen.findByText("Wipers");
    expect(brakeRows()).toHaveLength(3);
    fireEvent.click(screen.getAllByText("RESOLVE")[0]);
    await screen.findByText("2 other open reports of Brakes on NL-02");
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "PreviouslyRepaired" } });
    fireEvent.click(screen.getByText("RESOLVE 3 REPORTS"));
  }

  it("drops every row a repair cleared at once, not just the addressed one", async () => {
    listVehicleDefects.mockResolvedValue(brakes);
    previewDefectDuplicates.mockResolvedValueOnce(previewed);
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: "NL-2026-0050", severity: "Major" },
      { inspectionId: "i-4", item: "Brakes ", tripNumber: "NL-2026-0048", severity: "Minor" },
      { inspectionId: "i-3", item: "brakes", tripNumber: "NL-2026-0049", severity: "Minor" },
    ]);
    await repairFirstBrakes();

    await waitFor(() => expect(brakeRows()).toHaveLength(0));
    expect(screen.getByText("Wipers")).toBeTruthy();
  });

  it("keeps a previewed row the server did not clear", async () => {
    listVehicleDefects.mockResolvedValue(brakes);
    previewDefectDuplicates.mockResolvedValueOnce(previewed);
    // i-3 stopped being a valid duplicate between the preview and the confirm.
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: "NL-2026-0050", severity: "Major" },
      { inspectionId: "i-4", item: "Brakes ", tripNumber: "NL-2026-0048", severity: "Minor" },
    ]);
    await repairFirstBrakes();

    await waitFor(() => expect(brakeRows()).toHaveLength(1));
    expect(screen.getByText("NL-2026-0049")).toBeTruthy();
  });

  it("drops just the addressed row when an older API answers with no body", async () => {
    listVehicleDefects.mockResolvedValue(brakes);
    previewDefectDuplicates.mockResolvedValueOnce(previewed);
    // What resolveInspectionDefect hands back for an empty body (see its own test).
    resolveInspectionDefect.mockResolvedValueOnce([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: null },
    ]);
    await repairFirstBrakes();

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
