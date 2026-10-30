import type { BudgetPeriod } from "@/lib/types";
import type { AccessClaims } from "@/lib/claims";

// The period the planner is working in — "enter a period" rather than a shared selection that
// three controls could silently change. Console holds only the entered id; everything else is
// derived here, so it is pure and tested (lib/workingPeriod.test.ts).
//
// Persistence is sessionStorage, deliberately not localStorage:
//   - it survives a reload, which is the whole point;
//   - it dies with the tab, so a shared machine never reopens someone else's period;
//   - two tabs can work in two different periods without fighting over one key;
//   - the key names the tenant AND the user, so a sign-out and sign-in in the same tab never
//     inherits the previous account's period.
// Every access is wrapped in try/catch, as lib/auth.ts does for its refresh token: a browser
// that refuses storage (privacy mode, a sandboxed frame) degrades to "not remembered", never to
// a crash.

/** Today's date as the planner's own calendar sees it — local, not UTC. */
export function localIsoDate(date: Date = new Date()): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${y}-${m}-${d}`;
}

/**
 * The period the chooser highlights on a first visit: the one containing today (both ends
 * inclusive), else the one with the latest start, else none. A suggestion only — nothing is
 * entered until the planner clicks it.
 */
export function suggestedPeriodId(periods: BudgetPeriod[], todayIso: string): string | null {
  if (periods.length === 0) return null;
  const current = periods.find((p) => p.startsOn <= todayIso && todayIso <= p.endsOn);
  if (current) return current.id;
  return periods.reduce((a, b) => (a.startsOn >= b.startsOn ? a : b)).id;
}

/** Why the suggested row is suggested — the chooser's tag text. */
export function suggestionReason(
  periods: BudgetPeriod[],
  todayIso: string,
): "INCLUDES TODAY" | "LATEST" | null {
  const id = suggestedPeriodId(periods, todayIso);
  if (id === null) return null;
  const p = periods.find((x) => x.id === id)!;
  return p.startsOn <= todayIso && todayIso <= p.endsOn ? "INCLUDES TODAY" : "LATEST";
}

export interface EnteredPeriod {
  /** The period being worked in, or null when none is entered (or it cannot be found). */
  period: BudgetPeriod | null;
  /**
   * True only when an id WAS entered, the list loaded successfully, and nothing matches — the
   * period was deleted or belongs to data this account no longer sees. A failed or pending load
   * is never "lost": the period may well exist, we just could not ask.
   */
  lost: boolean;
}

export function resolveEnteredPeriod(
  periods: BudgetPeriod[] | null,
  enteredId: string | null,
  loadFailed: boolean,
): EnteredPeriod {
  if (enteredId === null) return { period: null, lost: false };
  const period = periods?.find((p) => p.id === enteredId) ?? null;
  if (period) return { period, lost: false };
  return { period: null, lost: periods !== null && !loadFailed };
}

const KEY_PREFIX = "nl.budgeting.enteredPeriod";

/** `nl.budgeting.enteredPeriod.{tenantId}.{sub}`, or null when there is no usable identity. */
export function enteredPeriodStorageKey(
  claims: Pick<AccessClaims, "tenantId" | "sub"> | null,
): string | null {
  if (!claims || !claims.tenantId || !claims.sub) return null;
  return `${KEY_PREFIX}.${claims.tenantId}.${claims.sub}`;
}

export function readEnteredPeriod(key: string | null): string | null {
  if (key === null) return null;
  try {
    const value = window.sessionStorage.getItem(key);
    return value ? value : null;
  } catch {
    return null;
  }
}

/** Stores the entered id; null removes the entry. A null key stores nothing. */
export function writeEnteredPeriod(key: string | null, id: string | null): void {
  if (key === null) return;
  try {
    if (id === null) window.sessionStorage.removeItem(key);
    else window.sessionStorage.setItem(key, id);
  } catch {
    // Storage refused — the period simply is not remembered across a reload.
  }
}
