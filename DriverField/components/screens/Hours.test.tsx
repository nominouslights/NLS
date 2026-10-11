import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import Hours from "./Hours";
import { ApiError } from "@/lib/api/transport";
import { dutyCommandPayload } from "@/lib/hos/dutyCommand";
import { addDays, type DateOnly, type DutyEvent, type DutyStatus } from "@/lib/hos/engine";
import { CALENDAR, installLedgerForTests, resetLedgerForTests } from "@/lib/hos/ledger";
import { drain, enqueue, failedCommands, pending, registerSender, resetQueueForTests } from "@/lib/sync/queue";

// The Hours screen as a driver meets it: no MockTag any more, the server's refusals listed
// with their message and a working DISCARD, and the certification sheet — enabled only when
// the day can actually be certified, with the reason ON SCREEN when it cannot.

const H = 3600;

function ev(atUnix: number, status: DutyStatus, sequence: number): DutyEvent {
  return { atUnix, status, source: "DriverApp", sequence, odometerDecikm: null, note: null };
}

/** Twenty certified days of nothing, then today's timeline, evaluated now (real clock). */
function install(timeline: [secondsAgo: number, status: DutyStatus][]): DateOnly {
  const now = Math.floor(Date.now() / 1000);
  const knownFrom = now - 20 * 86400;
  const certifiedDays: DateOnly[] = [];
  for (let d = CALENDAR.dayOf(knownFrom); d < CALENDAR.dayOf(now); d = addDays(d, 1)) certifiedDays.push(d);
  const events = [ev(knownFrom, "OffDuty", 1), ...timeline.map(([ago, status], i) => ev(now - ago, status, i + 2))];
  installLedgerForTests({ knownFromUnix: knownFrom, events, certifiedDays, asOfUnix: now });
  return CALENDAR.dayOf(now);
}

beforeEach(async () => {
  resetLedgerForTests();
  await resetQueueForTests({ wipe: true });
});

afterEach(() => {
  cleanup();
});

describe("Hours", () => {
  it("carries no MockTag — the figures are the driver's", () => {
    install([[H, "Driving"]]);
    render(<Hours />);
    expect(screen.queryByText("MOCK")).toBeNull();
    expect(screen.getByText(/Planning figures — your record of duty status is on the bus's ELD/)).toBeTruthy();
  });

  it("lists a change the server refused with the server's message, and DISCARD removes it", async () => {
    install([[H, "Driving"]]);
    const message = "The driver is already in that status.";
    registerSender("hos.duty", async () => {
      throw new ApiError("Drivers.Hos.NoStatusChange", message, 400);
    });
    await enqueue("hos.duty", (id) => dutyCommandPayload("Driving", new Date().toISOString(), id));
    await drain();
    expect(failedCommands()).toHaveLength(1);

    render(<Hours />);

    expect(screen.getByText(message)).toBeTruthy();
    expect(screen.getByText("Drivers.Hos.NoStatusChange")).toBeTruthy();
    expect(screen.getByText(/1 queued change the server refused/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Discard" }));

    await waitFor(() => expect(screen.queryByText(message)).toBeNull());
    expect(failedCommands()).toHaveLength(0);
  });

  it("Certify today opens the attestation sheet, and Certify queues hos.certify for that date", async () => {
    const today = install([
      [2 * H, "Driving"],
      [10 * 60, "OffDuty"],
    ]);
    render(<Hours />);

    const button = screen.getByRole("button", { name: "Certify today" }) as HTMLButtonElement;
    expect(button.disabled).toBe(false);
    fireEvent.click(button);

    expect(screen.getByText(`I certify these duty figures are true and complete for ${today}.`)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Certify" }));

    await waitFor(() => expect(pending().some((c) => c.kind === "hos.certify")).toBe(true));
    expect(pending().find((c) => c.kind === "hos.certify")?.payload).toEqual({ date: today });
    await waitFor(() => expect(screen.getByText(/Certification queued/)).toBeTruthy());
  });

  it("disables Certify today with the reason on screen while a personal-conveyance segment is open", () => {
    install([
      [2 * H, "Driving"],
      [10 * 60, "PersonalConveyance"],
    ]);
    render(<Hours />);

    const button = screen.getByRole("button", { name: "Certify today" }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(screen.getByText(/Personal conveyance is still open/)).toBeTruthy();
  });

  it("disables Certify today with the reason on screen when nothing has been recorded today", () => {
    install([]);
    render(<Hours />);

    const button = screen.getByRole("button", { name: "Certify today" }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(screen.getByText(/No duty events recorded for this day yet/)).toBeTruthy();
    expect(screen.getByText(/No duty events today yet/)).toBeTruthy();
  });
});
