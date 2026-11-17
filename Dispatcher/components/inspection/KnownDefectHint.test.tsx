import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import ChecklistGroupEditor from "./ChecklistGroupEditor";
import { rowsFor } from "./checklistRows";
import InspectionEntryModal from "@/components/InspectionEntryModal";
import TripInspectionModal from "@/components/TripInspectionModal";
import { itemsFor } from "@/lib/inspectionForm";
import { knownDefectsByItem } from "@/lib/defectDuplicates";
import { vehicleDefect } from "@/lib/defect.fixtures";
import type { VehicleDefectWire, VehicleInspection } from "@/lib/api/maintenance";
import type { TripRecord } from "@/lib/api/trips";

// The pre/post-trip forms show which checklist components ALREADY have an open
// defect on the unit, so a known fault reads as known rather than new. The hint
// is informational only: it never ticks, fills, blocks or changes a row, and a
// fresh Defect mark on a known fault stays the driver's own call.

const { listVehicleDefects, listDrivers, listVehicles } = vi.hoisted(() => ({
  listVehicleDefects: vi.fn<(vehicleId: string, includeResolved?: boolean) => Promise<VehicleDefectWire[]>>(),
  listDrivers: vi.fn<() => Promise<{ name: string; status: string }[]>>(),
  listVehicles: vi.fn<() => Promise<{ id: string; unitNumber: string }[]>>(),
}));

vi.mock("@/lib/api/maintenance", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/maintenance")>()),
  listVehicleDefects,
}));
vi.mock("@/lib/api/drivers", () => ({ listDrivers }));
vi.mock("@/lib/api/fleet", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/fleet")>()),
  listVehicles,
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

// NL-02's "Passenger seats" row: key "Passenger seats", label "Passenger seats (NL-02)".
const SEATS_KEY = "Passenger seats";
const SEATS_LABEL = "Passenger seats (NL-02)";
const NL02_PRE = itemsFor("NL-02", "PreTrip");
const SEATS_GROUP = NL02_PRE.find((g) => g.items.some((i) => i.key === SEATS_KEY))!;
const OTHER_KEY = NL02_PRE[0].items[0].key;

function hintRowLabels(): string[] {
  return screen.getAllByTestId("known-defect-hint").map((h) => h.parentElement?.textContent ?? "");
}

describe("ChecklistGroupEditor — a known-fault hint never changes a row", () => {
  it("shows beside the matching row and leaves every answer exactly as given", () => {
    const rows = rowsFor("NL-02", "PreTrip").filter((r) => r.groupKey === SEATS_GROUP.key);
    const onPatch = vi.fn();
    const known = knownDefectsByItem([
      vehicleDefect({ inspectionId: "i-1", item: SEATS_KEY, severity: "Major", tripNumber: "NL-2026-0040", reportedAt: "2026-10-01T12:00:00Z" }),
      vehicleDefect({ inspectionId: "i-2", item: "passenger seats", severity: "Minor", tripNumber: "NL-2026-0044", reportedAt: "2026-10-03T12:00:00Z" }),
    ]);
    render(<ChecklistGroupEditor group={SEATS_GROUP} rows={rows} onPatch={onPatch} knownDefects={known} />);

    const hints = screen.getAllByTestId("known-defect-hint");
    expect(hints).toHaveLength(1);
    // Severity chip (worst report) with its glyph, open since the EARLIEST report.
    expect(hints[0].textContent).toContain("▲");
    expect(hints[0].textContent).toContain("Major");
    expect(hints[0].textContent).toMatch(/Open defect since .+ · NL-2026-0040 · \+1 more report$/);
    expect(hintRowLabels()[0]).toContain(SEATS_LABEL);

    // Nothing was patched, ticked or pre-filled: every row is still unanswered.
    expect(onPatch).not.toHaveBeenCalled();
    expect(screen.getAllByText("Unanswered")).toHaveLength(rows.length);
    expect(screen.queryByText("Severity")).toBeNull();
    expect(screen.getByText(`MARK ${rows.length} UNANSWERED OK`)).toBeTruthy();
  });
});

describe("InspectionEntryModal — the unit's open defects", () => {
  const entry = () =>
    render(
      <InspectionEntryModal vehicleId="veh-2" unit="NL-02" type="PreTrip" odometerKm={1000} onClose={() => {}} onSaved={() => {}} />,
    );

  it("matches a row by its item KEY, never its label, and summarises at the top", async () => {
    listDrivers.mockResolvedValue([{ name: "R. Okimaw", status: "Active" }]);
    listVehicleDefects.mockResolvedValue([
      vehicleDefect({ inspectionId: "i-1", item: SEATS_KEY, severity: "Minor" }),
      // A defect stored under the row's LABEL is not that row's key.
      vehicleDefect({ inspectionId: "i-2", item: SEATS_LABEL, severity: "Minor" }),
      vehicleDefect({ inspectionId: "i-3", item: OTHER_KEY, severity: "Major" }),
    ]);
    entry();

    await screen.findByText("NL-02 has 3 open defects");
    expect(listVehicleDefects).toHaveBeenCalledWith("veh-2");
    const labels = hintRowLabels();
    expect(labels).toHaveLength(2);
    expect(labels.some((l) => l.includes(SEATS_LABEL))).toBe(true);
    // The label-spelled report has no row of its own, so the summary names it.
    expect(screen.getByText(new RegExp(`Not on any row here: ${SEATS_LABEL.replace(/[()]/g, "\\$&")}\\.`))).toBeTruthy();

    // Informational only: all 64 rows still unanswered, nothing marked Defect.
    expect(screen.getAllByText("Unanswered")).toHaveLength(64);
    expect(screen.queryByText("Severity")).toBeNull();
  });

  it("fails soft: one dim line, and saving is not blocked", async () => {
    listDrivers.mockResolvedValue([{ name: "R. Okimaw", status: "Active" }]);
    listVehicleDefects.mockRejectedValue(new Error("offline"));
    entry();

    await screen.findByTestId("known-defects-error");
    expect(screen.queryAllByTestId("known-defect-hint")).toHaveLength(0);
    expect(screen.getByText("SAVE INSPECTION").closest("button")?.disabled ?? false).toBe(false);
  });
});

describe("TripInspectionModal — the picked unit's open defects", () => {
  const trip = {
    id: "t-1",
    tripNumber: "NL-2026-0060",
    driverName: "R. Okimaw",
    vehicleId: "veh-2",
    vehicleUnit: "NL-02",
  } as unknown as TripRecord;

  function setup() {
    listDrivers.mockResolvedValue([{ name: "R. Okimaw", status: "Active" }]);
    listVehicles.mockResolvedValue([
      { id: "veh-1", unitNumber: "NL-01" },
      { id: "veh-2", unitNumber: "NL-02" },
    ]);
  }

  it("leaves out the edited inspection's own defects, so its rows do not flag themselves", async () => {
    setup();
    const existing = {
      id: "insp-own",
      type: "PreTrip",
      source: "Dispatcher",
      tripNumber: trip.tripNumber,
      vehicleId: "veh-2",
      unit: "NL-02",
      driverName: "R. Okimaw",
      odometerKm: null,
      location: "Leaf Rapids",
      checklist: [],
      defects: [
        { item: SEATS_KEY, severity: "Minor", note: "Torn", workOrderId: null, resolutionReason: null, resolvedAtUtc: null, resolvedWithInspectionId: null },
      ],
      weather: [],
      roadConditions: [],
      issues: [],
    } as unknown as VehicleInspection;
    listVehicleDefects.mockResolvedValue([
      vehicleDefect({ inspectionId: "insp-own", item: SEATS_KEY, severity: "Minor" }),
      vehicleDefect({ inspectionId: "insp-earlier", item: OTHER_KEY, severity: "Major" }),
    ]);
    render(
      <TripInspectionModal trip={trip} type="PreTrip" existing={existing} enteredBy="Dispatch" onClose={() => {}} onSaved={async () => {}} />,
    );

    await screen.findByText("NL-02 has 1 open defect");
    const labels = hintRowLabels();
    expect(labels).toHaveLength(1);
    expect(labels[0]).not.toContain(SEATS_LABEL);
  });

  it("re-loads for a newly picked unit, and the previous unit's late answer never paints under it", async () => {
    setup();
    let answerNl02: (rows: VehicleDefectWire[]) => void = () => {};
    listVehicleDefects.mockImplementation((vehicleId) =>
      vehicleId === "veh-2" ? new Promise((resolve) => (answerNl02 = resolve)) : Promise.resolve([]),
    );
    render(<TripInspectionModal trip={trip} type="PreTrip" enteredBy="Dispatch" onClose={() => {}} onSaved={async () => {}} />);

    await screen.findByText("Checking NL-02 for open defects…");
    await waitFor(() => expect(screen.getByRole("option", { name: "NL-01" })).toBeTruthy());
    fireEvent.change(screen.getAllByRole("combobox")[0], { target: { value: "NL-01" } });

    await screen.findByText("No open defects on NL-01");
    expect(listVehicleDefects).toHaveBeenCalledWith("veh-1");

    // NL-02's request answers late, with defects — none of it may appear.
    await act(async () => {
      answerNl02([vehicleDefect({ inspectionId: "i-9", item: OTHER_KEY, severity: "OutOfService" })]);
    });
    expect(screen.getByText("No open defects on NL-01")).toBeTruthy();
    expect(screen.queryAllByTestId("known-defect-hint")).toHaveLength(0);
  });
});
