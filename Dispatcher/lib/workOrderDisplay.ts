import type { StatusKind } from "./theme";
import type {
  DefectResolutionReasonWire,
  DefectSeverityWire,
  InspectionResultWire,
  ServiceCategoryWire,
  ShopWire,
  WorkOrderPriorityWire,
  WorkOrderSourceWire,
  WorkOrderStatusWire,
  WorkOrderWire,
} from "./api/maintenance";
import type { ServiceCategory, Shop, WorkOrder } from "./types";

// Wire ↔ display boundary for the live maintenance contract. Wire enums travel
// as backend enum names ("InProgress", "PreTripInspection"); everything shown
// to a dispatcher goes through the label maps here, and everything printed on
// NL-WO-01 goes through the adapters so lib/documents/workOrderPdf keeps its
// existing display-typed inputs.

export const WO_STATUS_LABEL: Record<WorkOrderStatusWire, WorkOrder["status"]> = {
  Open: "Open",
  InProgress: "In Progress",
  AwaitingParts: "Awaiting Parts",
  Completed: "Completed",
  Cancelled: "Cancelled",
};

export const WO_SOURCE_LABEL: Record<WorkOrderSourceWire, WorkOrder["source"]> = {
  Manual: "Manual",
  PreTripInspection: "Pre-Trip Inspection",
  PostTripInspection: "Post-Trip Inspection",
  DtcAlert: "DTC Alert",
  PmReminder: "PM Reminder",
};

export const DEFECT_SEVERITY_LABEL: Record<DefectSeverityWire, string> = {
  Minor: "Minor",
  Major: "Major",
  OutOfService: "Out-of-Service",
};

/** Defect severity → chip colour + glyph. The LABEL stays in
 *  DEFECT_SEVERITY_LABEL above so the chip and the printed NL-WO-01 line items
 *  can never drift. Minor is gold; Major and Out-of-Service are BOTH vermillion
 *  — they are told apart by glyph and label, never by a new colour. Minor and
 *  Major take their kind's default glyph (◐ / ▲); Out-of-Service overrides it
 *  with ✕ (U+2715, a Barlow glyph — not an emoji). */
export const DEFECT_SEVERITY_META: Record<DefectSeverityWire, { kind: StatusKind; glyph?: string }> = {
  Minor: { kind: "soon" },
  Major: { kind: "over" },
  OutOfService: { kind: "over", glyph: "✕" },
};

/** Work-order status → chip colour for a defect row's "repair underway" tag.
 *  Status-only sibling of workOrderKindWire: VehicleDefectWire carries the
 *  status string but no priority. Anything still in the shop reads as caution —
 *  the defect is not cleared until the work order is completed. */
export const DEFECT_WO_KIND: Record<WorkOrderStatusWire, StatusKind> = {
  Open: "soon",
  InProgress: "soon",
  AwaitingParts: "soon",
  Completed: "ontime",
  Cancelled: "off",
};

/** Why a defect stopped being an open fault. RepairedUnderWorkOrder is stamped
 *  by work-order completion, never chosen by hand — it is here because it comes
 *  back on resolved rows. */
export const DEFECT_RESOLUTION_LABEL: Record<DefectResolutionReasonWire, string> = {
  RepairedUnderWorkOrder: "Repaired under work order",
  PreviouslyRepaired: "Previously repaired",
  ReportedInError: "Reported in error",
  AcceptedMonitoring: "Accepted, monitoring",
};

export const OPEN_WIRE_STATUSES: WorkOrderStatusWire[] = ["Open", "InProgress", "AwaitingParts"];

/** Wire inspection result → chip colour + label (glyph comes from StatusChip). */
export const INSPECTION_RESULT_META: Record<InspectionResultWire, { kind: StatusKind; label: string }> = {
  Pass: { kind: "ontime", label: "Pass" },
  PassWithDefects: { kind: "soon", label: "Pass with defects" },
  Fail: { kind: "over", label: "Fail" },
};

/** Priority → chip colour. Critical/High vermillion, Medium gold, Low blue. */
export function priorityKindWire(priority: WorkOrderPriorityWire): StatusKind {
  switch (priority) {
    case "Critical":
    case "High":
      return "over";
    case "Medium":
      return "soon";
    case "Low":
    default:
      return "info";
  }
}

/** Work-order chip colour, accounting for terminal states. */
export function workOrderKindWire(wo: WorkOrderWire): StatusKind {
  if (wo.status === "Completed") return "ontime";
  if (wo.status === "Cancelled") return "off";
  return priorityKindWire(wo.priority);
}

/** Display category (select options, mock service log) → wire enum name. */
export const CATEGORY_WIRE: Record<ServiceCategory, ServiceCategoryWire> = {
  Preventive: "Preventive",
  Repair: "Repair",
  "Inspection Fix": "InspectionFix",
  Recall: "Recall",
};

/** Backend binds DateTimeOffset — a bare "YYYY-MM-DD" must travel as full UTC ISO. */
export function toUtcIso(dateStr: string): string {
  return `${dateStr}T00:00:00Z`;
}

/** Wire work order → the display-typed shape workOrderPdf prints (NL-WO-01). */
export function toPrintableWorkOrder(w: WorkOrderWire, unit: string): WorkOrder {
  return {
    id: w.number,
    unit,
    title: w.title,
    description: w.description,
    status: WO_STATUS_LABEL[w.status],
    k: workOrderKindWire(w),
    priority: w.priority,
    source: WO_SOURCE_LABEL[w.source],
    sourceRef: w.sourceRef ?? undefined,
    createdBy: w.createdBy,
    createdAt: w.createdAt.slice(0, 10),
    assignedTo: w.assignedTo ?? undefined,
    dueDate: w.dueDate?.slice(0, 10),
    lineItems: w.lineItems,
    completedAt: w.completedAt?.slice(0, 10),
    resolvingServiceId: w.resolvingServiceId ?? undefined,
    shopId: w.shopId ?? undefined,
    authorizedLimitCad: w.authorizedLimitCad ?? undefined,
    budgetCode: w.budgetCode ?? undefined,
    dateRequiredOrOos: w.dateRequiredOrOos?.slice(0, 10),
  };
}

/** Wire shop → the display-typed shape workOrderPdf prints (NL-WO-01 §2). */
export function toPrintableShop(s: ShopWire): Shop {
  return {
    id: s.id,
    name: s.name,
    contactName: s.contactName ?? undefined,
    phone: s.phone ?? undefined,
    email: s.email ?? undefined,
    address: s.address ?? undefined,
    gstBusinessNo: s.gstBusinessNo ?? undefined,
    mpiAccredited: s.mpiAccredited,
    inspectionStationNo: s.inspectionStationNo ?? undefined,
    suppliesParts: s.suppliesParts,
    notes: s.notes ?? undefined,
  };
}
