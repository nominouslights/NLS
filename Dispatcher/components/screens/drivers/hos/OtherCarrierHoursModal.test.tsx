import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError } from "@/lib/api/transport";
import OtherCarrierHoursModal, { OTHER_CARRIER_MAX_MINUTES, OTHER_CARRIER_SOURCE, otherCarrierEvents, validateOtherCarrier } from "./OtherCarrierHoursModal";
import { FIXTURE_DATE, FIXTURE_DAY_START } from "./hosFixtures";

// Other-carrier hours become three duty events from the day's start, in order, each with
// its own id, source "Other carrier (declared)" and the carrier's name — posted one at a time.

const { recordDutyEvent } = vi.hoisted(() => ({ recordDutyEvent: vi.fn() }));
vi.mock("@/lib/api/hos", () => ({ recordDutyEvent }));

const draft = { date: FIXTURE_DATE, carrierName: "Thompson Bus Lines", onDutyMinutes: "480", drivingMinutes: "300" };

beforeEach(() => recordDutyEvent.mockReset());
afterEach(cleanup);

describe("pure helpers", () => {
  it("the source is the wire string for OtherCarrier", () => {
    expect(OTHER_CARRIER_SOURCE).toBe("Other carrier (declared)");
    expect(OTHER_CARRIER_MAX_MINUTES).toBe(1439);
  });

  it("validateOtherCarrier requires date, carrier, on-duty ≥ 1, driving ≤ on-duty, and keeps the day inside 24h", () => {
    expect(validateOtherCarrier({ ...draft, date: "" })).toMatch(/date/);
    expect(validateOtherCarrier({ ...draft, carrierName: " " })).toMatch(/carrier/);
    expect(validateOtherCarrier({ ...draft, onDutyMinutes: "0" })).toMatch(/at least 1/);
    expect(validateOtherCarrier({ ...draft, onDutyMinutes: "1440" })).toMatch(/1439/);
    expect(validateOtherCarrier({ ...draft, drivingMinutes: "" })).toMatch(/driving/i);
    expect(validateOtherCarrier({ ...draft, drivingMinutes: "481" })).toMatch(/cannot exceed on-duty/);
    expect(validateOtherCarrier(draft)).toBeNull();
    expect(validateOtherCarrier({ ...draft, drivingMinutes: "0" })).toBeNull();
  });

  it("lays out On Duty → Driving → Off Duty from the day's start with three distinct ids", () => {
    let n = 0;
    const events = otherCarrierEvents(draft, " Paper log ", () => `id-${++n}`);
    const iso = (hours: number) => new Date((FIXTURE_DAY_START + hours * 3600) * 1000).toISOString();

    expect(events.map((e) => e.status)).toEqual(["On Duty", "Driving", "Off Duty"]);
    expect(events.map((e) => e.eventId)).toEqual(["id-1", "id-2", "id-3"]);
    expect(events.map((e) => e.atUtc)).toEqual([iso(0), iso(3), iso(8)]); // 480 − 300 = 180 min of non-driving on-duty first
    for (const e of events) {
      expect(e.source).toBe("Other carrier (declared)");
      expect(e.carrierName).toBe("Thompson Bus Lines");
      expect(e.note).toBe("Paper log");
    }
  });

  it("drops the On Duty event when all on-duty time was driving, and the Driving one when none was", () => {
    expect(otherCarrierEvents({ ...draft, drivingMinutes: "480" }, "", () => "x").map((e) => e.status)).toEqual(["Driving", "Off Duty"]);
    expect(otherCarrierEvents({ ...draft, drivingMinutes: "0" }, "", () => "x").map((e) => e.status)).toEqual(["On Duty", "Off Duty"]);
  });
});

describe("OtherCarrierHoursModal", () => {
  function fill() {
    fireEvent.change(screen.getByPlaceholderText("The other carrier's name"), { target: { value: "Thompson Bus Lines" } });
    fireEvent.change(screen.getByPlaceholderText("Including driving"), { target: { value: "480" } });
    fireEvent.change(screen.getByPlaceholderText("0 when none"), { target: { value: "300" } });
  }

  it("posts the three events in order, one call each, with distinct ids and the carrier name", async () => {
    const onSaved = vi.fn();
    const onClose = vi.fn();
    render(<OtherCarrierHoursModal driverId="driver-1" driverName="K. Beardy" defaultDate={FIXTURE_DATE} onClose={onClose} onSaved={onSaved} />);
    fill();
    recordDutyEvent.mockResolvedValue({ created: true });

    fireEvent.click(screen.getByText("RECORD HOURS"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(FIXTURE_DATE));
    expect(onClose).toHaveBeenCalled();
    expect(recordDutyEvent).toHaveBeenCalledTimes(3);
    const bodies = recordDutyEvent.mock.calls.map((c) => c[1]);
    expect(recordDutyEvent.mock.calls.every((c) => c[0] === "driver-1")).toBe(true);
    expect(bodies.map((b) => b.status)).toEqual(["On Duty", "Driving", "Off Duty"]);
    expect(new Set(bodies.map((b) => b.eventId)).size).toBe(3);
    expect(bodies.every((b) => /^[0-9a-f-]{36}$/.test(b.eventId))).toBe(true);
    expect(bodies.every((b) => b.source === "Other carrier (declared)" && b.carrierName === "Thompson Bus Lines")).toBe(true);
    expect(bodies.map((b) => b.atUtc)).toEqual([...bodies.map((b) => b.atUtc)].sort());
  });

  it("refuses driving over on-duty before any POST, and shows a server refusal verbatim", async () => {
    render(<OtherCarrierHoursModal driverId="driver-1" driverName="K. Beardy" defaultDate={FIXTURE_DATE} onClose={vi.fn()} onSaved={vi.fn()} />);
    fill();
    fireEvent.change(screen.getByPlaceholderText("0 when none"), { target: { value: "500" } });
    fireEvent.click(screen.getByText("RECORD HOURS"));
    expect(recordDutyEvent).not.toHaveBeenCalled();

    fireEvent.change(screen.getByPlaceholderText("0 when none"), { target: { value: "300" } });
    const message = "A carrier name is required for an other-carrier event.";
    recordDutyEvent.mockRejectedValueOnce(new ApiError("Drivers.Hos.CarrierNameRequired", message, 400));
    fireEvent.click(screen.getByText("RECORD HOURS"));
    await waitFor(() => expect(screen.getByText(message)).toBeTruthy());
    expect(recordDutyEvent).toHaveBeenCalledTimes(1);
  });
});
