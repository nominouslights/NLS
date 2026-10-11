"use client";

import { useEffect, useSyncExternalStore } from "react";
import { alertThresholdCrossed, limitRemaining, type AlertLimit } from "./display";
import { getSnapshot, subscribe as subscribeLedger, tick, type LedgerSnapshot } from "./ledger";

// ---------------------------------------------------------------------------
// The hours alerts — when the tablet interrupts a driver, and with what.
//
// A module-level store driven straight from the ledger (not from React), read through
// useSyncExternalStore by components/HosAlerts.tsx, which Console renders on every screen.
// Store-not-effect for the two reasons every store in this app gives (lib/useInspectionStore.ts):
// `react-hooks/set-state-in-effect` is one of the inherited lint errors and a fourth would
// break the bar, and the whole state machine is then testable with fake timers and no DOM.
//
// THE RULES (owner's policy, 2026-10-09; the thresholds are lib/hos/display.ts's):
//   - Every 30 seconds the ledger's as-of moves to now (tick), so the four remaining figures
//     — driving, on-duty, shift window, cycle — count down while the screen sits still.
//   - A figure crossing 60 or 30 minutes from above, while Driving or On Duty, opens a
//     DISMISSIBLE modal: "Approaching a limit — … ends in n min. Plan to stop at the nearest
//     safe location and call dispatch."
//   - A figure reaching 0 while Driving opens a NON-DISMISSIBLE modal with a tel: button,
//     cleared only when duty leaves Driving; from then on a persistent out-of-hours banner
//     stays up. Reaching 0 while merely On Duty shows the banner alone (there is nothing to
//     pull over from).
//   - Once per (limit, threshold) per duty period. The period is the engine's shift — it
//     resets after an off-duty run of 8h or more, which is exactly when the figures reset.
//   - A dismissed 60/30 alert comes back after 10 minutes if the driver is still Driving.
//   - Nothing fires while Off Duty or in Personal Conveyance; the last unsuppressed figures
//     are kept, so a crossing that happened during a break still fires on return to duty.
//   - Several figures crossing the same threshold at once (driving and the window, say) make
//     ONE modal, naming the most specific limit; every one of them counts as fired.
// ---------------------------------------------------------------------------

export const ALERT_TICK_MS = 30_000;
export const ALERT_REMIND_MS = 10 * 60_000;

export interface HosAlert {
  limit: AlertLimit;
  thresholdSeconds: number;
  /** False at 0: the modal stays until duty leaves Driving. */
  dismissible: boolean;
  /** True when this is the 10-minute reminder of a dismissed alert. */
  reminder: boolean;
  firedAtUnix: number;
}

export interface HosAlertsSnapshot {
  modal: HosAlert | null;
  /** Set once a figure hit 0 in this duty period: the limit that ran out. Cleared with the period. */
  outOfHours: AlertLimit | null;
}

/** Most specific first: when several cross together, the modal names the first of these. */
const LIMIT_PRIORITY: readonly AlertLimit[] = ["window", "cycle", "onDuty", "driving"];

const EMPTY: HosAlertsSnapshot = { modal: null, outOfHours: null };

type Listener = (snapshot: HosAlertsSnapshot) => void;
const listeners = new Set<Listener>();

let snapshot: HosAlertsSnapshot = EMPTY;
let periodKey: number | null | undefined;
let fired = new Set<string>();
let prev: Record<AlertLimit, number | null> = { driving: null, onDuty: null, window: null, cycle: null };
let dismissed: { alert: HosAlert; atUnix: number } | null = null;

function publish(next: HosAlertsSnapshot): void {
  if (next.modal === snapshot.modal && next.outOfHours === snapshot.outOfHours) return;
  snapshot = next;
  for (const listener of [...listeners]) listener(snapshot);
}

/** Runs the rules against one ledger snapshot. Pure apart from the module state it advances. */
export function evaluateAlerts(ledger: LedgerSnapshot): void {
  const e = ledger.evaluation;
  const now = e.asOfUnix;
  let modal = snapshot.modal;
  let outOfHours = snapshot.outOfHours;

  // A new duty period (the engine's shift reset after ≥ 8h off, or no shift at all) forgets
  // what fired: the figures it was about are gone.
  if (e.shift.startUnix !== periodKey) {
    periodKey = e.shift.startUnix;
    fired = new Set();
    dismissed = null;
    outOfHours = null;
    if (modal !== null && !modal.dismissible) modal = null;
  }

  const driving = e.currentStatus === "Driving";
  const onDuty = e.currentStatus === "OnDuty";
  const suppressed = !driving && !onDuty;

  // The stop modal is cleared by leaving Driving and by nothing else.
  if (modal !== null && !modal.dismissible && !driving) modal = null;

  const next = limitRemaining(e);

  if (!suppressed) {
    let candidate: HosAlert | null = null;
    for (const limit of LIMIT_PRIORITY) {
      const n = next[limit];
      if (n === null) continue;
      const p = prev[limit] ?? Number.POSITIVE_INFINITY;
      const t = alertThresholdCrossed(p, n);
      if (t === null) continue;
      const key = `${limit}:${t}`;
      if (fired.has(key)) continue;
      fired.add(key);
      if (t === 0) {
        if (outOfHours === null) outOfHours = limit;
        if (!driving) continue;
      }
      const alert: HosAlert = { limit, thresholdSeconds: t, dismissible: t > 0, reminder: false, firedAtUnix: now };
      if (candidate === null || t < candidate.thresholdSeconds) candidate = alert;
    }
    prev = next;

    if (candidate !== null && (modal === null || modal.dismissible)) {
      modal = candidate;
      dismissed = null;
    }
  }

  // The 10-minute reminder of a dismissed alert, while still Driving and still inside it.
  if (modal === null && dismissed !== null && driving && (now - dismissed.atUnix) * 1000 >= ALERT_REMIND_MS) {
    const n = next[dismissed.alert.limit];
    if (n !== null && n <= dismissed.alert.thresholdSeconds) {
      modal = { ...dismissed.alert, reminder: true, firedAtUnix: now };
    }
    dismissed = null;
  }

  publish({ modal, outOfHours });
}

/** The driver's OK on a 60/30 alert. The stop modal ignores it. */
export function dismissAlert(): void {
  const modal = snapshot.modal;
  if (modal === null || !modal.dismissible) return;
  dismissed = { alert: modal, atUnix: getSnapshot().evaluation.asOfUnix };
  publish({ ...snapshot, modal: null });
}

export function getAlertsSnapshot(): HosAlertsSnapshot {
  return snapshot;
}

export function getAlertsServerSnapshot(): HosAlertsSnapshot {
  return EMPTY;
}

export function subscribeAlerts(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

// --- lifecycle ----------------------------------------------------------------

let running = 0;
let stopRunning: (() => void) | null = null;

/**
 * Subscribes the rules to the ledger and starts the 30-second tick. Reference-counted, so
 * however many surfaces mount the hook there is one subscription and one timer. Returns the
 * release function.
 */
export function startAlerts(): () => void {
  running += 1;
  if (running === 1) {
    const unsubscribe = subscribeLedger(evaluateAlerts);
    const timer = setInterval(tick, ALERT_TICK_MS);
    evaluateAlerts(getSnapshot());
    stopRunning = () => {
      unsubscribe();
      clearInterval(timer);
    };
  }
  let released = false;
  return () => {
    if (released) return;
    released = true;
    running -= 1;
    if (running === 0 && stopRunning !== null) {
      stopRunning();
      stopRunning = null;
    }
  };
}

/** Test seam: forgets every fired threshold and the snapshot, and stops any running loop. */
export function resetAlertsForTests(): void {
  if (stopRunning !== null) stopRunning();
  stopRunning = null;
  running = 0;
  periodKey = undefined;
  fired = new Set();
  prev = { driving: null, onDuty: null, window: null, cycle: null };
  dismissed = null;
  snapshot = EMPTY;
}

// --- React -------------------------------------------------------------------

/** The alert state for the surface that renders it. Starts the rules on mount; one timer app-wide. */
export function useHosAlerts(): HosAlertsSnapshot {
  useEffect(() => startAlerts(), []);
  return useSyncExternalStore(subscribeAlerts, getAlertsSnapshot, getAlertsServerSnapshot);
}
