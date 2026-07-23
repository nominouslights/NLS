import type { HosEntryRecord } from "./api/drivers";
import type { StatusKind } from "./theme";

// Pure HOS-limit rules (no React) — the client-side CVDHS rule engine over the
// raw duty entries returned by the Drivers API (lib/api/drivers.ts). The
// backend persists and returns entries but does NOT reject over-limit days;
// these rules derive the violations and the remaining-hours gauge on read.
// Formalizes the CVDHS cycle numbers shown as static text in the Drivers.tsx
// HOS gauge ("13h drive · 14h on-duty · 10h off-duty").

export const MAX_DRIVING_HOURS_PER_DAY = 13;
export const MAX_ON_DUTY_HOURS_PER_DAY = 14;
export const MIN_OFF_DUTY_HOURS_PER_DAY = 10;

export interface HosViolation {
  date: string;
  kind: "driving" | "onDuty" | "offDuty";
  message: string;
}

export function violationsForEntry(entry: HosEntryRecord): HosViolation[] {
  const violations: HosViolation[] = [];
  if (entry.drivingH > MAX_DRIVING_HOURS_PER_DAY) {
    violations.push({
      date: entry.date,
      kind: "driving",
      message: `Driving ${entry.drivingH}h exceeds ${MAX_DRIVING_HOURS_PER_DAY}h max`,
    });
  }
  if (entry.onDutyH > MAX_ON_DUTY_HOURS_PER_DAY) {
    violations.push({
      date: entry.date,
      kind: "onDuty",
      message: `On-duty ${entry.onDutyH}h exceeds ${MAX_ON_DUTY_HOURS_PER_DAY}h max`,
    });
  }
  if (entry.offDutyH < MIN_OFF_DUTY_HOURS_PER_DAY) {
    violations.push({
      date: entry.date,
      kind: "offDuty",
      message: `Off-duty ${entry.offDutyH}h is below the ${MIN_OFF_DUTY_HOURS_PER_DAY}h minimum`,
    });
  }
  return violations;
}

/** All HOS violations across a driver's fetched duty log. */
export function hosViolationsFor(entries: HosEntryRecord[]): HosViolation[] {
  return entries.flatMap(violationsForEntry);
}

/** "HOS remaining" gauge line, derived from the hours already driven that day:
 *  remaining = 13h daily driving max − hours driven. Pass the latest entry's
 *  drivingH (detail pane) or the driver's latestDrivingHours rollup (roster
 *  row); null when there's no entry. Colour + label travel together (never
 *  colour alone). */
export function hosRemainingFor(drivingHours: number | null): { label: string; kind: StatusKind; pct: number } {
  if (drivingHours === null) return { label: "—", kind: "off", pct: 0 };
  const remaining = Math.max(0, MAX_DRIVING_HOURS_PER_DAY - drivingHours);
  const h = Math.floor(remaining);
  const m = Math.round((remaining - h) * 60);
  const label = `${h}h ${String(m).padStart(2, "0")}m`;
  const kind: StatusKind = remaining <= 0 ? "over" : remaining <= 4 ? "soon" : "ontime";
  return { label, kind, pct: Math.round((remaining / MAX_DRIVING_HOURS_PER_DAY) * 100) };
}
