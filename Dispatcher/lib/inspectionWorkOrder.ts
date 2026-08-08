import type {
  InspectionDefectWire,
  VehicleInspection,
  WorkOrderPriorityWire,
  WorkOrderSourceWire,
} from "./api/maintenance";
import { DEFECT_SEVERITY_LABEL } from "./workOrderDisplay";

// Build a work-order prefill from a live inspection's defects — used both when
// generating a work order from all defects and from a single failed item.

export interface WorkOrderPrefillWire {
  source: WorkOrderSourceWire;
  sourceRef: string | null; // trip number on the printed NL-WO-01 — never the GUID
  title: string;
  description: string;
  lineItems: string[];
  priority: WorkOrderPriorityWire;
  inspectionId: string; // links the WO back to the inspection server-side
}

export function prefillFromInspection(
  insp: VehicleInspection,
  unit: string,
  defects: InspectionDefectWire[] = insp.defects,
): WorkOrderPrefillWire {
  const priority: WorkOrderPriorityWire = defects.some((d) => d.severity === "OutOfService")
    ? "Critical"
    : defects.some((d) => d.severity === "Major")
      ? "High"
      : "Medium";
  const single = defects.length === 1;
  const typeLabel = insp.type === "PreTrip" ? "Pre-Trip" : "Post-Trip";
  return {
    source: insp.type === "PreTrip" ? "PreTripInspection" : "PostTripInspection",
    sourceRef: insp.tripNumber,
    title: single ? `${defects[0].item} — ${unit}` : `${typeLabel} defects — ${unit}`,
    description: `Defect(s) recorded on the ${typeLabel} DVIR for ${unit}${insp.tripNumber ? ` (trip ${insp.tripNumber})` : ""}.`,
    lineItems: defects.map(
      (d) => `${d.item} — ${DEFECT_SEVERITY_LABEL[d.severity]}${d.note ? `: ${d.note}` : ""}`,
    ),
    priority,
    inspectionId: insp.id,
  };
}
