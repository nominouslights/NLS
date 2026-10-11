import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { DutyDaySummary, DutyEventRecord, HosStatus } from "../api/hos";
import { ApiError } from "../api/transport";
import { drain, enqueue, resetQueueForTests } from "../sync/queue";
import { dutyCommandPayload } from "./dutyCommand";
import { addDays, HOS_LIMITS, HosWire, type DateOnly, type DayTotals, type HosEvaluation } from "./engine";
import { band, cannotDriveBecause, dutyStateOf } from "./display";
import {
  appendDutyEvent,
  CALENDAR,
  evaluation,
  getServerSnapshot,
  getSnapshot,
  hydrate,
  installLedgerForTests,
  nowUnix,
  resetLedgerForTests,
  RETAINED_DAYS,
  startLedger,
  subscribe,
  tick,
} from "./ledger";

// The live ledger store: hydration from the API, the optimistic append that moves the figures,
// reconciliation by clientEventId, the cache that survives a dead-zone reload, the clock,
// retention, and the subscribe contract the screens lean on. The ENGINE is not under test here
// (engine.fixtures.test.ts holds it to the server's snapshots); what is under test is that the
// store feeds it the right ledger at the right instant and publishes the result.

const { getMyDriver, listDutyDays, getHosStatus, recordDutyEvent, certifyDay } = vi.hoisted(() => ({
  getMyDriver: vi.fn(),
  listDutyDays: vi.fn(),
  getHosStatus: vi.fn(),
  recordDutyEvent: vi.fn(),
  certifyDay: vi.fn(),
}));

vi.mock("../api/hos", () => ({ getMyDriver, listDutyDays, getHosStatus, recordDutyEvent, certifyDay }));

const H = 3600;
const DRIVER = "11111111-1111-1111-1111-111111111111";
/** 15:00 CDT on the 10th. */
const NOW_ISO = "2026-10-10T20:00:00.000Z";
const NOW = Date.parse(NOW_ISO) / 1000;
const TODAY = "2026-10-10";

function totals(date: DateOnly, fields: Partial<DayTotals> = {}): DayTotals {
  return {
    date,
    known: true,
    complete: true,
    drivingSeconds: 0,
    onDutySeconds: 0,
    otherCarrierOnDutySeconds: 0,
    offDutySeconds: 24 * H,
    rawOffDutySeconds: 24 * H,
    pcDecikm: 0,
    hasOwnEvents: true,
    certified: false,
    eightHourConsecutiveMet: true,
    dailyOffRequiredSeconds: 10 * H,
    offDutyShortfallSeconds: 0,
    dailyOffMet: true,
    ...fields,
  };
}

function event(fields: Partial<DutyEventRecord> & { eventId: string; atUtc: string; status: DutyEventRecord["status"] }): DutyEventRecord {
  return {
    sequence: 1,
    recordedAtUtc: fields.atUtc,
    source: "Driver App",
    recordedByUserId: null,
    enteredBy: null,
    carrierName: null,
    note: null,
    odometerStartKm: null,
    odometerEndKm: null,
    pcNoPassengersOrCargo: false,
    pcNoTrailer: false,
    pcNoWork: false,
    amendsEventId: null,
    amendmentReason: null,
    isVoided: false,
    ...fields,
  };
}

function day(date: DateOnly, events: DutyEventRecord[], fields: Partial<DutyDaySummary> = {}): DutyDaySummary {
  return {
    driverId: DRIVER,
    date,
    hasRecords: events.length > 0,
    dutyDayId: events.length > 0 ? `day-${date}` : null,
    totals: totals(date),
    personalConveyanceKm: 0,
    certified: false,
    certifiedAtUtc: null,
    recertificationRequired: false,
    segments: [],
    events,
    violations: [],
    label: "Planning figures",
    ...fields,
  };
}

/** Fourteen days ending today: thirteen quiet certified days, then today's events. */
function fourteenDays(todayEvents: DutyEventRecord[]): DutyDaySummary[] {
  const days: DutyDaySummary[] = [];
  for (let i = 13; i >= 1; i -= 1) {
    const date = addDays(TODAY, -i);
    const start = CALENDAR.dayStartUnix(date);
    days.push(day(date, [event({ eventId: `off-${date}`, atUtc: new Date(start * 1000).toISOString(), status: "Off Duty" })], { certified: true, certifiedAtUtc: NOW_ISO }));
  }
  days.push(day(TODAY, todayEvents));
  return days;
}

function status(fields: Partial<HosStatus> = {}): HosStatus {
  return {
    driverId: DRIVER,
    label: "Planning figures",
    currentDuty: "Off Duty",
    currentDutySinceUtc: NOW_ISO,
    recordsOnFile: true,
    // The server's known-from is the driver's earliest day on file — well before the window.
    knownFromUtc: new Date(CALENDAR.dayStartUnix(addDays(TODAY, -40)) * 1000).toISOString(),
    outOfService: null,
    eldMalfunctionOpen: false,
    evaluation: {} as HosEvaluation,
    ...fields,
  };
}

/** Today: on duty 06:00, driving 06:30, on duty 09:15, off 09:45 (CDT). */
const TODAY_EVENTS = [
  event({ eventId: "e1", sequence: 1, atUtc: "2026-10-10T11:00:00Z", status: "On Duty" }),
  event({ eventId: "e2", sequence: 2, atUtc: "2026-10-10T11:30:00Z", status: "Driving" }),
  event({ eventId: "e3", sequence: 3, atUtc: "2026-10-10T14:15:00Z", status: "On Duty", source: "ELD transcription", enteredBy: "Dispatch" }),
  event({ eventId: "e4", sequence: 4, atUtc: "2026-10-10T14:45:00Z", status: "Off Duty" }),
  event({ eventId: "void", sequence: 5, atUtc: "2026-10-10T14:50:00Z", status: "Driving", isVoided: true, amendsEventId: null }),
];

beforeEach(async () => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date(NOW_ISO));
  resetLedgerForTests();
  await resetQueueForTests({ wipe: true });
  getMyDriver.mockResolvedValue({ id: DRIVER, name: "R. Beardy" });
  listDutyDays.mockResolvedValue(fourteenDays(TODAY_EVENTS));
  getHosStatus.mockResolvedValue(status());
  recordDutyEvent.mockResolvedValue({ eventId: "x", dutyDayId: "d", localDate: TODAY, created: true });
  certifyDay.mockResolvedValue(undefined);
});

afterEach(() => {
  vi.useRealTimers();
  vi.clearAllMocks();
});

describe("the clock", () => {
  it("is the device clock, in Unix seconds, and tick() moves as-of forward — never back", () => {
    expect(nowUnix()).toBe(NOW);
    vi.setSystemTime(new Date((NOW + 90) * 1000));
    expect(nowUnix()).toBe(NOW + 90);

    installLedgerForTests({ knownFromUnix: NOW - 20 * 86400, events: [], certifiedDays: [], asOfUnix: NOW + 100 });
    tick();
    expect(evaluation().asOfUnix).toBe(NOW + 100);
    vi.setSystemTime(new Date((NOW + 600) * 1000));
    tick();
    expect(evaluation().asOfUnix).toBe(NOW + 600);
  });

  it("starts empty: no ledger, blocked, never a free pass", () => {
    const e = evaluation();
    expect(e.canDriveNow).toBe(false);
    expect(e.blockingReasons[0]).toBe("NoLedger");
    expect(getSnapshot().hydration).toEqual({ source: "none", atUnix: null, error: null });
    expect(getServerSnapshot().events).toHaveLength(0);
  });
});

describe("hydrate — the server copy", () => {
  it("resolves the driver from /me, fetches days + status, and maps events through HosWire with voided ones left out", async () => {
    expect(await hydrate()).toBe(true);

    expect(getMyDriver).toHaveBeenCalledTimes(1);
    // The server's own lookback (DutyLedgerAssembler.LookbackDays = 21), ending today by default.
    expect(listDutyDays).toHaveBeenCalledWith(DRIVER, { from: addDays(TODAY, -RETAINED_DAYS) });
    expect(RETAINED_DAYS).toBe(21);
    expect(getHosStatus).toHaveBeenCalledWith(DRIVER);

    const s = getSnapshot();
    expect(s.hydration).toEqual({ source: "server", atUnix: NOW, error: null });
    expect(s.ledger.driverId).toBe(DRIVER);
    expect(s.days).toHaveLength(14);
    const today = s.events.filter((e) => CALENDAR.dayOf(e.atUnix) === TODAY);
    expect(today.map((e) => [e.eventId, e.status, e.source, e.clientEventId])).toEqual([
      ["e1", "OnDuty", "DriverApp", null],
      ["e2", "Driving", "DriverApp", null],
      ["e3", "OnDuty", "EldTranscription", null],
      ["e4", "OffDuty", "DriverApp", null],
    ]);
    expect(s.events.some((e) => e.eventId === "void")).toBe(false);
    expect(s.ledger.knownFromUnix).toBe(CALENDAR.dayStartUnix(addDays(TODAY, -40)));
    expect(s.ledger.certifiedDays).toHaveLength(13);
  });

  it("evaluates the server copy as the engine does: today's totals, the current status, the band", () => {
    return hydrate().then(() => {
      const e = evaluation();
      expect(e.currentStatus).toBe("OffDuty");
      expect(dutyStateOf(e.currentStatus)).toBe(HosWire.OffDuty);
      expect(e.today.drivingSeconds).toBe(2 * H + 45 * 60);
      expect(e.today.onDutySeconds).toBe(3 * H + 45 * 60);
      // Remaining driving is the engine's minimum over every rule: 10h15m of the daily 13h is
      // left, but the shift began at 06:00 and its 16h window closes at 22:00 — 7h from now.
      expect(e.shift.elapsedSeconds).toBe(9 * H);
      expect(e.remainingDrivingTodaySeconds).toBe(HOS_LIMITS.shiftElapsedSeconds - 9 * H);
      expect(e.blockingReasons).toEqual([]);
      expect(e.canDriveNow).toBe(true);
      expect(band(e)).toEqual({ kind: "ontime", label: "OK" });
    });
  });

  it("maps a PC opener's start odometer and every other event's end odometer to tenths of a km", async () => {
    listDutyDays.mockResolvedValue(
      fourteenDays([
        event({ eventId: "p1", sequence: 1, atUtc: "2026-10-10T11:00:00Z", status: "Personal Conveyance", odometerStartKm: 1234.5, odometerEndKm: null }),
        event({ eventId: "p2", sequence: 2, atUtc: "2026-10-10T12:00:00Z", status: "Off Duty", odometerStartKm: null, odometerEndKm: 1260.0 }),
      ]),
    );
    await hydrate();
    const today = getSnapshot().events.filter((e) => CALENDAR.dayOf(e.atUnix) === TODAY);
    expect(today.map((e) => e.odometerDecikm)).toEqual([12345, 12600]);
  });

  it("keeps the last copy and records the server's message when a fetch fails", async () => {
    await hydrate();
    const before = getSnapshot();
    listDutyDays.mockRejectedValue(new ApiError("Network.Unreachable", "Cannot reach the API.", 0));

    expect(await hydrate()).toBe(false);

    const after = getSnapshot();
    expect(after.events).toEqual(before.events);
    expect(after.hydration.source).toBe("server");
    expect(after.hydration.atUnix).toBe(NOW);
    expect(after.hydration.error).toEqual({ code: "Network.Unreachable", message: "Cannot reach the API." });
  });

  it("surfaces 404 Drivers.NotLinked as the hydration error, with no driver and no figures", async () => {
    getMyDriver.mockRejectedValue(new ApiError("Drivers.NotLinked", "This account is not linked to a driver record.", 404));
    expect(await hydrate()).toBe(false);
    expect(getSnapshot().hydration.error?.code).toBe("Drivers.NotLinked");
    expect(evaluation().blockingReasons[0]).toBe("NoLedger");
    expect(listDutyDays).not.toHaveBeenCalled();
  });
});

describe("appendDutyEvent — the optimistic half of a duty change", () => {
  beforeEach(() => hydrate());

  it("moves the current status and the figures immediately, carrying the command id", () => {
    const before = evaluation();
    const event = appendDutyEvent({ status: "Driving", clientEventId: "cmd-1" });
    const after = evaluation();

    expect(event).toMatchObject({ status: "Driving", source: "DriverApp", clientEventId: "cmd-1", eventId: null, atUnix: NOW });
    expect(event.sequence).toBeGreaterThan(Math.max(...before.days.map(() => 0), ...getSnapshot().events.slice(0, -1).map((e) => e.sequence)));
    expect(after.currentStatus).toBe("Driving");
    expect(after.currentStatusSinceUnix).toBe(NOW);
    expect(after.shift.mustStopDrivingByUnix).not.toBeNull();
    expect(getSnapshot().events.at(-1)).toBe(event);
  });

  it("counts driving time down as the clock advances", () => {
    appendDutyEvent({ status: "Driving", clientEventId: "cmd-1" });
    const remaining = evaluation().remainingDrivingTodaySeconds;
    vi.setSystemTime(new Date((NOW + 45 * 60) * 1000));
    tick();
    expect(evaluation().remainingDrivingTodaySeconds).toBe(remaining - 45 * 60);
    expect(evaluation().today.drivingSeconds).toBe(2 * H + 45 * 60 + 45 * 60);
  });

  it("re-greys Driving when the shift's window is used up — the reason reads on screen", () => {
    // The window (16h from the 06:00 start) is the binding rule for this timeline, so driving
    // to the end of the remaining figure closes it: ShiftWindowClosed, and the DutyControl
    // shows that sentence under the control.
    appendDutyEvent({ status: "Driving", clientEventId: "cmd-1" });
    const remaining = evaluation().remainingDrivingTodaySeconds;
    vi.setSystemTime(new Date((NOW + remaining) * 1000));
    appendDutyEvent({ status: "OnDuty", clientEventId: "cmd-2" });
    const e = evaluation();
    expect(e.canDriveNow).toBe(false);
    expect(e.blockingReasons).toContain("ShiftWindowClosed");
    expect(band(e)).toEqual({ kind: "over", label: "Do not drive" });
    expect(cannotDriveBecause(e)).toBe("16h shift window closed");
  });

  it("never lets as-of fall behind an appended event, so the engine cannot ignore it as future", () => {
    const event = appendDutyEvent({ status: "OnDuty", clientEventId: "cmd-1", atUnix: NOW + 20 * 60 });
    expect(evaluation().asOfUnix).toBe(event.atUnix);
    expect(evaluation().currentStatus).toBe("OnDuty");
    expect(evaluation().ledgerProblems.map((p) => p.code)).not.toContain("FutureEvent");
  });

  it("publishes a new snapshot to subscribers on every append, and a stable one between", () => {
    const seen: number[] = [];
    const unsubscribe = subscribe((s) => seen.push(s.asOfUnix));
    const first = getSnapshot();
    expect(getSnapshot()).toBe(first);
    appendDutyEvent({ status: "OnDuty", clientEventId: "cmd-1" });
    expect(seen).toHaveLength(1);
    expect(getSnapshot()).not.toBe(first);
    unsubscribe();
    appendDutyEvent({ status: "Driving", clientEventId: "cmd-2" });
    expect(seen).toHaveLength(1);
  });
});

describe("reconciliation — the server wins", () => {
  it("drops the optimistic event once the server holds an event with the same id", async () => {
    await hydrate();
    appendDutyEvent({ status: "Driving", clientEventId: "cmd-1" });
    expect(getSnapshot().events.filter((e) => e.clientEventId === "cmd-1")).toHaveLength(1);

    listDutyDays.mockResolvedValue(
      fourteenDays([...TODAY_EVENTS, event({ eventId: "cmd-1", sequence: 6, atUtc: NOW_ISO, status: "Driving" })]),
    );
    await hydrate();

    const matches = getSnapshot().events.filter((e) => e.eventId === "cmd-1" || e.clientEventId === "cmd-1");
    expect(matches).toHaveLength(1);
    expect(matches[0]).toMatchObject({ eventId: "cmd-1", clientEventId: null, status: "Driving" });
    expect(evaluation().currentStatus).toBe("Driving");
  });

  it("drops the optimistic event when the server refuses its command, and the figures follow", async () => {
    // The real path: enqueue → append → drain → 4xx parked. The DutyControl showed Driving the
    // moment the driver tapped; the server said no; the figures go back to the server's.
    const stop = startLedger("user-1");
    await vi.waitFor(() => expect(getSnapshot().hydration.source).toBe("server"));

    const id = await enqueue("hos.duty", (commandId) => dutyCommandPayload("Driving", NOW_ISO, commandId));
    appendDutyEvent({ status: "Driving", clientEventId: id });
    expect(evaluation().currentStatus).toBe("Driving");

    recordDutyEvent.mockRejectedValue(new ApiError("Drivers.Hos.EventInFuture", "The event is more than five minutes in the future.", 400));
    await drain();

    expect(recordDutyEvent).toHaveBeenCalledWith(DRIVER, { eventId: id, atUtc: NOW_ISO, status: "Driving", source: "Driver App" });
    expect(getSnapshot().events.some((e) => e.clientEventId === id)).toBe(false);
    expect(evaluation().currentStatus).toBe("OffDuty");
    stop();
  });

  it("re-fetches after a drain that sent something, and the certify sender posts the day", async () => {
    const stop = startLedger("user-1");
    await vi.waitFor(() => expect(getSnapshot().hydration.source).toBe("server"));
    const fetches = listDutyDays.mock.calls.length;

    await enqueue("hos.certify", { date: TODAY });
    await drain();

    expect(certifyDay).toHaveBeenCalledWith(DRIVER, TODAY);
    await vi.waitFor(() => expect(listDutyDays.mock.calls.length).toBe(fetches + 1));
    stop();
  });

  it("restores held duty changes from the queue on start, so a reload keeps the optimistic figures", async () => {
    const id = await enqueue("hos.duty", (commandId) => dutyCommandPayload("Driving", NOW_ISO, commandId));
    resetLedgerForTests();

    const stop = startLedger("user-1");
    await vi.waitFor(() => expect(getSnapshot().hydration.source).toBe("server"));

    expect(getSnapshot().events.at(-1)).toMatchObject({ clientEventId: id, status: "Driving", eventId: null });
    expect(evaluation().currentStatus).toBe("Driving");
    stop();
  });
});

describe("the cache — a reload in a dead zone", () => {
  it("shows the cached server copy for the same account when the network is gone, and says so", async () => {
    const stop = startLedger("user-1");
    await vi.waitFor(() => expect(getSnapshot().hydration.source).toBe("server"));
    stop();
    const live = getSnapshot();

    resetLedgerForTests();
    getMyDriver.mockRejectedValue(new ApiError("Network.Unreachable", "Cannot reach the API.", 0));
    listDutyDays.mockRejectedValue(new ApiError("Network.Unreachable", "Cannot reach the API.", 0));

    const stopAgain = startLedger("user-1");
    await vi.waitFor(() => expect(getSnapshot().hydration.error).not.toBeNull());

    const cached = getSnapshot();
    expect(cached.hydration.source).toBe("cache");
    expect(cached.hydration.atUnix).toBe(NOW);
    expect(cached.events.map((e) => e.eventId)).toEqual(live.events.map((e) => e.eventId));
    expect(cached.ledger.driverId).toBe(DRIVER);
    expect(evaluation().canDriveNow).toBe(true);
    stopAgain();
  });

  it("ignores a cache written for a different account", async () => {
    const stop = startLedger("user-1");
    await vi.waitFor(() => expect(getSnapshot().hydration.source).toBe("server"));
    stop();

    resetLedgerForTests();
    getMyDriver.mockRejectedValue(new ApiError("Network.Unreachable", "Cannot reach the API.", 0));
    const stopAgain = startLedger("user-2");
    await vi.waitFor(() => expect(getSnapshot().hydration.error).not.toBeNull());

    expect(getSnapshot().hydration.source).toBe("none");
    expect(getSnapshot().events).toHaveLength(0);
    stopAgain();
  });
});

describe("retention", () => {
  it("keeps only the last 14 regulation days of events, cut at a day boundary, and still knows from before that", () => {
    const knownFrom = CALENDAR.dayStartUnix(addDays(TODAY, -20));
    const old = CALENDAR.dayStartUnix(addDays(TODAY, -15));
    const cutoff = CALENDAR.dayStartUnix(addDays(TODAY, -RETAINED_DAYS));
    installLedgerForTests({
      knownFromUnix: knownFrom,
      events: [
        { atUnix: knownFrom, status: "OffDuty", source: "DriverApp", sequence: 1, odometerDecikm: null, note: null },
        { atUnix: old, status: "OnDuty", source: "DriverApp", sequence: 2, odometerDecikm: null, note: null },
        { atUnix: old + H, status: "OffDuty", source: "DriverApp", sequence: 3, odometerDecikm: null, note: null },
        { atUnix: cutoff, status: "OffDuty", source: "DriverApp", sequence: 4, odometerDecikm: null, note: null },
      ],
      certifiedDays: [],
      asOfUnix: NOW,
    });
    const s = getSnapshot();
    expect(s.events.every((e) => e.atUnix >= cutoff)).toBe(true);
    expect(s.events[0].atUnix).toBe(cutoff);
    expect(s.ledger.knownFromUnix).toBe(knownFrom);
  });
});
