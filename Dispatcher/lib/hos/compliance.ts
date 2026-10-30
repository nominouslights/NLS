import type { StatusKind } from "../theme";
import type { FindingSeverity, FindingStatus, HosAlertKind, HosAlertsToday } from "../api/hos";
import { HOS_RULES } from "./engine";
import { HOS_CALENDAR, type ChipSpec } from "./display";

// ---------------------------------------------------------------------------
// The Compliance screen's and the Dispatch Board tile's display rules, plus the per-driver
// fan-out both registers rely on. Every kind travels with a glyph and a label — colour never
// stands alone (architecture non-negotiable; StatusChip renders all three).
//
// KNOWN COST — the fan-out. The Drivers module serves compliance records per driver only
// (#124), so a cross-driver register is roster × one request. Concurrency is capped so a
// 40-driver roster is ~7 waves rather than 40 sockets at once, but it is still N requests per
// tab. The follow-up endpoints that retire it (backend contract, not yet published):
//   GET /api/compliance/findings          — every driver's findings, filterable by status
//   GET /api/compliance/out-of-service    — active + historical declarations across drivers
//   GET /api/compliance/eld-malfunctions  — open (and recently resolved) reports across drivers
//   GET /api/compliance/deferrals         — in-force deferrals + Declared exceptions across drivers
//   GET /api/compliance/audit?from&to     — the driver × day audit grid in one response
// ---------------------------------------------------------------------------

/** Concurrent requests per fan-out. */
export const FAN_OUT_CONCURRENCY = 6;

export type FanOutResult<T, R> = { item: T; ok: true; value: R } | { item: T; ok: false; error: string };

/**
 * Runs `fn` over `items` with at most `limit` in flight, keeping input order. A failure is
 * captured per item rather than failing the whole batch — one driver's 404 must not blank the
 * other thirty-nine rows.
 */
export async function fanOut<T, R>(
  items: readonly T[],
  fn: (item: T) => Promise<R>,
  limit: number = FAN_OUT_CONCURRENCY,
): Promise<FanOutResult<T, R>[]> {
  const results: FanOutResult<T, R>[] = new Array(items.length);
  let next = 0;
  async function worker() {
    while (next < items.length) {
      const i = next++;
      const item = items[i];
      try {
        results[i] = { item, ok: true, value: await fn(item) };
      } catch (e) {
        results[i] = { item, ok: false, error: e instanceof Error ? e.message : String(e) };
      }
    }
  }
  await Promise.all(Array.from({ length: Math.max(1, Math.min(limit, items.length)) }, worker));
  return results;
}

/** Today's date ("yyyy-MM-dd") on the regulation's calendar (America/Winnipeg). */
export function hosToday(nowMs: number = Date.now()): string {
  return HOS_CALENDAR.dayOf(Math.floor(nowMs / 1000));
}

/** The planning-figures disclaimer every compliance surface carries. */
export const PLANNING_LINE = "Planning figures from the app ledger — not the record of duty status (the ELD is the RODS).";

// ---- Today alerts ------------------------------------------------------------------

export const ALERT_KIND_CHIP: Record<HosAlertKind, ChipSpec> = {
  OutOfService: { kind: "over", glyph: "✕", label: "Out of service" },
  OverLimit: { kind: "over", glyph: "▲", label: "Over limit" },
  NearLimit: { kind: "soon", glyph: "◐", label: "Near limit" },
  EldMalfunction: { kind: "soon", glyph: "✎", label: "ELD malfunction" },
  ExceptionDeclared: { kind: "soon", glyph: "⚑", label: "Exception to acknowledge" },
  Uncertified: { kind: "soon", glyph: "◷", label: "Uncertified" },
};

export function alertKindChip(kind: string): ChipSpec {
  return ALERT_KIND_CHIP[kind as HosAlertKind] ?? { kind: "info", glyph: "●", label: kind };
}

export interface AlertsTileSpec {
  total: number;
  kind: StatusKind;
  glyph: string;
  /** The breakdown sub-line — every non-zero count, worst first; "All clear" when none. */
  breakdown: string;
}

/**
 * The Dispatch Board tile: the six counts summed. Vermillion when anyone is over a limit or out
 * of service (they must not drive), gold when anything else needs a look, teal "0" otherwise.
 */
export function alertsTileSpec(a: HosAlertsToday): AlertsTileSpec {
  const total =
    a.driversOverLimit + a.driversNearLimit + a.outOfService + a.openMalfunctions + a.uncertifiedYesterday + a.unassignedExceptions;
  const parts: [number, string][] = [
    [a.outOfService, "out of service"],
    [a.driversOverLimit, "over limit"],
    [a.driversNearLimit, "near limit"],
    [a.openMalfunctions, a.openMalfunctions === 1 ? "ELD malfunction" : "ELD malfunctions"],
    [a.uncertifiedYesterday, "uncertified"],
    [a.unassignedExceptions, a.unassignedExceptions === 1 ? "exception" : "exceptions"],
  ];
  const breakdown = parts
    .filter(([n]) => n > 0)
    .map(([n, text]) => `${n} ${text}`)
    .join(" · ");
  if (a.driversOverLimit + a.outOfService > 0) return { total, kind: "over", glyph: "▲", breakdown };
  if (total > 0) return { total, kind: "soon", glyph: "◐", breakdown };
  return { total: 0, kind: "ontime", glyph: "✓", breakdown: "All clear" };
}

// ---- Findings ------------------------------------------------------------------------

export const FINDING_STATUS_CHIP: Record<FindingStatus, ChipSpec> = {
  Open: { kind: "soon", glyph: "◐", label: "Open" },
  Corrected: { kind: "info", glyph: "●", label: "Corrected" },
  Closed: { kind: "off", glyph: "✓", label: "Closed" },
};

export const SEVERITY_CHIP: Record<FindingSeverity, ChipSpec> = {
  Minor: { kind: "info", glyph: "●", label: "Minor" },
  Major: { kind: "soon", glyph: "◐", label: "Major" },
  Severe: { kind: "over", glyph: "▲", label: "Severe" },
};

export const FINDING_SEVERITIES: readonly FindingSeverity[] = ["Minor", "Major", "Severe"];

/** The forward moves the server allows: Open → Corrected → Closed, or Open → Closed; never backwards. */
export function nextFindingStatuses(status: FindingStatus): FindingStatus[] {
  if (status === "Open") return ["Corrected", "Closed"];
  if (status === "Corrected") return ["Closed"];
  return [];
}

/** The rule names a finding may carry besides the engine's HosRule list (ComplianceFindingResponse). */
export const NON_ENGINE_FINDING_RULES = ["Falsification", "Tampering", "TwoRecords", "OutOfServiceDeclared"] as const;

export const FINDING_RULES: readonly string[] = [...HOS_RULES, ...NON_ENGINE_FINDING_RULES];

// ---- ELD malfunctions --------------------------------------------------------------

/** Whole days between the malfunction's local date and today — "days on paper". Never negative. */
export function daysOnPaper(noticedAtUtc: string, today: string): number {
  const ms = Date.parse(noticedAtUtc);
  if (Number.isNaN(ms)) return 0;
  const noticed = HOS_CALENDAR.dayOf(Math.floor(ms / 1000));
  const a = Date.parse(`${noticed}T00:00:00Z`);
  const b = Date.parse(`${today}T00:00:00Z`);
  return Math.max(0, Math.round((b - a) / 86400000));
}

// ---- Month grid -----------------------------------------------------------------------

/** Every "yyyy-MM-dd" from `start` to `end` inclusive (UTC arithmetic, so no day slides). */
export function datesBetween(start: string, end: string): string[] {
  const out: string[] = [];
  for (let t = Date.parse(`${start}T00:00:00Z`), stop = Date.parse(`${end}T00:00:00Z`); t <= stop; t += 86400000) {
    out.push(new Date(t).toISOString().slice(0, 10));
  }
  return out;
}
