import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError } from "@/lib/api/transport";
import HosExceptionModal, {
  ADVERSE_ATTESTATION_TEXT,
  DECLARABLE_KINDS,
  EXTENSION_MAX_MINUTES,
  EXTENSION_MIN_MINUTES,
  minutesInRange,
  validateAdverse,
} from "./HosExceptionModal";

// The exception modal: an adverse declaration cannot leave without the knowability
// attestation and a 1–120 minute extension; an emergency needs neither; the deferral mode
// (compliance only) picks one of the driver's upcoming trips and posts to the compliance route.

const { declareHosException, approveOffDutyDeferral, listTrips } = vi.hoisted(() => ({
  declareHosException: vi.fn(),
  approveOffDutyDeferral: vi.fn(),
  listTrips: vi.fn(),
}));
vi.mock("@/lib/api/hos", () => ({ declareHosException, approveOffDutyDeferral }));
vi.mock("@/lib/api/trips", () => ({ listTrips }));

const TODAY = "2026-10-09";

function renderModal(mode: "declare" | "deferral" = "declare") {
  const onClose = vi.fn();
  const onSaved = vi.fn();
  const utils = render(<HosExceptionModal driverId="driver-1" driverName="K. Beardy" today={TODAY} mode={mode} onClose={onClose} onSaved={onSaved} />);
  return { ...utils, onClose, onSaved };
}

const reasonBox = () => screen.getByPlaceholderText(/Whiteout on PTH 6/) as HTMLTextAreaElement;
const minutesBox = () => screen.getByPlaceholderText("1–120") as HTMLInputElement;
const attestationBox = () => screen.getByTestId("adverse-attestation").querySelector('input[type="checkbox"]') as HTMLInputElement;

beforeEach(() => {
  declareHosException.mockReset();
  approveOffDutyDeferral.mockReset();
  listTrips.mockReset();
});

afterEach(cleanup);

describe("pure helpers", () => {
  it("the extension bounds are the engine's 2h, from one minute", () => {
    expect(EXTENSION_MIN_MINUTES).toBe(1);
    expect(EXTENSION_MAX_MINUTES).toBe(120);
    expect(minutesInRange("0", 1, 120)).toBeNull();
    expect(minutesInRange("1", 1, 120)).toBe(1);
    expect(minutesInRange("120", 1, 120)).toBe(120);
    expect(minutesInRange("121", 1, 120)).toBeNull();
    expect(minutesInRange("30.5", 1, 120)).toBeNull();
    expect(minutesInRange("", 1, 120)).toBeNull();
  });

  it("validateAdverse requires the reason, then the attestation, then a bounded extension", () => {
    expect(validateAdverse({ attested: true, extraMinutes: "60", reason: "" })).toMatch(/reason/);
    expect(validateAdverse({ attested: false, extraMinutes: "60", reason: "Whiteout" })).toMatch(/attestation/);
    expect(validateAdverse({ attested: true, extraMinutes: "121", reason: "Whiteout" })).toMatch(/1 to 120/);
    expect(validateAdverse({ attested: true, extraMinutes: "60", reason: "Whiteout" })).toBeNull();
  });
});

describe("HosExceptionModal — declare", () => {
  it("offers Adverse driving conditions and Emergency only — never the deferral kind", () => {
    const { container } = renderModal();
    const options = [...(container.querySelector("select") as HTMLSelectElement).options].map((o) => o.value);
    expect(options).toEqual(["Adverse driving conditions", "Emergency"]);
    expect(DECLARABLE_KINDS).not.toContain("Off-duty deferral (CO approved)");
  });

  it("shows the attestation text and refuses an adverse declaration until it is ticked", () => {
    renderModal();
    expect(screen.getByText(ADVERSE_ATTESTATION_TEXT)).toBeTruthy();
    fireEvent.change(reasonBox(), { target: { value: "Whiteout on PTH 6" } });
    fireEvent.change(minutesBox(), { target: { value: "60" } });

    fireEvent.click(screen.getByText("RECORD EXCEPTION"));

    expect(declareHosException).not.toHaveBeenCalled();
    expect(screen.getByText(/Tick the attestation/)).toBeTruthy();
  });

  it("refuses an extension outside 1–120 and accepts the bounds", async () => {
    renderModal();
    fireEvent.change(reasonBox(), { target: { value: "Whiteout on PTH 6" } });
    fireEvent.click(attestationBox());

    for (const bad of ["0", "121"]) {
      fireEvent.change(minutesBox(), { target: { value: bad } });
      fireEvent.click(screen.getByText("RECORD EXCEPTION"));
      expect(declareHosException).not.toHaveBeenCalled();
      expect(screen.getByText(/from 1 to 120/)).toBeTruthy();
    }

    declareHosException.mockResolvedValueOnce({ id: "x" });
    fireEvent.change(minutesBox(), { target: { value: "120" } });
    fireEvent.click(screen.getByText("RECORD EXCEPTION"));
    await waitFor(() => expect(declareHosException).toHaveBeenCalledTimes(1));
  });

  it("POSTs the adverse body: wire kind, attestation true, minutes, reason, ELD remark flag", async () => {
    const { onSaved, onClose } = renderModal();
    fireEvent.change(reasonBox(), { target: { value: "  Whiteout on PTH 6 " } });
    fireEvent.change(minutesBox(), { target: { value: "45" } });
    fireEvent.click(attestationBox());
    fireEvent.change(screen.getByPlaceholderText("Where the driver was"), { target: { value: "Grand Rapids" } });
    const record = { id: "x", status: "Declared" };
    declareHosException.mockResolvedValueOnce(record);

    fireEvent.click(screen.getByText("RECORD EXCEPTION"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(record));
    expect(onClose).toHaveBeenCalled();
    const [driverId, body] = declareHosException.mock.calls[0];
    expect(driverId).toBe("driver-1");
    expect(body).toEqual({
      kind: "Adverse driving conditions",
      effectiveFromUtc: expect.stringMatching(/^\d{4}-\d{2}-\d{2}T.*Z$/),
      reason: "Whiteout on PTH 6",
      effectiveToUtc: null,
      location: "Grand Rapids",
      eldRemarkRecorded: false,
      notKnownBeforeDeparture: true,
      extraDrivingMinutes: 45,
    });
  });

  it("an emergency needs no attestation or extension, and sends neither", async () => {
    const { container } = renderModal();
    fireEvent.change(container.querySelector("select") as HTMLSelectElement, { target: { value: "Emergency" } });
    expect(screen.queryByText(ADVERSE_ATTESTATION_TEXT)).toBeNull();
    fireEvent.change(screen.getByPlaceholderText("What happened"), { target: { value: "Medical emergency on board" } });
    declareHosException.mockResolvedValueOnce({ id: "e" });

    fireEvent.click(screen.getByText("RECORD EXCEPTION"));

    await waitFor(() => expect(declareHosException).toHaveBeenCalledTimes(1));
    const body = declareHosException.mock.calls[0][1];
    expect(body.kind).toBe("Emergency");
    expect(body).not.toHaveProperty("notKnownBeforeDeparture");
    expect(body).not.toHaveProperty("extraDrivingMinutes");
  });

  it("shows the server's refusal verbatim", async () => {
    renderModal();
    fireEvent.change(reasonBox(), { target: { value: "Whiteout" } });
    fireEvent.change(minutesBox(), { target: { value: "30" } });
    fireEvent.click(attestationBox());
    const message = "The exception's start is more than 24 hours ago; declare it inside the shift (s.76).";
    declareHosException.mockRejectedValueOnce(new ApiError("Drivers.Hos.ExceptionBackdated", message, 400));

    fireEvent.click(screen.getByText("RECORD EXCEPTION"));

    await waitFor(() => expect(screen.getByText(message)).toBeTruthy());
  });
});

describe("HosExceptionModal — deferral (compliance)", () => {
  it("lists the driver's upcoming scheduled trips and posts the deferral to the compliance route", async () => {
    listTrips.mockResolvedValueOnce({
      items: [
        { id: "t-2", tripNumber: "NL-0202", serviceDate: "2026-10-12", windowStart: "07:00:00", origin: "Thompson", destination: "Lynn Lake", status: "Scheduled" },
        { id: "t-1", tripNumber: "NL-0201", serviceDate: "2026-10-11", windowStart: "06:30:00", origin: "Thompson", destination: "Leaf Rapids", status: "Scheduled" },
        { id: "t-0", tripNumber: "NL-0200", serviceDate: "2026-10-10", windowStart: "06:30:00", origin: "Thompson", destination: "Leaf Rapids", status: "Cancelled" },
      ],
      page: null,
      pageSize: null,
      totalCount: 3,
    });
    const { container } = renderModal("deferral");
    expect(screen.queryByText(ADVERSE_ATTESTATION_TEXT)).toBeNull();

    await waitFor(() => expect(container.querySelector("select")).toBeTruthy());
    expect(listTrips).toHaveBeenCalledWith({ driverId: "driver-1", from: TODAY, excludeCancelled: true });
    const select = container.querySelector("select") as HTMLSelectElement;
    expect([...select.options].map((o) => o.value)).toEqual(["t-1", "t-2"]);
    expect(select.options[0].text).toContain("NL-0201 · 2026-10-11 06:30");

    fireEvent.change(select, { target: { value: "t-2" } });
    fireEvent.change(minutesBox(), { target: { value: "90" } });
    fireEvent.change(screen.getByPlaceholderText(/deferred to day two/), { target: { value: "Late return" } });
    approveOffDutyDeferral.mockResolvedValueOnce({ id: "d", status: "Approved" });

    fireEvent.click(screen.getByText("APPROVE DEFERRAL"));

    await waitFor(() => expect(approveOffDutyDeferral).toHaveBeenCalledTimes(1));
    expect(declareHosException).not.toHaveBeenCalled();
    const [, body] = approveOffDutyDeferral.mock.calls[0];
    expect(body).toEqual({
      tripId: "t-2",
      effectiveFromUtc: expect.stringMatching(/Z$/),
      deferredOffDutyMinutes: 90,
      reason: "Late return",
      location: null,
      eldRemarkRecorded: false,
    });
  });

  it("falls back to a free trip-id field when no upcoming trips are found", async () => {
    listTrips.mockRejectedValueOnce(new Error("offline"));
    renderModal("deferral");
    await waitFor(() => expect(screen.getByPlaceholderText(/paste the trip id/)).toBeTruthy());
    fireEvent.click(screen.getByText("APPROVE DEFERRAL"));
    expect(approveOffDutyDeferral).not.toHaveBeenCalled();
    expect(screen.getByText(/Choose the trip/)).toBeTruthy();
  });
});
