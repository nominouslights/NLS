import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import Hours from "./Hours";
import type { DutyDaySummary, HosExceptionRecord } from "@/lib/api/hos";
import { ApiError } from "@/lib/api/transport";
import { dutyCommandPayload, type DutyCommandPayload } from "@/lib/hos/dutyCommand";
import { PAPER_LOG_LINE } from "@/lib/hos/display";
import { addDays, type DateOnly, type DutyEvent, type DutyStatus } from "@/lib/hos/engine";
import { CALENDAR, getSnapshot, installLedgerForTests, resetLedgerForTests, syncWithQueue } from "@/lib/hos/ledger";
import { drain, enqueue, failedCommands, pending, registerSender, resetQueueForTests } from "@/lib/sync/queue";

// The Hours screen as a driver meets it: no MockTag any more, the server's refusals listed
// with their message and a working DISCARD, the certification sheet — enabled only when the
// day can actually be certified, with the reason ON SCREEN when it cannot — and the compliance
// slice: exceptions (declared, listed, withdrawn), the ELD malfunction report and its paper-log
// line, other-carrier hours as three duty events, the prior-records line, and the history rows'
// exception and out-of-service chips.

const H = 3600;

function ev(atUnix: number, status: DutyStatus, sequence: number): DutyEvent {
  return { atUnix, status, source: "DriverApp", sequence, odometerDecikm: null, note: null };
}

type Extras = Pick<Parameters<typeof installLedgerForTests>[0], "status" | "exceptions" | "days" | "driverName">;

/** Twenty certified days of nothing, then today's timeline, evaluated now (real clock). */
function install(timeline: [secondsAgo: number, status: DutyStatus][], extras: Extras = {}): DateOnly {
  const now = Math.floor(Date.now() / 1000);
  const knownFrom = now - 20 * 86400;
  const certifiedDays: DateOnly[] = [];
  for (let d = CALENDAR.dayOf(knownFrom); d < CALENDAR.dayOf(now); d = addDays(d, 1)) certifiedDays.push(d);
  // One Off Duty event per past day: a completed day with no event of its own is "records
  // incomplete" to the engine (s.86), which would block Driving ahead of everything under test.
  const events = [ev(knownFrom, "OffDuty", 1)];
  for (let d = addDays(CALENDAR.dayOf(knownFrom), 1); d < CALENDAR.dayOf(now); d = addDays(d, 1)) events.push(ev(CALENDAR.dayStartUnix(d), "OffDuty", events.length + 1));
  for (const [ago, status] of timeline) events.push(ev(now - ago, status, events.length + 1));
  installLedgerForTests({ knownFromUnix: knownFrom, events, certifiedDays, asOfUnix: now, ...extras });
  return CALENDAR.dayOf(now);
}

function exception(fields: Partial<HosExceptionRecord> & { id: string; effectiveFromUtc: string }): HosExceptionRecord {
  return {
    driverId: "driver-test",
    kind: "Adverse driving conditions",
    status: "Declared",
    applying: true,
    tripId: null,
    declaredAtUtc: fields.effectiveFromUtc,
    declaredByUserId: "u",
    effectiveToUtc: null,
    reason: "Whiteout",
    location: null,
    notKnownBeforeDeparture: true,
    extraDrivingMinutes: 60,
    deferredOffDutyMinutes: null,
    eldRemarkRecorded: false,
    dispatchAcknowledgedAtUtc: null,
    dispatchAcknowledgedByUserId: null,
    approvedAtUtc: null,
    approvedByUserId: null,
    closedAtUtc: null,
    closedByUserId: null,
    closureNote: null,
    ...fields,
  };
}

function summary(date: DateOnly, fields: Partial<DutyDaySummary> = {}): DutyDaySummary {
  const start = CALENDAR.dayStartUnix(date);
  return {
    driverId: "driver-test",
    date,
    hasRecords: true,
    dutyDayId: `day-${date}`,
    totals: {
      date,
      known: true,
      complete: true,
      drivingSeconds: 2 * H,
      onDutySeconds: 3 * H,
      otherCarrierOnDutySeconds: 0,
      offDutySeconds: 21 * H,
      rawOffDutySeconds: 21 * H,
      pcDecikm: 0,
      hasOwnEvents: true,
      certified: true,
      eightHourConsecutiveMet: true,
      dailyOffRequiredSeconds: 10 * H,
      offDutyShortfallSeconds: 0,
      dailyOffMet: true,
    },
    personalConveyanceKm: 0,
    certified: true,
    certifiedAtUtc: new Date(start * 1000).toISOString(),
    recertificationRequired: false,
    segments: [],
    events: [],
    violations: [],
    exceptions: [],
    outOfService: null,
    label: "Planning figures",
    ...fields,
  };
}

const iso = (unix: number) => new Date(unix * 1000).toISOString();
const nowUnix = () => Math.floor(Date.now() / 1000);
const textarea = () => document.querySelector("textarea") as HTMLTextAreaElement;
const inputs = (type: string) => [...document.querySelectorAll(`input[type="${type}"]`)] as HTMLInputElement[];

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

describe("exceptions", () => {
  it("adverse conditions: Declare stays off until the s.76 attestation is on and the extension is 1–120 min, then queues hos.exception with the HosWire kind", async () => {
    install([[2 * H, "OnDuty"], [H, "Driving"]]);
    render(<Hours />);
    fireEvent.click(screen.getByRole("button", { name: "Record exception" }));

    const declare = () => screen.getByRole("button", { name: "Declare adverse conditions" }) as HTMLButtonElement;
    expect(declare().disabled).toBe(true);
    expect(screen.getByText("A reason is required.")).toBeTruthy();

    fireEvent.change(textarea(), { target: { value: "Whiteout on 391" } });
    expect(declare().disabled).toBe(true);
    expect(screen.getByText(/could not reasonably have been known before departure \(s\.76\)/)).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: /Conditions were not knowable before departure/ }));
    expect(declare().disabled).toBe(true);
    expect(screen.getByText(/at least 1 minute/)).toBeTruthy();

    fireEvent.change(inputs("number")[0], { target: { value: "121" } });
    expect(declare().disabled).toBe(true);
    expect(screen.getByText(/at most 120 minutes/)).toBeTruthy();

    fireEvent.change(inputs("number")[0], { target: { value: "90" } });
    expect(declare().disabled).toBe(false);
    expect(screen.getByText(/8 hours of consecutive off-duty time are still required/)).toBeTruthy();
    expect(screen.getByText(/Dispatch is notified/)).toBeTruthy();
    fireEvent.click(declare());

    await waitFor(() => expect(pending().some((c) => c.kind === "hos.exception")).toBe(true));
    const held = pending().find((c) => c.kind === "hos.exception")!;
    expect(held.payload).toMatchObject({
      action: "declare",
      kind: "Adverse driving conditions",
      reason: "Whiteout on 391",
      notKnownBeforeDeparture: true,
      extraDrivingMinutes: 90,
      eldRemarkRecorded: false,
      tripId: null,
    });
    expect(Math.abs(Date.parse((held.payload as { effectiveFromUtc: string }).effectiveFromUtc) / 1000 - nowUnix())).toBeLessThan(60);

    // Applied to the figures the moment it is queued, and listed as this tablet's.
    syncWithQueue();
    await waitFor(() => expect(screen.getByText("Queued on this tablet")).toBeTruthy());
    expect(screen.getByText("Adverse conditions")).toBeTruthy();
    expect(getSnapshot().evaluation.shift.adverseExtensionSeconds).toBe(90 * 60);
    expect(screen.getByText(/\+1h 30m adverse conditions/)).toBeTruthy();
  });

  it("emergency: where you stopped and why, queued with kind 'Emergency' and no extension", async () => {
    install([[H, "Driving"]]);
    render(<Hours />);
    fireEvent.click(screen.getByRole("button", { name: "Record exception" }));
    fireEvent.click(screen.getByRole("button", { name: "Emergency" }));

    const declare = () => screen.getByRole("button", { name: "Declare emergency" }) as HTMLButtonElement;
    expect(declare().disabled).toBe(true);
    expect(screen.getByText("Say why you had to stop.")).toBeTruthy();
    fireEvent.change(inputs("text")[0], { target: { value: "Shoulder, Hwy 6 at Grand Rapids" } });
    fireEvent.change(textarea(), { target: { value: "Medical emergency on board" } });
    expect(declare().disabled).toBe(false);
    fireEvent.click(declare());

    await waitFor(() => expect(pending().some((c) => c.kind === "hos.exception")).toBe(true));
    expect(pending().find((c) => c.kind === "hos.exception")?.payload).toMatchObject({
      action: "declare",
      kind: "Emergency",
      reason: "Medical emergency on board",
      location: "Shoulder, Hwy 6 at Grand Rapids",
      notKnownBeforeDeparture: false,
      extraDrivingMinutes: null,
    });
  });

  it("lists today's exceptions with their status, and WITHDRAW queues a withdrawal while one is Declared", async () => {
    const now = nowUnix();
    install([[2 * H, "OnDuty"], [H, "Driving"]], {
      exceptions: [
        exception({ id: "exc-declared", effectiveFromUtc: iso(now - 30 * 60) }),
        exception({ id: "exc-ack", effectiveFromUtc: iso(now - 50 * 60), kind: "Emergency", status: "Acknowledged", extraDrivingMinutes: null, reason: "EMS" }),
        exception({ id: "exc-rejected", effectiveFromUtc: iso(now - 70 * 60), status: "Rejected", applying: false }),
        exception({ id: "exc-yesterday", effectiveFromUtc: iso(now - 30 * H) }),
      ],
    });
    render(<Hours />);

    expect(screen.getByText("Declared")).toBeTruthy();
    expect(screen.getByText("Acknowledged by dispatch")).toBeTruthy();
    expect(screen.getByText("Rejected")).toBeTruthy();
    expect(screen.getByText("EMS")).toBeTruthy();
    // Only the Declared one can be withdrawn by the driver (HosException.Withdraw).
    const withdraw = screen.getAllByRole("button", { name: "Withdraw" });
    expect(withdraw).toHaveLength(1);
    fireEvent.click(withdraw[0]);

    await waitFor(() => expect(pending().some((c) => c.kind === "hos.exception")).toBe(true));
    expect(pending().find((c) => c.kind === "hos.exception")?.payload).toEqual({ action: "withdraw", exceptionId: "exc-declared", note: null });
    syncWithQueue();
    await waitFor(() => expect(screen.getByText("Withdrawal queued")).toBeTruthy());
    expect(screen.queryByRole("button", { name: "Withdraw" })).toBeNull();
    // Withdrawn on this tablet, so no longer applied to the figures.
    expect(getSnapshot().evaluation.shift.adverseExtensionSeconds).toBe(0);
  });
});

describe("ELD malfunction", () => {
  it("queues hos.eld-malfunction from the sheet, and the paper-log line shows at once", async () => {
    const today = install([[H, "OnDuty"]]);
    render(<Hours />);
    expect(screen.queryByText(PAPER_LOG_LINE)).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Report ELD malfunction" }));

    const report = () => screen.getByRole("button", { name: "Report malfunction" }) as HTMLButtonElement;
    expect(report().disabled).toBe(true);
    fireEvent.change(inputs("text")[0], { target: { value: "P" } });
    expect(report().disabled).toBe(true);
    expect(screen.getByText(/description of the malfunction is required/)).toBeTruthy();
    fireEvent.change(textarea(), { target: { value: "Screen frozen since the depot" } });
    expect(report().disabled).toBe(false);
    fireEvent.click(report());

    await waitFor(() => expect(pending().some((c) => c.kind === "hos.eld-malfunction")).toBe(true));
    const held = pending().find((c) => c.kind === "hos.eld-malfunction")!;
    expect(held.payload).toMatchObject({ code: "P", description: "Screen frozen since the depot", vehicleId: null });
    const noticed = Date.parse((held.payload as { noticedAtUtc: string }).noticedAtUtc) / 1000;
    expect(CALENDAR.dayOf(noticed)).toBe(today);
    expect(Math.abs(noticed - nowUnix())).toBeLessThan(120);

    syncWithQueue();
    await waitFor(() => expect(screen.getByText(PAPER_LOG_LINE)).toBeTruthy());
  });

  it("shows the paper-log line while the server says a malfunction is open", () => {
    install([[H, "OnDuty"]], { status: { eldMalfunctionOpen: true } });
    render(<Hours />);
    expect(screen.getByText(PAPER_LOG_LINE)).toBeTruthy();
  });
});

describe("other-carrier hours", () => {
  it("lays the day out as three hos.duty events — On Duty, Driving, Off Duty — each with its own id, the other-carrier source, the carrier and the driver's name", async () => {
    const today = install([[H, "OnDuty"]], { driverName: "R. Beardy" });
    render(<Hours />);
    fireEvent.click(screen.getByRole("button", { name: "Other carrier hours" }));

    const yesterday = addDays(today, -1);
    expect(inputs("date")[0].value).toBe(yesterday);
    fireEvent.change(inputs("text")[0], { target: { value: "Gardewine North" } });
    fireEvent.change(inputs("number")[0], { target: { value: "480" } });
    fireEvent.change(inputs("number")[1], { target: { value: "300" } });
    expect(screen.getByText(/00:00 On Duty → 03:00 Driving → 08:00 Off Duty/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Declare 3 duty events" }));

    await waitFor(() => expect(pending().filter((c) => c.kind === "hos.duty")).toHaveLength(3));
    const held = pending().filter((c) => c.kind === "hos.duty");
    const start = CALENDAR.dayStartUnix(yesterday);
    expect(held.map((c) => (c.payload as DutyCommandPayload).duty)).toEqual(["On Duty", "Driving", "Off Duty"]);
    expect(held.map((c) => Date.parse((c.payload as DutyCommandPayload).atUtc) / 1000 - start)).toEqual([0, 180 * 60, 480 * 60]);
    expect(new Set(held.map((c) => c.id)).size).toBe(3);
    for (const c of held) {
      expect(c.payload).toMatchObject({ source: "Other carrier (declared)", carrierName: "Gardewine North", enteredBy: "R. Beardy", clientEventId: c.id });
    }
    // Capture order is send order: the day's events reach the server On Duty first.
    expect(held.map((c) => c.seq)).toEqual([...held.map((c) => c.seq)].sort((a, b) => a - b));
    // And the day's totals move now, from events the engine reads as that carrier's.
    await waitFor(() => expect(getSnapshot().evaluation.days.find((d) => d.date === yesterday)?.onDutySeconds).toBe(8 * H));
    expect(getSnapshot().events.filter((e) => e.source === "OtherCarrier")).toHaveLength(3);
  });

  it("is unavailable, with the reason on screen, until the driver's roster name has been fetched", () => {
    install([[H, "OnDuty"]], { driverName: null });
    render(<Hours />);
    expect((screen.getByRole("button", { name: "Other carrier hours" }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText(/Other carrier hours unavailable/)).toBeTruthy();
  });
});

describe("prior records and the history rows", () => {
  it("says the prior 14 days are not on file — not dispatchable — while the server has neither records nor an attestation", () => {
    install([[H, "OnDuty"]], { status: { recordsOnFile: false, priorRecords: null } });
    render(<Hours />);
    expect(screen.getByText(/Prior 14 days not on file — not dispatchable\./)).toBeTruthy();
    cleanup();

    install([[H, "OnDuty"]], { status: { recordsOnFile: true, priorRecords: null } });
    render(<Hours />);
    expect(screen.queryByText(/Prior 14 days not on file/)).toBeNull();
    cleanup();

    install([[H, "OnDuty"]], {
      status: { recordsOnFile: true, priorRecords: { coveredThroughDate: "2026-10-01", attestedAtUtc: iso(nowUnix()), attestedByUserId: "co", note: null, worksForOtherCarriers: false } },
    });
    render(<Hours />);
    expect(screen.queryByText(/Prior 14 days not on file/)).toBeNull();
  });

  it("shows a day's exceptions and its out-of-service declaration on the history row", () => {
    const today = install([[H, "OnDuty"]]);
    const day = addDays(today, -2);
    install([[H, "OnDuty"]], {
      days: [
        summary(day, {
          exceptions: [exception({ id: "exc-1", effectiveFromUtc: iso(CALENDAR.dayStartUnix(day) + 8 * H), kind: "Emergency", status: "Acknowledged" })],
          outOfService: { id: "oos-1", fromUtc: iso(CALENDAR.dayStartUnix(day) + 10 * H), untilUtc: iso(CALENDAR.dayStartUnix(day) + 20 * H), reason: "r" },
        }),
      ],
    });
    render(<Hours />);
    expect(screen.getByText("Emergency · Acknowledged")).toBeTruthy();
    expect(screen.getByText("Out of service")).toBeTruthy();
  });

  it("read-only hides every action and the certification, and keeps the figures", () => {
    install([[H, "OnDuty"]]);
    render(<Hours readOnly />);
    expect(screen.queryByRole("button", { name: "Record exception" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Report ELD malfunction" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Other carrier hours" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Certify today" })).toBeNull();
    expect(screen.getByText(/Planning figures — your record of duty status/)).toBeTruthy();
    expect(screen.getByRole("group", { name: "Duty status" })).toBeTruthy();
  });
});
