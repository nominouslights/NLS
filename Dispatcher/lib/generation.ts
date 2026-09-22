// On-demand trip generation — the date window a dispatcher may ask the Trips
// module to materialize a schedule template through (GenerateTripsModal).
//
// The backend owns the real rule (ScheduleTemplate.MaxGenerateAheadDays and
// the InvalidGenerationWindow guard); these helpers only keep the dialog's
// date picker inside it so a dispatcher is steered rather than rejected. The
// shared DateField deliberately has no min/max props (it is a copied
// design-system component), so the picker's value is clamped on change instead.
//
// Plain Date math in the style of lib/schedule.ts (no date library). All
// arithmetic runs on UTC day numbers so a DST transition can never make a day
// count off by one, and no local Date is ever turned back into a string with
// toISOString().

/** Hard cap on how far ahead a template may be generated, in days from today.
 *  Mirrors ScheduleTemplate.MaxGenerateAheadDays — keep the two in step. */
export const GENERATE_MAX_DAYS_AHEAD = 366;

const DAY_MS = 86_400_000;

/** "yyyy-MM-dd" → [year, month(1-12), day], or null when malformed or not a
 *  real calendar date (2026-02-30 is rejected, not rolled into March). */
function parts(iso: string): [number, number, number] | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(iso)) return null;
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || m < 1 || m > 12 || d < 1) return null;
  const probe = new Date(Date.UTC(y, m - 1, d));
  if (probe.getUTCFullYear() !== y || probe.getUTCMonth() !== m - 1 || probe.getUTCDate() !== d) return null;
  return [y, m, d];
}

/** "yyyy-MM-dd" → whole days since the epoch (UTC), or null when malformed. */
function dayNumber(iso: string): number | null {
  const p = parts(iso);
  return p === null ? null : Math.floor(Date.UTC(p[0], p[1] - 1, p[2]) / DAY_MS);
}

/** Whole days since the epoch (UTC) → "yyyy-MM-dd". */
function isoOf(day: number): string {
  const d = new Date(day * DAY_MS);
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getUTCFullYear()}-${p(d.getUTCMonth() + 1)}-${p(d.getUTCDate())}`;
}

/**
 * The dialog's default "through" date: the last day of the month AFTER
 * `todayIso`'s month (December → the following January 31; January 31 → the
 * last day of February, 28 or 29). A malformed `todayIso` is returned unchanged
 * — todayIso() never produces one, and there is nothing sensible to compute.
 */
export function endOfNextMonthIso(todayIso: string): string {
  const p = parts(todayIso);
  if (p === null) return todayIso;
  // Date.UTC(y, m + 1, 0): month index m+1 is the month AFTER next (0-based),
  // day 0 rolls back to the last day of the one before it — i.e. next month.
  // Month index 13 simply wraps into the following year.
  return isoOf(Math.floor(Date.UTC(p[0], p[1] + 1, 0) / DAY_MS));
}

/** The furthest "through" date allowed: today + GENERATE_MAX_DAYS_AHEAD days. */
export function maxGenerateThroughIso(todayIso: string): string {
  const today = dayNumber(todayIso);
  if (today === null) return todayIso;
  return isoOf(today + GENERATE_MAX_DAYS_AHEAD);
}

/**
 * Keeps a picked "through" date inside the generation window:
 * - malformed/empty → the default (end of next month);
 * - before today → today;
 * - beyond the cap → the cap (exactly the cap is allowed);
 * - otherwise the date as given.
 */
export function clampThroughIso(todayIso: string, throughIso: string): string {
  const today = dayNumber(todayIso);
  const through = dayNumber(throughIso);
  if (today === null) return throughIso; // nothing to clamp against
  if (through === null) return endOfNextMonthIso(todayIso);
  if (through < today) return todayIso;
  const max = today + GENERATE_MAX_DAYS_AHEAD;
  if (through > max) return isoOf(max);
  return throughIso;
}
