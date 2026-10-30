import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { DutyControl, pcReason } from "./DutyControl";
import { PC_ATTESTATIONS } from "./PersonalConveyanceModal";
import { ApiError } from "@/lib/api/transport";
import { dutyCommandPayload, type DutyCommandPayload } from "@/lib/hos/dutyCommand";
import { addDays, type DateOnly, type DutyEvent, type DutyStatus } from "@/lib/hos/engine";
import { CALENDAR, getSnapshot, installLedgerForTests, resetLedgerForTests } from "@/lib/hos/ledger";
import { drain, enqueue, pending, registerSender, resetQueueForTests } from "@/lib/sync/queue";

// The duty control with personal conveyance live: the PC tap opens the sheet, Confirm queues a
// hos.duty event carrying the PC fields and moves the ledger; leaving PC prompts for the end
// odometer first and the closing event carries it; and the two reasons PC can be unavailable
// (out of service, the allowance) read on screen, as does Driving's while out of service.

const H = 3600;

function ev(atUnix: number, status: DutyStatus, sequence: number, odometerDecikm: number | null = null): DutyEvent {
  return { atUnix, status, source: "DriverApp", sequence, odometerDecikm, note: null };
}

function install(timeline: [secondsAgo: number, status: DutyStatus, odometerDecikm?: number][], status: Parameters<typeof installLedgerForTests>[0]["status"] = {}): number {
  const now = Math.floor(Date.now() / 1000);
  const knownFrom = now - 20 * 86400;
  const certifiedDays: DateOnly[] = [];
  for (let d = CALENDAR.dayOf(knownFrom); d < CALENDAR.dayOf(now); d = addDays(d, 1)) certifiedDays.push(d);
  // One Off Duty event per past day: a completed day with no event of its own is "records
  // incomplete" to the engine (s.86), which would be reported ahead of everything under test.
  const events = [ev(knownFrom, "OffDuty", 1)];
  for (let d = addDays(CALENDAR.dayOf(knownFrom), 1); d < CALENDAR.dayOf(now); d = addDays(d, 1)) events.push(ev(CALENDAR.dayStartUnix(d), "OffDuty", events.length + 1));
  for (const [ago, s, odo] of timeline) events.push(ev(now - ago, s, events.length + 1, odo ?? null));
  installLedgerForTests({ knownFromUnix: knownFrom, events, certifiedDays, asOfUnix: now, status });
  return now;
}

function numberInput(): HTMLInputElement {
  return document.querySelector('input[type="number"]') as HTMLInputElement;
}

beforeEach(async () => {
  resetLedgerForTests();
  await resetQueueForTests({ wipe: true });
});

afterEach(() => {
  cleanup();
});

describe("personal conveyance", () => {
  it("opens the sheet on the PC tap and, once confirmed, queues hos.duty with the PC fields and moves the ledger", async () => {
    install([[H, "OnDuty"]]);
    render(<DutyControl />);

    const pc = screen.getByRole("button", { name: /Personal Conveyance/ }) as HTMLButtonElement;
    expect(pc.disabled).toBe(false);
    fireEvent.click(pc);
    expect(screen.getByText("Start personal conveyance")).toBeTruthy();
    // Nothing queued by the tap itself — the sheet gates it.
    expect(pending()).toHaveLength(0);

    fireEvent.change(numberInput(), { target: { value: "184920" } });
    for (const label of PC_ATTESTATIONS) fireEvent.click(screen.getByRole("button", { name: label }));
    fireEvent.click(screen.getByRole("button", { name: "Confirm personal conveyance" }));

    await waitFor(() => expect(pending()).toHaveLength(1));
    const held = pending()[0];
    expect(held.kind).toBe("hos.duty");
    expect(held.payload).toMatchObject({
      duty: "Personal Conveyance",
      source: "Driver App",
      clientEventId: held.id,
      odometerStartKm: 184920,
      pcNoPassengersOrCargo: true,
      pcNoTrailer: true,
      pcNoWork: true,
    });
    await waitFor(() => expect(getSnapshot().evaluation.currentStatus).toBe("PersonalConveyance"));
    expect(getSnapshot().events.at(-1)).toMatchObject({ status: "PersonalConveyance", odometerDecikm: 1849200, clientEventId: held.id });
    expect(screen.getByText(/kilometres are what you enter, not a live feed/)).toBeTruthy();
  });

  it("prompts for the END odometer on the next duty tap, refuses a lower reading, and the closing event carries it", async () => {
    install([
      [2 * H, "OnDuty"],
      [H, "PersonalConveyance", 1849200],
    ]);
    render(<DutyControl />);

    fireEvent.click(screen.getByRole("button", { name: /Off Duty/ }));
    expect(screen.getByText("Odometer at the end of personal conveyance")).toBeTruthy();
    expect(pending()).toHaveLength(0);

    fireEvent.change(numberInput(), { target: { value: "184900" } });
    const confirm = () => screen.getByRole("button", { name: "Record end of personal conveyance" }) as HTMLButtonElement;
    expect(confirm().disabled).toBe(true);
    expect(screen.getByText(/lower than the 184920\.0 km that started the segment/)).toBeTruthy();

    fireEvent.change(numberInput(), { target: { value: "184950" } });
    fireEvent.click(confirm());

    await waitFor(() => expect(pending()).toHaveLength(1));
    const held = pending()[0];
    expect(held.payload).toMatchObject({ duty: "Off Duty", source: "Driver App", odometerEndKm: 184950 });
    expect((held.payload as DutyCommandPayload).odometerStartKm).toBeUndefined();
    await waitFor(() => expect(getSnapshot().evaluation.currentStatus).toBe("OffDuty"));
    expect(getSnapshot().events.at(-1)).toMatchObject({ status: "OffDuty", odometerDecikm: 1849500 });
    // 30.0 km of PC today, counted from the readings.
    expect(getSnapshot().evaluation.today.pcDecikm).toBe(300);
  });

  it("greys PC with the reason once today's 75.0 km allowance is used up", () => {
    install([
      [3 * H, "PersonalConveyance", 1000000],
      [2 * H, "OffDuty", 1000750],
    ]);
    render(<DutyControl />);
    const pc = screen.getByRole("button", { name: /Personal Conveyance/ }) as HTMLButtonElement;
    expect(pc.disabled).toBe(true);
    expect(screen.getByText(/Personal Conveyance unavailable/)).toBeTruthy();
    expect(screen.getByText(/75\.0 km personal-conveyance allowance for today is used up/)).toBeTruthy();
    expect(screen.getByText(/PC today: 75\.0 km/)).toBeTruthy();
  });

  it("greys PC with the server's own refusal after a PcAllowanceExhausted answer today", async () => {
    install([[H, "OnDuty"]]);
    const message = "The 75.0 km personal-conveyance allowance for this day is used up; further driving is work.";
    registerSender("hos.duty", async () => {
      throw new ApiError("Drivers.Hos.PcAllowanceExhausted", message, 400);
    });
    await enqueue("hos.duty", (id) => dutyCommandPayload("PersonalConveyance", new Date().toISOString(), id, { odometerStartKm: 1, pcNoPassengersOrCargo: true, pcNoTrailer: true, pcNoWork: true }));
    await drain();

    render(<DutyControl />);
    const pc = screen.getByRole("button", { name: /Personal Conveyance/ }) as HTMLButtonElement;
    expect(pc.disabled).toBe(true);
    expect(screen.getByText(new RegExp(`The server refused personal conveyance today — ${message.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}`))).toBeTruthy();
    expect(pcReason(false, false, 0, Math.floor(Date.now() / 1000))).toMatch(/server refused/);
  });
});

describe("out of service", () => {
  it("greys Driving and Personal Conveyance with the reason on screen, and leaves Off Duty / On Duty", () => {
    const now = install([[H, "OnDuty"]], {
      outOfService: { id: "oos-1", fromUtc: new Date((Math.floor(Date.now() / 1000) - 2 * H) * 1000).toISOString(), untilUtc: null, reason: "Logbook not produced at roadside check" },
    });
    expect(getSnapshot().outOfService?.id).toBe("oos-1");
    expect(getSnapshot().evaluation.blockingReasons).toContain("OutOfService");
    render(<DutyControl />);

    expect((screen.getByRole("button", { name: /Driving/ }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: /Personal Conveyance/ }) as HTMLButtonElement).disabled).toBe(true);
    expect((screen.getByRole("button", { name: /Off Duty/ }) as HTMLButtonElement).disabled).toBe(false);
    expect(screen.getByText(/Driving unavailable/)).toBeTruthy();
    expect(screen.getByText(/— Out of service$/)).toBeTruthy();
    expect(screen.getByText(/Personal Conveyance unavailable/)).toBeTruthy();
    expect(screen.getByText(/personal conveyance is not permitted until the declaration is lifted/)).toBeTruthy();
    expect(pcReason(true, false, 0, now)).toMatch(/Out of service/);
  });
});
