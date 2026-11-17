import { normaliseDefectItem, type DefectSeverityWire, type VehicleDefectWire } from "./api/maintenance";
import { DEFECT_SEVERITY_RANK } from "./workOrderDisplay";

// A known fault is re-reported on every daily pre-trip until it is fixed, so one
// component can carry several OPEN defects on several inspections. They are
// duplicates of each other — same vehicle, same `item` trimmed and
// case-insensitive (`normaliseDefectItem`, the backend's rule) — and a REPAIR of
// any one of them clears them all. ("Reported in error" and "Accepted,
// monitoring" clear only the report they address.)
//
// What a resolve will actually clear is NOT derived here: ResolveDefectModal asks
// the server (`previewDefectDuplicates`), which reads the write model with the
// cascade's own scope. The derivations below are over `listVehicleDefects` rows
// (the projection, ≈5s behind) and feed the inspection forms' known-fault hints,
// which are informational only.
//
// WARN ONLY. Nothing here decides anything — it says what is already open so a
// dispatcher reads a row as "known fault" rather than "new fault". Never filter
// a list by it, and never sort with it: rows stay in the server's safety order.

const isOpen = (d: VehicleDefectWire) => d.resolvedAtUtc == null;

/**
 * The worst severity among `duplicates` that is strictly MORE severe than the
 * defect being resolved, or null when none is. With the addressed severity
 * unknown, an Out-of-Service duplicate still counts — a repair must never clear
 * an Out-of-Service report without saying so.
 */
export function moreSevereDuplicate(
  duplicates: readonly { severity: DefectSeverityWire }[],
  addressed: DefectSeverityWire | null | undefined,
): DefectSeverityWire | null {
  const bar = addressed ? DEFECT_SEVERITY_RANK[addressed] : DEFECT_SEVERITY_RANK.OutOfService + 1;
  let worst: DefectSeverityWire | null = null;
  for (const d of duplicates) {
    const rank = DEFECT_SEVERITY_RANK[d.severity];
    if (rank === undefined || rank >= bar) continue;
    if (worst === null || rank < DEFECT_SEVERITY_RANK[worst]) worst = d.severity;
  }
  return worst;
}

/** What an inspection form says about one checklist component that already has
 *  open reports on the unit. */
export interface KnownDefect {
  /** The worst open report — the first in the server's order, which is severity
   *  first. Its severity is the hint's chip. */
  worst: VehicleDefectWire;
  /** The EARLIEST open report — "open since" its date and trip. */
  first: VehicleDefectWire;
  /** How many open reports there are of this component (≥ 1). */
  count: number;
}

/**
 * The unit's open defects keyed by normalised ITEM KEY — the wire key a form row
 * carries (`ChecklistRow.itemKey`), never its label, because key ≠ label for some
 * NL-PTI-01 rows. Look a row up with `knownDefectFor`.
 *
 * `excludeInspectionId` is the inspection being EDITED: its own defects are what
 * the form is showing, so counting them would flag every row it marked as a known
 * fault of itself.
 */
export function knownDefectsByItem(
  rows: readonly VehicleDefectWire[],
  excludeInspectionId?: string | null,
): Map<string, KnownDefect> {
  const byItem = new Map<string, KnownDefect>();
  for (const r of openReports(rows, excludeInspectionId)) {
    const key = normaliseDefectItem(r.item);
    const known = byItem.get(key);
    if (!known) {
      byItem.set(key, { worst: r, first: r, count: 1 });
      continue;
    }
    known.count += 1;
    if (Date.parse(r.reportedAt) < Date.parse(known.first.reportedAt)) known.first = r;
  }
  return byItem;
}

/** The open reports a form counts — open, and not the edited inspection's own —
 *  in the server's order. */
export function openReports(
  rows: readonly VehicleDefectWire[],
  excludeInspectionId?: string | null,
): VehicleDefectWire[] {
  return rows.filter((r) => isOpen(r) && (!excludeInspectionId || r.inspectionId !== excludeInspectionId));
}

export function knownDefectFor(
  known: ReadonlyMap<string, KnownDefect> | null | undefined,
  itemKey: string,
): KnownDefect | undefined {
  return known?.get(normaliseDefectItem(itemKey));
}
