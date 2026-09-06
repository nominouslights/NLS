import type { DayName, ScheduleTemplateRecord } from "./api/trips";

// Pure, client-side mirror of the backend TripGenerator's recurrence branch
// (TripGenerator.MatchingDates) — used by the Special Dates calendar to mark
// which days a template WOULD fire on, before exceptions are applied. No
// horizon/window logic here: the calendar previews any month; how far ahead
// trips actually materialize stays the worker's business.
//
// Plain Date math (this app deliberately has no date library). All arithmetic
// runs on UTC day numbers so a DST transition can never make a day count off
// by one.

/** JS getUTCDay() index (0 = Sunday) → backend DayName. */
const DAY_BY_UTC_INDEX: DayName[] = [
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
];

type RecurrenceFields = Pick<
  ScheduleTemplateRecord,
  "recurrenceKind" | "daysOfWeek" | "intervalDays" | "anchorDate" | "daysOfMonth"
>;

/** "yyyy-MM-dd" → whole days since the epoch (UTC), or null when malformed. */
function dayNumber(iso: string): number | null {
  const [y, m, d] = iso.split("-").map(Number);
  if (!y || !m || !d) return null;
  return Math.floor(Date.UTC(y, m - 1, d) / 86_400_000);
}

/** Days in the month of a "yyyy-MM-dd" date (month-end clamp target). */
function daysInMonthOf(iso: string): number {
  const [y, m] = iso.split("-").map(Number);
  return new Date(Date.UTC(y, m, 0)).getUTCDate();
}

/**
 * Whether the template's recurrence fires on `dateIso` ("yyyy-MM-dd"):
 * - DaysOfWeek — the date's weekday is selected.
 * - EveryNDays — on or after the anchor, a whole number of intervals from it.
 * - MonthlyDays — the date is a configured day of month, where a configured
 *   day past the month's end clamps to month-end (31 → 28/29/30), matching
 *   the backend's Math.Min(configuredDay, daysInMonth).
 * Misconfigured fields (which template validation prevents) simply never match.
 */
export function templateOccursOn(t: RecurrenceFields, dateIso: string): boolean {
  switch (t.recurrenceKind) {
    case "DaysOfWeek": {
      const [y, m, d] = dateIso.split("-").map(Number);
      if (!y || !m || !d) return false;
      const name = DAY_BY_UTC_INDEX[new Date(Date.UTC(y, m - 1, d)).getUTCDay()];
      return t.daysOfWeek.includes(name);
    }
    case "EveryNDays": {
      if (t.intervalDays == null || t.intervalDays < 1 || !t.anchorDate) return false;
      const date = dayNumber(dateIso);
      const anchor = dayNumber(t.anchorDate);
      if (date === null || anchor === null || date < anchor) return false;
      return (date - anchor) % t.intervalDays === 0;
    }
    case "MonthlyDays": {
      if (t.daysOfMonth.length === 0) return false;
      const day = Number(dateIso.slice(8, 10));
      if (!day) return false;
      const monthLen = daysInMonthOf(dateIso);
      // A direct hit, or this is month-end and some configured day clamps here.
      return t.daysOfMonth.some((configured) => Math.min(configured, monthLen) === day);
    }
    default:
      return false;
  }
}
