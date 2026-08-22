/**
 * Month/quarter period math for the Trips list's period navigator.
 *
 * There is no date library in this app, so this is plain Date arithmetic (the same
 * convention ui/MonthGrid.tsx follows). Everything is computed in LOCAL time and
 * formatted by hand — service dates are local operational days, so `toISOString()`
 * is never used here (it would shift the day for anyone west of UTC).
 */

export type Granularity = "month" | "quarter";

export interface Period {
  granularity: Granularity;
  /** First day of the month or quarter, "yyyy-MM-dd". */
  start: string;
  /** Last day of the month or quarter, inclusive, "yyyy-MM-dd". */
  end: string;
}

const MONTHS = [
  "January",
  "February",
  "March",
  "April",
  "May",
  "June",
  "July",
  "August",
  "September",
  "October",
  "November",
  "December",
];

const MONTHS_SHORT = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

function iso(year: number, monthIndex: number, day: number): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${year}-${p(monthIndex + 1)}-${p(day)}`;
}

/** Day count of a month — day 0 of the next month is the last day of this one. */
function daysInMonth(year: number, monthIndex: number): number {
  return new Date(year, monthIndex + 1, 0).getDate();
}

/** Parses "yyyy-MM-dd" without going through Date, which would apply a timezone. */
function parts(isoDate: string): { year: number; monthIndex: number } {
  const [y, m] = isoDate.split("-").map(Number);
  return { year: y, monthIndex: m - 1 };
}

function build(year: number, monthIndex: number, granularity: Granularity): Period {
  const startMonth = granularity === "quarter" ? Math.floor(monthIndex / 3) * 3 : monthIndex;
  const endMonth = granularity === "quarter" ? startMonth + 2 : monthIndex;
  return {
    granularity,
    start: iso(year, startMonth, 1),
    end: iso(year, endMonth, daysInMonth(year, endMonth)),
  };
}

/** The month or quarter containing today. */
export function currentPeriod(granularity: Granularity): Period {
  const now = new Date();
  return build(now.getFullYear(), now.getMonth(), granularity);
}

/** The month or quarter containing the given "yyyy-MM-dd" service date. */
export function periodContaining(isoDate: string, granularity: Granularity): Period {
  const { year, monthIndex } = parts(isoDate);
  return build(year, monthIndex, granularity);
}

/** Steps one whole period backwards (-1) or forwards (+1), rolling the year. */
export function stepPeriod(period: Period, delta: -1 | 1): Period {
  const { year, monthIndex } = parts(period.start);
  const months = period.granularity === "quarter" ? 3 : 1;
  const shifted = new Date(year, monthIndex + delta * months, 1);
  return build(shifted.getFullYear(), shifted.getMonth(), period.granularity);
}

/**
 * Re-buckets the period at a different granularity, keeping the anchor date — so
 * switching Month→Quarter while viewing August lands on that August's quarter, not
 * on the current one.
 */
export function withGranularity(period: Period, granularity: Granularity): Period {
  const { year, monthIndex } = parts(period.start);
  return build(year, monthIndex, granularity);
}

/** "August 2026" · "Q3 2026 · Jul – Sep" */
export function periodLabel(period: Period): string {
  const { year, monthIndex } = parts(period.start);
  if (period.granularity === "month") return `${MONTHS[monthIndex]} ${year}`;
  const quarter = Math.floor(monthIndex / 3) + 1;
  return `Q${quarter} ${year} · ${MONTHS_SHORT[monthIndex]} – ${MONTHS_SHORT[monthIndex + 2]}`;
}

/** Whether a "yyyy-MM-dd" service date falls inside the period (ISO dates sort lexically). */
export function periodContains(period: Period, isoDate: string): boolean {
  return isoDate >= period.start && isoDate <= period.end;
}
