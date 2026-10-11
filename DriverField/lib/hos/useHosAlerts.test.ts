import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { addDays, type DateOnly, type DutyEvent, type DutyStatus } from "./engine";
import { ALERT_THRESHOLDS_SECONDS } from "./display";
import { appendDutyEvent, CALENDAR, evaluation, installLedgerForTests, resetLedgerForTests, tick } from "./ledger";
import { ALERT_REMIND_MS, ALERT_TICK_MS, dismissAlert, getAlertsSnapshot, resetAlertsForTests, startAlerts } from "./useHosAlerts";

// The alert rules against a real ledger evaluated by the real engine, on a controlled clock.
// Each case sets up a timeline that puts one figure a minute above a threshold, moves the
// clock across it, ticks, and reads what the driver would see.

const H = 3600;
const M = 60;
/** 15:00 CDT. */
const NOW_ISO = "2026-10-10T20:00:00.000Z";
const NOW = Date.parse(NOW_ISO) / 1000;

function ev(atUnix: number, status: DutyStatus, sequence: number): DutyEvent {
  return { atUnix, status, source: "DriverApp", sequence, odometerDecikm: null, note: null };
}

/** Twenty certified days of nothing, then the given timeline, evaluated now. */
function install(timeline: [secondsAgo: number, status: DutyStatus][]): void {
  const knownFrom = NOW - 20 * 86400;
  const certifiedDays: DateOnly[] = [];
  for (let d = CALENDAR.dayOf(knownFrom); d <= CALENDAR.dayOf(NOW); d = addDays(d, 1)) certifiedDays.push(d);
  const events = [ev(knownFrom, "OffDuty", 1), ...timeline.map(([ago, status], i) => ev(NOW - ago, status, i + 2))];
  installLedgerForTests({ knownFromUnix: knownFrom, events, certifiedDays, asOfUnix: NOW });
}

function advance(seconds: number): void {
  vi.setSystemTime(new Date((Date.now() / 1000 + seconds) * 1000));
  tick();
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date", "setInterval", "clearInterval"] });
  vi.setSystemTime(new Date(NOW_ISO));
  resetAlertsForTests();
  resetLedgerForTests();
});

afterEach(() => {
  resetAlertsForTests();
  vi.useRealTimers();
});

describe("thresholds", () => {
  it("are 60, 30 and 0 minutes, in that order", () => {
    expect([...ALERT_THRESHOLDS_SECONDS]).toEqual([60 * M, 30 * M, 0]);
  });
});

describe("crossings while Driving", () => {
  it("61 → 59 min of driving left fires the 60-minute alert, dismissible", () => {
    install([[11 * H + 59 * M, "Driving"]]);
    startAlerts();
    expect(evaluation().remainingDrivingTodaySeconds).toBe(61 * M);
    expect(getAlertsSnapshot().modal).toBeNull();

    advance(2 * M);

    expect(getAlertsSnapshot().modal).toMatchObject({ limit: "driving", thresholdSeconds: 60 * M, dismissible: true, reminder: false });
  });

  it("31 → 29 fires the 30-minute alert", () => {
    install([[12 * H + 29 * M, "Driving"]]);
    startAlerts();
    // Opening the app with 31 min left is itself inside the 60-minute band: that fires once.
    expect(getAlertsSnapshot().modal?.thresholdSeconds).toBe(60 * M);
    dismissAlert();

    advance(2 * M);

    expect(getAlertsSnapshot().modal).toMatchObject({ limit: "driving", thresholdSeconds: 30 * M, dismissible: true });
  });

  it("1 → 0 fires the stop alert: not dismissible, cleared only when duty leaves Driving, then the banner stays", () => {
    install([[12 * H + 59 * M, "Driving"]]);
    startAlerts();
    expect(evaluation().remainingDrivingTodaySeconds).toBe(60);
    // One minute left is inside the 30-minute band: that fires once on opening, and is dismissible.
    expect(getAlertsSnapshot().modal).toMatchObject({ thresholdSeconds: 30 * M, dismissible: true });
    dismissAlert();

    advance(60);

    const stop = getAlertsSnapshot().modal;
    expect(stop).toMatchObject({ limit: "driving", thresholdSeconds: 0, dismissible: false });
    expect(evaluation().canDriveNow).toBe(false);
    dismissAlert();
    expect(getAlertsSnapshot().modal).toBe(stop);
    expect(getAlertsSnapshot().outOfHours).toBe("driving");

    appendDutyEvent({ status: "OnDuty", clientEventId: "cmd-stop" });

    expect(getAlertsSnapshot().modal).toBeNull();
    expect(getAlertsSnapshot().outOfHours).toBe("driving");
  });

  it("fires once per (limit, threshold) per duty period", () => {
    install([[11 * H + 59 * M, "Driving"]]);
    startAlerts();
    advance(2 * M);
    expect(getAlertsSnapshot().modal?.thresholdSeconds).toBe(60 * M);
    dismissAlert();

    advance(M);
    advance(M);

    expect(getAlertsSnapshot().modal).toBeNull();
  });

  it("names the most specific limit when several cross together, and counts all of them as fired", () => {
    // Driving since 14h59m ago with a 6h off-duty break inside the shift: the 16h window has
    // 61 min left while driving and on-duty have hours. Back to Driving, both the window and
    // the engine's remaining-driving figure (which is the minimum of everything) cross 60
    // together — one modal, naming the window.
    install([
      [14 * H + 59 * M, "Driving"],
      [6 * H, "OffDuty"],
    ]);
    startAlerts();
    appendDutyEvent({ status: "Driving", clientEventId: "cmd-back", atUnix: NOW });
    expect(getAlertsSnapshot().modal).toBeNull();

    advance(2 * M);

    expect(getAlertsSnapshot().modal).toMatchObject({ limit: "window", thresholdSeconds: 60 * M });
    dismissAlert();
    advance(M);
    expect(getAlertsSnapshot().modal).toBeNull();
  });
});

describe("suppression", () => {
  it("fires nothing while Off Duty, keeps the last figures, and fires on return to duty", () => {
    install([
      [14 * H + 59 * M, "Driving"],
      [6 * H, "OffDuty"],
    ]);
    startAlerts();
    expect(evaluation().currentStatus).toBe("OffDuty");

    advance(2 * M);
    expect(getAlertsSnapshot().modal).toBeNull();

    appendDutyEvent({ status: "Driving", clientEventId: "cmd-back" });

    expect(getAlertsSnapshot().modal).toMatchObject({ limit: "window", thresholdSeconds: 60 * M });
  });

  it("fires nothing while in Personal Conveyance", () => {
    install([
      [14 * H + 59 * M, "Driving"],
      [6 * H, "PersonalConveyance"],
    ]);
    startAlerts();
    advance(2 * M);
    expect(getAlertsSnapshot().modal).toBeNull();
  });

  it("shows the banner but no stop modal when a limit runs out while merely On Duty", () => {
    install([[13 * H + 59 * M, "OnDuty"]]);
    startAlerts();
    dismissAlert();
    advance(2 * M);

    expect(getAlertsSnapshot().modal).toBeNull();
    expect(getAlertsSnapshot().outOfHours).toBe("onDuty");
  });
});

describe("reminders and periods", () => {
  it("comes back 10 minutes after a dismissal if still Driving, and not if Off Duty", () => {
    install([[11 * H + 59 * M, "Driving"]]);
    startAlerts();
    advance(2 * M);
    dismissAlert();

    advance(ALERT_REMIND_MS / 1000 - M);
    expect(getAlertsSnapshot().modal).toBeNull();
    advance(M);
    expect(getAlertsSnapshot().modal).toMatchObject({ limit: "driving", thresholdSeconds: 60 * M, reminder: true });

    dismissAlert();
    appendDutyEvent({ status: "OffDuty", clientEventId: "cmd-off" });
    advance(ALERT_REMIND_MS / 1000 + M);
    expect(getAlertsSnapshot().modal).toBeNull();
  });

  it("forgets what fired, and the banner, when a new duty period begins after 8h off", () => {
    install([[12 * H + 59 * M, "Driving"]]);
    startAlerts();
    dismissAlert();
    advance(60 * M);
    expect(getAlertsSnapshot().outOfHours).toBe("driving");
    appendDutyEvent({ status: "OffDuty", clientEventId: "cmd-off" });
    expect(getAlertsSnapshot().outOfHours).toBe("driving");

    advance(8 * H + M);

    expect(evaluation().shift.startUnix).toBeNull();
    expect(getAlertsSnapshot().outOfHours).toBeNull();
    expect(getAlertsSnapshot().modal).toBeNull();
  });

  it("ticks the ledger every 30 seconds while started, and stops when released", () => {
    install([[H, "Driving"]]);
    const stop = startAlerts();
    const before = evaluation().asOfUnix;

    // Advancing the fake timers moves the faked Date with them.
    vi.advanceTimersByTime(ALERT_TICK_MS);
    expect(evaluation().asOfUnix).toBe(before + 30);

    stop();
    vi.advanceTimersByTime(ALERT_TICK_MS * 2);
    expect(evaluation().asOfUnix).toBe(before + 30);
  });
});
