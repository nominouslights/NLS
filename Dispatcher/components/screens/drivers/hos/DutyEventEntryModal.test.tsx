import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError } from "@/lib/api/transport";
import { HosMessages } from "@/lib/hos/engine";
import DutyEventEntryModal, { quickFillRows, validateRows } from "./DutyEventEntryModal";

// The transcription modal: what it offers, what it refuses, what it previews, and the exact
// body it POSTs. The source list is the load-bearing one — a console entry labelled
// "Driver App" would corrupt the HOS record.

const { transcribeDay } = vi.hoisted(() => ({ transcribeDay: vi.fn() }));
vi.mock("@/lib/api/hos", () => ({ transcribeDay }));

const DATE = "2026-10-09";

function renderModal(onSaved = vi.fn()) {
  const onClose = vi.fn();
  const utils = render(<DutyEventEntryModal driverId="driver-1" driverName="K. Beardy" defaultDate={DATE} onClose={onClose} onSaved={onSaved} />);
  return { ...utils, onClose, onSaved };
}

function timeInputs(container: HTMLElement): HTMLInputElement[] {
  return [...container.querySelectorAll<HTMLInputElement>('input[type="time"]')];
}

function quickFill(container: HTMLElement, times: [string, string, string, string]) {
  const inputs = timeInputs(container);
  times.forEach((t, i) => fireEvent.change(inputs[i], { target: { value: t } }));
  fireEvent.click(screen.getByText("FILL ROWS"));
}

function textInputByPlaceholder(placeholder: string): HTMLInputElement {
  return screen.getByPlaceholderText(placeholder) as HTMLInputElement;
}

beforeEach(() => {
  transcribeDay.mockReset();
});

afterEach(cleanup);

describe("pure helpers", () => {
  it("quickFillRows yields the four rows in shift order", () => {
    expect(quickFillRows("06:00", "06:30", "14:00", "14:30").map((r) => [r.time, r.status])).toEqual([
      ["06:00", "On Duty"],
      ["06:30", "Driving"],
      ["14:00", "On Duty"],
      ["14:30", "Off Duty"],
    ]);
  });

  it("validateRows requires strictly increasing times and names the row", () => {
    expect(validateRows([])).toMatch(/at least one/);
    expect(validateRows(quickFillRows("06:00", "06:30", "14:00", "14:30"))).toBeNull();
    expect(validateRows(quickFillRows("06:00", "06:00", "14:00", "14:30"))).toBe("Row 2: times must be strictly increasing.");
    expect(validateRows(quickFillRows("06:00", "06:30", "06:15", "14:30"))).toBe("Row 3: times must be strictly increasing.");
    expect(validateRows([{ time: "", status: "On Duty", note: "" }])).toBe("Row 1: enter a time (HH:MM).");
  });
});

describe("DutyEventEntryModal", () => {
  it("offers ELD transcription and Manual (paper backup) only — never Driver App", () => {
    const { container } = renderModal();
    const select = container.querySelector("select") as HTMLSelectElement;
    const options = [...select.options].map((o) => o.value);
    expect(options).toEqual(["ELD transcription", "Manual (paper backup)"]);
    expect(options).not.toContain("Driver App");
  });

  it("quick-fill produces four ordered rows", () => {
    const { container } = renderModal();
    quickFill(container, ["06:00", "06:30", "14:00", "14:30"]);

    const rows = screen.getAllByTestId("entry-row");
    expect(rows).toHaveLength(4);
    const rowTimes = rows.map((r) => (r.querySelector('input[type="time"]') as HTMLInputElement).value);
    expect(rowTimes).toEqual(["06:00", "06:30", "14:00", "14:30"]);
    const rowStatuses = rows.map((r) => (r.querySelector("select") as HTMLSelectElement).value);
    expect(rowStatuses).toEqual(["On Duty", "Driving", "On Duty", "Off Duty"]);
  });

  it("refuses non-increasing times before any POST", () => {
    const { container } = renderModal();
    quickFill(container, ["06:00", "06:30", "06:15", "14:30"]);
    fireEvent.change(textInputByPlaceholder("Dispatch — your name"), { target: { value: "L. Moose" } });

    expect(screen.getAllByText("Row 3: times must be strictly increasing.").length).toBeGreaterThan(0);
    fireEvent.click(screen.getByText("RECORD DAY"));
    expect(transcribeDay).not.toHaveBeenCalled();
  });

  it("previews the engine's totals and a violation, and will not POST until it is acknowledged", async () => {
    const { container } = renderModal();
    // 14h30 of driving in one day — more than the 13h daily limit.
    quickFill(container, ["05:00", "05:30", "20:00", "20:30"]);
    fireEvent.change(textInputByPlaceholder("Dispatch — your name"), { target: { value: "L. Moose" } });

    expect(screen.getByText(/Driving 14h 30m · On duty 15h 30m/)).toBeTruthy();
    expect(screen.getByText(HosMessages.for("DailyDriving13h", 14 * 3600 + 1800, 13 * 3600))).toBeTruthy();
    expect(screen.getByText("The server will record this violation — continue?")).toBeTruthy();

    fireEvent.click(screen.getByText("RECORD DAY"));
    expect(transcribeDay).not.toHaveBeenCalled();
    expect(screen.getByText(/tick the acknowledgement/)).toBeTruthy();

    transcribeDay.mockResolvedValueOnce({ dutyDayId: "d", localDate: DATE, appended: 4, alreadyRecorded: 0 });
    fireEvent.click(container.querySelector('input[type="checkbox"]') as HTMLInputElement);
    fireEvent.click(screen.getByText("RECORD DAY"));
    await waitFor(() => expect(transcribeDay).toHaveBeenCalledTimes(1));
  });

  it("POSTs the exact body with a client transcriptionId, and reports the result", async () => {
    const { container, onSaved, onClose } = renderModal();
    quickFill(container, ["06:00", "06:30", "14:00", "14:30"]);
    fireEvent.change(textInputByPlaceholder("Dispatch — your name"), { target: { value: "  L. Moose " } });
    fireEvent.change(textInputByPlaceholder("Only for another carrier's hours"), { target: { value: "" } });
    transcribeDay.mockResolvedValueOnce({ dutyDayId: "d", localDate: DATE, appended: 4, alreadyRecorded: 0 });

    expect(screen.getByText("No violation on this day")).toBeTruthy();
    fireEvent.click(screen.getByText("RECORD DAY"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(DATE));
    expect(onClose).toHaveBeenCalled();
    const [driverId, date, body] = transcribeDay.mock.calls[0];
    expect(driverId).toBe("driver-1");
    expect(date).toBe(DATE);
    expect(body).toEqual({
      transcriptionId: expect.stringMatching(/^[0-9a-f-]{36}$/),
      source: "ELD transcription",
      enteredBy: "L. Moose",
      carrierName: null,
      entries: [
        { time: "06:00", status: "On Duty", note: null },
        { time: "06:30", status: "Driving", note: null },
        { time: "14:00", status: "On Duty", note: null },
        { time: "14:30", status: "Off Duty", note: null },
      ],
    });
  });

  it("shows the server's message verbatim when the all-or-nothing write is refused", async () => {
    const { container } = renderModal();
    quickFill(container, ["06:00", "06:30", "14:00", "14:30"]);
    fireEvent.change(textInputByPlaceholder("Dispatch — your name"), { target: { value: "L. Moose" } });
    const message = "2026-10-09 already has a Driver App record at 06:00; transcriptions cannot overwrite it.";
    transcribeDay.mockRejectedValueOnce(new ApiError("Drivers.Hos.ConflictingSource", message, 409));

    fireEvent.click(screen.getByText("RECORD DAY"));

    await waitFor(() => expect(screen.getByText(message)).toBeTruthy());
  });
});
