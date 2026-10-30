import type {
  DefectOutcomeInputWire,
  DefectRefWire,
  DefectRepairOutcomeWire,
  DefectSeverityWire,
  VehicleDefectWire,
} from "./api/maintenance";

// Pure rules for completing a work order that carries defect lines — the same
// rules the backend enforces (WorkOrder.RecordOutcomes), checked client-side so
// the dispatcher gets a message beside the row instead of a 400 after submit.
// The backend stays the authority.

/** A defect line as the completion form needs it. */
export interface DefectLineForCompletion extends DefectRefWire {
  severity: DefectSeverityWire;
}

/** One row of the form. `outcome` is null until the dispatcher picks one —
 *  there is deliberately NO default, so nothing is cleared by not looking. */
export interface DefectOutcomeDraft extends DefectRefWire {
  outcome: DefectRepairOutcomeWire | null;
  note: string;
}

export interface DefectOutcomeIssue extends DefectRefWire {
  message: string;
}

export type DefectOutcomeValidation =
  | { ok: true; defectOutcomes: DefectOutcomeInputWire[] }
  | { ok: false; issues: DefectOutcomeIssue[] };

/** Same addressing as the backend: the inspection id, and the item compared
 *  trimmed and case-insensitively. */
export function defectKeyOf(d: DefectRefWire): string {
  return `${d.inspectionId}:${d.item.trim().toLowerCase()}`;
}

/** Deferred is never offered for an out-of-service defect: a truck with an
 *  OOS fault may not keep running with it. */
export function outcomesFor(severity: DefectSeverityWire): DefectRepairOutcomeWire[] {
  return severity === "OutOfService" ? ["Repaired", "NoFaultFound"] : ["Repaired", "NoFaultFound", "Deferred"];
}

/**
 * The severity the Deferred rule is judged on: the defect's CURRENT severity on
 * its inspection (an amended inspection can move it either way), looked up in
 * the vehicle's defect list by the same addressing as the backend — falling
 * back to the severity snapshotted on the work-order line when the defect is
 * not in the list (gone, or the list could not be read). Same rule as the
 * backend's WorkOrder completion.
 */
export function currentSeverityOf(
  line: DefectLineForCompletion,
  currentDefects: readonly VehicleDefectWire[] | null | undefined,
): DefectSeverityWire {
  if (!currentDefects) return line.severity;
  const key = defectKeyOf(line);
  return currentDefects.find((d) => defectKeyOf(d) === key)?.severity ?? line.severity;
}

/**
 * Every line answered exactly once, a note on every Deferred, and no Deferred on
 * an out-of-service line — judged on the defect's current severity when
 * `currentDefects` knows it, else the line's snapshot (see currentSeverityOf).
 * On success returns the `defectOutcomes` body (notes trimmed, blank → null), in
 * line order. A work order with no lines validates to an empty list — the
 * caller then omits the field.
 */
export function validateDefectOutcomes(
  lines: DefectLineForCompletion[],
  outcomes: DefectOutcomeDraft[],
  currentDefects?: readonly VehicleDefectWire[] | null,
): DefectOutcomeValidation {
  const byKey = new Map<string, DefectOutcomeDraft>();
  const issues: DefectOutcomeIssue[] = [];
  const lineKeys = new Set(lines.map(defectKeyOf));

  for (const o of outcomes) {
    const key = defectKeyOf(o);
    if (!lineKeys.has(key)) {
      issues.push({ inspectionId: o.inspectionId, item: o.item, message: "This defect is not on the work order." });
    } else if (byKey.has(key)) {
      issues.push({ inspectionId: o.inspectionId, item: o.item, message: "This defect has more than one outcome." });
    } else {
      byKey.set(key, o);
    }
  }

  const defectOutcomes: DefectOutcomeInputWire[] = [];
  for (const line of lines) {
    const draft = byKey.get(defectKeyOf(line));
    const ref = { inspectionId: line.inspectionId, item: line.item };
    if (!draft || draft.outcome === null) {
      issues.push({ ...ref, message: "Choose an outcome." });
      continue;
    }
    const note = draft.note.trim();
    if (draft.outcome === "Deferred") {
      if (currentSeverityOf(line, currentDefects) === "OutOfService") {
        issues.push({ ...ref, message: "An out-of-service defect cannot be deferred." });
        continue;
      }
      if (!note) {
        issues.push({ ...ref, message: "Say why it was deferred." });
        continue;
      }
    }
    defectOutcomes.push({ ...ref, outcome: draft.outcome, note: note || null });
  }

  return issues.length ? { ok: false, issues } : { ok: true, defectOutcomes };
}

/** An open defect that keeps a truck off the road: Major or Out-of-Service.
 *  A Minor defect never holds back a return to service. */
export function isBlockingSeverity(severity: DefectSeverityWire): boolean {
  return severity === "Major" || severity === "OutOfService";
}

/**
 * The vehicle's Major / Out-of-Service defects still open AFTER this completion.
 * `openRows` is the vehicle's open-defect list; the defects this completion just
 * cleared (Repaired / NoFaultFound) are subtracted explicitly because the defect
 * read model lags a few seconds behind the write. A Deferred line stays open,
 * so it still counts. Empty means a "Return to service" OFFER may be shown —
 * never an automatic status change.
 */
export function remainingBlockingDefects(
  openRows: VehicleDefectWire[],
  completed: DefectOutcomeInputWire[],
): VehicleDefectWire[] {
  const cleared = new Set(completed.filter((o) => o.outcome !== "Deferred").map(defectKeyOf));
  return openRows.filter(
    (r) => r.resolvedAtUtc === null && isBlockingSeverity(r.severity) && !cleared.has(defectKeyOf(r)),
  );
}
