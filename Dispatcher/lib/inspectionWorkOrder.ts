import type {
  DefectRefWire,
  DefectSeverityWire,
  InspectionDefectWire,
  InspectionType,
  VehicleDefectWire,
  VehicleInspection,
  WorkOrderPriorityWire,
  WorkOrderSourceWire,
} from "./api/maintenance";
import { DEFECT_SEVERITY_LABEL } from "./workOrderDisplay";

// Build a work-order prefill from defects — from one live inspection (all its
// open defects, or one failed item) or from a vehicle's defect rows, which may
// span several inspections. The work order is linked to each defect by
// (inspectionId, item): a defect has no id of its own.

/** One defect the new work order will be raised against. Severity and note
 *  travel with the reference only so the modal can show what is attached; the
 *  wire carries `{ inspectionId, item }` alone (see `defectRefs`). */
export interface PrefillDefect extends DefectRefWire {
  severity: DefectSeverityWire;
  note: string | null;
}

export interface WorkOrderPrefillWire {
  source: WorkOrderSourceWire;
  sourceRef: string | null; // trip number(s) on the printed NL-WO-01 — never a GUID
  title: string;
  description: string;
  /** Still built from the defects for the paper form's free-text tasks. */
  lineItems: string[];
  priority: WorkOrderPriorityWire;
  /** The defects this work order is raised against — never empty on an
   *  inspection prefill. Sent as `defects` on POST /api/fleet/work-orders. */
  defects: PrefillDefect[];
}

/** The backend's SourceRef column is varchar(64). */
const SOURCE_REF_MAX = 64;

/** A defect can go on a new work order only while it is open and not already
 *  on an active one (a defect is on at most one open work order). */
export function isAttachable(d: Pick<InspectionDefectWire, "resolvedAtUtc" | "workOrderId">): boolean {
  return d.resolvedAtUtc == null && d.workOrderId == null;
}

/** Whether a work order still holds the defects it was raised against: any
 *  work order that is not Completed/Cancelled. An UNKNOWN status (null or
 *  undefined — e.g. the work-order list failed to load) counts as holding:
 *  offering a second work order for a defect that is in fact still on one lets
 *  the server move it, and the original work order's completion then skips it. */
export function workOrderHoldsDefect(workOrderId: string | null | undefined, status: string | null | undefined): boolean {
  if (workOrderId == null) return false;
  return status !== "Completed" && status !== "Cancelled";
}

/** The same rule for a vehicle-defect row, whose `workOrderId` may also name a
 *  completed/cancelled (legacy) work order — only an OPEN one blocks. */
export function isVehicleDefectAttachable(d: VehicleDefectWire): boolean {
  if (d.resolvedAtUtc !== null) return false;
  return !workOrderHoldsDefect(d.workOrderId, d.workOrderStatus);
}

/**
 * The work order an inspection's still-open defect is on, or null — the
 * inspection-side twin of the `workOrderId` a vehicle-defect row carries, so the
 * inspection detail and the Open Defects tab agree. The defect's own per-defect
 * link wins (it is released on cancel and resolved on completion, so a set one is
 * open). Without one, a work order from before per-defect links — the
 * inspection's `generatedWorkOrderId` — holds every defect of the inspection
 * while it is not Completed/Cancelled (`workOrderHoldsDefect`).
 */
export function inspectionDefectWorkOrderId(
  d: Pick<InspectionDefectWire, "resolvedAtUtc" | "workOrderId">,
  generatedWorkOrderId: string | null,
  workOrderStatusOf?: (id: string) => string | null | undefined,
): string | null {
  if (d.resolvedAtUtc != null) return null;
  if (d.workOrderId != null) return d.workOrderId;
  return workOrderHoldsDefect(generatedWorkOrderId, generatedWorkOrderId ? workOrderStatusOf?.(generatedWorkOrderId) : null)
    ? generatedWorkOrderId
    : null;
}

/** `isAttachable` with the legacy inspection-level work order taken into account. */
export function isInspectionDefectAttachable(
  d: Pick<InspectionDefectWire, "resolvedAtUtc" | "workOrderId">,
  generatedWorkOrderId: string | null,
  workOrderStatusOf?: (id: string) => string | null | undefined,
): boolean {
  return d.resolvedAtUtc == null && inspectionDefectWorkOrderId(d, generatedWorkOrderId, workOrderStatusOf) === null;
}

/** Worst severity present → work-order priority. */
export function priorityForSeverities(severities: DefectSeverityWire[]): WorkOrderPriorityWire {
  if (severities.includes("OutOfService")) return "Critical";
  if (severities.includes("Major")) return "High";
  return "Medium";
}

/** The printed/paper line for one defect: "Item — Severity: note". The work
 *  order PDF prints defect lines in exactly this form, which is what lets it
 *  drop a free-text line item that merely repeats one. */
export function defectLineText(d: { item: string; severity: DefectSeverityWire; note: string | null }): string {
  return `${d.item} — ${DEFECT_SEVERITY_LABEL[d.severity]}${d.note ? `: ${d.note}` : ""}`;
}

/** Wire body for `defects` on POST /api/fleet/work-orders. */
export function defectRefs(defects: DefectRefWire[]): DefectRefWire[] {
  return defects.map((d) => ({ inspectionId: d.inspectionId, item: d.item }));
}

const sourceFor = (type: InspectionType): WorkOrderSourceWire =>
  type === "PreTrip" ? "PreTripInspection" : "PostTripInspection";
const typeLabelFor = (type: InspectionType) => (type === "PreTrip" ? "Pre-Trip" : "Post-Trip");

export function prefillFromInspection(
  insp: VehicleInspection,
  unit: string,
  defects: InspectionDefectWire[] = insp.defects.filter(isAttachable),
): WorkOrderPrefillWire {
  const single = defects.length === 1;
  const typeLabel = typeLabelFor(insp.type);
  return {
    source: sourceFor(insp.type),
    sourceRef: insp.tripNumber,
    title: single ? `${defects[0].item} — ${unit}` : `${typeLabel} defects — ${unit}`,
    description: `Defect(s) recorded on the ${typeLabel} DVIR for ${unit}${insp.tripNumber ? ` (trip ${insp.tripNumber})` : ""}.`,
    lineItems: defects.map(defectLineText),
    priority: priorityForSeverities(defects.map((d) => d.severity)),
    defects: defects.map((d) => ({ inspectionId: insp.id, item: d.item, severity: d.severity, note: d.note })),
  };
}

/**
 * Prefill from a vehicle's defect rows (the Open Defects tab). The rows may come
 * from several inspections; the server checks every one is open, unattached and
 * on this vehicle. Rows keep the order given — the panel's rows arrive worst
 * severity first, so the first row decides the source when DVIR types are mixed.
 */
export function prefillFromDefects(rows: VehicleDefectWire[], unit: string): WorkOrderPrefillWire {
  if (rows.length === 0) throw new Error("prefillFromDefects needs at least one defect.");
  const types = new Set(rows.map((r) => r.inspectionType));
  const inspections = new Set(rows.map((r) => r.inspectionId));
  const trips = [...new Set(rows.map((r) => r.tripNumber).filter((t): t is string => !!t))];
  const single = rows.length === 1;
  const typeLabel = types.size === 1 ? typeLabelFor(rows[0].inspectionType) : "Inspection";

  const joined = trips.join(", ");
  const sourceRef = trips.length === 0 ? null : joined.length <= SOURCE_REF_MAX ? joined : trips[0];

  const where =
    inspections.size === 1
      ? `the ${typeLabel} DVIR`
      : `${inspections.size} DVIRs`;
  const tripText = trips.length === 0 ? "" : trips.length === 1 ? ` (trip ${trips[0]})` : ` (trips ${joined})`;

  return {
    source: sourceFor(rows[0].inspectionType),
    sourceRef,
    title: single ? `${rows[0].item} — ${unit}` : `${typeLabel} defects — ${unit}`,
    description: `Defect(s) recorded on ${where} for ${unit}${tripText}.`,
    lineItems: rows.map(defectLineText),
    priority: priorityForSeverities(rows.map((r) => r.severity)),
    defects: rows.map((r) => ({ inspectionId: r.inspectionId, item: r.item, severity: r.severity, note: r.note })),
  };
}
