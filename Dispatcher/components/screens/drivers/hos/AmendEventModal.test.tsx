import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError } from "@/lib/api/transport";
import AmendEventModal from "./AmendEventModal";
import { dutyDay, dutyEvent, FIXTURE_DATE, FIXTURE_DAY_START } from "./hosFixtures";

// The amendment modal: the reason is mandatory, the replacement is POSTed with the
// corrected time on the same local day, and a refusal shows the server's words.

const { amendDutyEvent } = vi.hoisted(() => ({ amendDutyEvent: vi.fn() }));
vi.mock("@/lib/api/hos", () => ({ amendDutyEvent }));

beforeEach(() => {
  amendDutyEvent.mockReset();
});

afterEach(cleanup);

function renderModal() {
  const event = dutyEvent({ eventId: "orig", status: "Driving", source: "Manual (paper backup)", enteredBy: "L. Moose" });
  const day = dutyDay({ events: [event] });
  const onSaved = vi.fn();
  const onClose = vi.fn();
  const utils = render(<AmendEventModal driverId="driver-1" driverName="K. Beardy" day={day} event={event} onClose={onClose} onSaved={onSaved} />);
  return { ...utils, event, day, onSaved, onClose };
}

describe("AmendEventModal", () => {
  it("requires a reason before it will POST", () => {
    renderModal();
    fireEvent.click(screen.getByText("AMEND EVENT"));
    expect(screen.getByText("Enter the reason for this amendment.")).toBeTruthy();
    expect(amendDutyEvent).not.toHaveBeenCalled();
  });

  it("POSTs the replacement with the corrected local time, the reason and a client replacementEventId", async () => {
    const { onSaved, onClose } = renderModal();
    fireEvent.change(screen.getByPlaceholderText("Paper log reads 08:15, not 08:00"), { target: { value: " Paper log reads 08:15. " } });
    const time = document.querySelector('input[type="time"]') as HTMLInputElement;
    expect(time.value).toBe("07:00"); // the original, prefilled in local time
    fireEvent.change(time, { target: { value: "08:15" } });
    amendDutyEvent.mockResolvedValueOnce({ originalEventId: "orig", replacementEventId: "r", dutyDayId: "d", localDate: FIXTURE_DATE, recertificationRequired: true, created: true });

    fireEvent.click(screen.getByText("AMEND EVENT"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(FIXTURE_DATE, true));
    expect(onClose).toHaveBeenCalled();
    const [driverId, eventId, body] = amendDutyEvent.mock.calls[0];
    expect(driverId).toBe("driver-1");
    expect(eventId).toBe("orig");
    expect(body).toEqual({
      replacementEventId: expect.stringMatching(/^[0-9a-f-]{36}$/),
      atUtc: new Date((FIXTURE_DAY_START + 8 * 3600 + 15 * 60) * 1000).toISOString(),
      status: "Driving",
      reason: "Paper log reads 08:15.",
      source: "Manual (paper backup)",
      enteredBy: "L. Moose",
      note: null,
    });
  });

  it("shows the server's refusal verbatim", async () => {
    renderModal();
    fireEvent.change(screen.getByPlaceholderText("Paper log reads 08:15, not 08:00"), { target: { value: "r" } });
    const message = "The replacement must fall on the same local day as the original (2026-10-09).";
    amendDutyEvent.mockRejectedValueOnce(new ApiError("Drivers.Hos.AmendCrossesDay", message, 409));

    fireEvent.click(screen.getByText("AMEND EVENT"));

    await waitFor(() => expect(screen.getByText(message)).toBeTruthy());
  });
});
