import type { StatusKind } from "./theme";
import { formatKm } from "./api/format";
import type {
  PmDueStateWire,
  PmEntryKindWire,
  PmTaskWire,
  PmTierWire,
} from "./api/pm";

// Wire ↔ display boundary for the preventative-maintenance contract
// (workOrderDisplay.ts pattern). Wire enums travel as backend enum names
// ("NotYetRecorded", "DueSoon", "Primary", …); everything shown to a
// dispatcher goes through the maps and formatters here. Chip colours come
// from StatusKind via the existing StatusChip (colour + glyph + text label,
// never colour alone) — no hexes live in this module.

/** PmDueState → chip colour. NotYetRecorded is "unknown", not "compliant" —
 *  it renders as the hashed neutral chip, never green. */
export function pmKind(state: PmDueStateWire): StatusKind {
  switch (state) {
    case "Ok":
      return "ontime";
    case "DueSoon":
      return "soon";
    case "Overdue":
      return "over";
    case "NotYetRecorded":
    default:
      return "off";
  }
}

/** PmDueState → chip text label. */
export const PM_STATE_LABEL: Record<PmDueStateWire, string> = {
  NotYetRecorded: "Not recorded",
  Ok: "OK",
  DueSoon: "Due soon",
  Overdue: "Overdue",
};

export const TIER_LABEL: Record<PmTierWire, string> = {
  Primary: "Primary",
  Secondary: "Secondary",
};

export const TASK_LABEL: Record<PmTaskWire, string> = {
  Inspect: "Inspect",
  Service: "Service",
  Test: "Test",
  Replace: "Replace",
};

export const KIND_LABEL: Record<PmEntryKindWire, string> = {
  Item: "Item",
  Overhaul: "Overhaul",
};

const AVG_DAYS_PER_MONTH = 30.4375;

// Mirror of PmSchedule.DefaultLeadKm / DefaultLeadDays — the backend's
// absolute DueSoon windows, used to name the arm that actually triggered
// the state (a per-entry lead override travels on the wire as leadKm/leadDays).
const DEFAULT_LEAD_KM = 2000;
const DEFAULT_LEAD_DAYS = 30;

/** "12 d" under two months, "3 mo" beyond (magnitude only — sign handled by caller). */
function daysMagnitudeLabel(days: number): string {
  const abs = Math.abs(days);
  if (abs >= 60) return `${Math.round(abs / AVG_DAYS_PER_MONTH)} mo`;
  return `${abs} d`;
}

/** The fields pmDueLabel needs — satisfied by PmEntryStatusWire and PmOverhaulStatusWire. */
export interface PmDueArms {
  state: PmDueStateWire;
  intervalKm: number | null;
  intervalMonths: number | null;
  leadKm: number | null;
  leadDays: number | null;
  kmRemaining: number | null;
  daysRemaining: number | null;
}

/**
 * Which arm (km or days) the due line should name. The backend drives the
 * state from absolute lead windows (leadKm ?? 2000 km / leadDays ?? 30 d),
 * so the label must name the arm that tripped it: an overdue arm first,
 * then an arm inside its lead window; ties break on remaining-to-lead
 * ratio, and only when both arms are comfortably Ok does the
 * fraction-of-interval comparison decide (purely informational there).
 */
function tighterArm(e: PmDueArms): "km" | "days" | null {
  const hasKm = e.kmRemaining != null;
  const hasDays = e.daysRemaining != null;
  if (hasKm && !hasDays) return "km";
  if (hasDays && !hasKm) return "days";
  if (!hasKm && !hasDays) return null;

  const km = e.kmRemaining as number;
  const days = e.daysRemaining as number;
  const kmLead = e.leadKm ?? DEFAULT_LEAD_KM;
  const daysLead = e.leadDays ?? DEFAULT_LEAD_DAYS;

  const kmOver = km <= 0;
  const daysOver = days <= 0;
  if (kmOver !== daysOver) return kmOver ? "km" : "days";

  const kmIn = km <= kmLead;
  const daysIn = days <= daysLead;
  if (!kmOver && kmIn !== daysIn) return kmIn ? "km" : "days";
  if (kmOver || kmIn) return km / kmLead <= days / daysLead ? "km" : "days";

  // Both arms comfortably Ok — compare how much of each interval is used.
  const kmFrac = e.intervalKm ? km / e.intervalKm : null;
  const daysFrac = e.intervalMonths ? days / (e.intervalMonths * AVG_DAYS_PER_MONTH) : null;
  if (kmFrac != null && daysFrac != null) return daysFrac < kmFrac ? "days" : "km";
  if (daysFrac != null) return "days";
  return "km";
}

/**
 * The one-line human due summary for a PM entry, built from the arm that
 * governs the state: "Due in 1,800 km", "Due in 12 d", "Overdue 400 km",
 * "Overdue 3 mo", "Overdue now" (exactly at the threshold — the backend
 * flips to Overdue at remaining <= 0), "Not yet recorded". Pair it with the
 * state chip (pmKind + PM_STATE_LABEL) — this line is detail, not the
 * status indicator.
 */
export function pmDueLabel(entry: PmDueArms): string {
  if (entry.state === "NotYetRecorded") return "Not yet recorded";

  const arm = tighterArm(entry);
  if (arm === null) return PM_STATE_LABEL[entry.state];

  const remaining = arm === "km" ? (entry.kmRemaining as number) : (entry.daysRemaining as number);
  const magnitude = arm === "km" ? formatKm(Math.abs(remaining)) : daysMagnitudeLabel(remaining);
  if (remaining < 0) return `Overdue ${magnitude}`;
  if (remaining === 0) return "Overdue now";
  return `Due in ${magnitude}`;
}

/**
 * Interval spec → "10,000 km / 6 mo", "5,000 km", "48 mo"; "—" when neither
 * arm applies. Works for plan items, overhaul specs, and computed entries.
 */
export function pmIntervalLabel(spec: { intervalKm: number | null; intervalMonths: number | null }): string {
  const parts: string[] = [];
  if (spec.intervalKm != null) parts.push(formatKm(spec.intervalKm));
  if (spec.intervalMonths != null) parts.push(`${spec.intervalMonths} mo`);
  return parts.length ? parts.join(" / ") : "—";
}

/**
 * Shop-time formatter: "45 min", "2 h", "2 h 30 min"; totals read naturally
 * too ("6 h 05 min" — minutes zero-padded whenever hours are present).
 */
export function formatShopMinutes(minutes: number): string {
  if (minutes < 60) return `${minutes} min`;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (m === 0) return `${h} h`;
  return `${h} h ${String(m).padStart(2, "0")} min`;
}
