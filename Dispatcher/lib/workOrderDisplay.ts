import type { StatusKind } from "./theme";
import type {
  DefectRepairOutcomeWire,
  DefectResolutionReasonWire,
  DefectSeverityWire,
  InspectionDefectWire,
  InspectionResultWire,
  ServiceCategoryWire,
  ShopWire,
  WorkOrderPriorityWire,
  WorkOrderSourceWire,
  WorkOrderStatusWire,
  WorkOrderWire,
} from "./api/maintenance";
import type { ServiceCategory, Shop, WorkOrder } from "./types";
import { formatUtcDate } from "./api/format";

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

/** Worst first — the order the server already returns defect rows in. Used to
 *  PICK the worst severity present, never to re-sort a list. */
export const DEFECT_SEVERITY_RANK: Record<DefectSeverityWire, number> = { OutOfService: 0, Major: 1, Minor: 2 };

/** A chip's worth of status: colour kind + optional glyph override + words. */
export interface DefectChip {
  kind: StatusKind;
  glyph?: string;
  label: string;
}

/**
 * The LIVE defect state of one inspection, beside its frozen result chip.
 * "Fail" / "Pass with defects" is the compliance record of what the driver
 * found and never changes; this says whether those defects are still open.
 *
 *  - null when the inspection reported no defects (nothing to say);
 *  - "N open defect(s)" in the WORST open severity's colour + glyph — a defect
 *    on an open work order is still open until that work order completes;
 *  - teal "Defects resolved" once every reported defect is cleared.
 */
export function inspectionDefectStatus(insp: { defects: readonly InspectionDefectWire[] }): DefectChip | null {
  if (insp.defects.length === 0) return null;
  const open = insp.defects.filter((d) => d.resolvedAtUtc == null);
  if (open.length === 0) return { kind: "ontime", label: "Defects resolved" };
  const worst = open.reduce(
    (w, d) => ((DEFECT_SEVERITY_RANK[d.severity] ?? 1) < (DEFECT_SEVERITY_RANK[w] ?? 1) ? d.severity : w),
    open[0].severity,
  );
  const meta = DEFECT_SEVERITY_META[worst] ?? DEFECT_SEVERITY_META.Major;
  return {
    kind: meta.kind,
    glyph: meta.glyph,
    label: `${open.length} open defect${open.length === 1 ? "" : "s"}`,
  };
}

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

/** Why a defect stopped being an open fault. RepairedUnderWorkOrder and
 *  NoFaultFound are stamped by work-order completion, never chosen by hand —
 *  they are here because they come back on resolved rows. */
export const DEFECT_RESOLUTION_LABEL: Record<DefectResolutionReasonWire, string> = {
  RepairedUnderWorkOrder: "Repaired under work order",
  PreviouslyRepaired: "Previously repaired",
  ReportedInError: "Reported in error",
  AcceptedMonitoring: "Accepted, monitoring",
  NoFaultFound: "No fault found under work order",
};

/** Every resolution is a cleared defect, so every reason is teal (the label
 *  says which). NoFaultFound takes the same ○ as its work-order outcome, so the
 *  defect row and the work-order card read alike. */
export const DEFECT_RESOLUTION_META: Record<DefectResolutionReasonWire, { kind: StatusKind; glyph?: string }> = {
  RepairedUnderWorkOrder: { kind: "ontime" },
  PreviouslyRepaired: { kind: "ontime" },
  ReportedInError: { kind: "ontime" },
  AcceptedMonitoring: { kind: "ontime" },
  NoFaultFound: { kind: "ontime", glyph: "○" },
};

/** The chip for a resolved inspection defect: teal, the reason's glyph, and
 *  "Resolved · <reason>" — plain "Resolved" when no reason came back. An
 *  unmapped reason prints as-is rather than vanishing. */
export function resolvedDefectChip(reason: DefectResolutionReasonWire | string | null): DefectChip {
  if (!reason) return { kind: "ontime", label: "Resolved" };
  const meta = DEFECT_RESOLUTION_META[reason as DefectResolutionReasonWire];
  return {
    kind: meta?.kind ?? "ontime",
    glyph: meta?.glyph,
    label: `Resolved · ${DEFECT_RESOLUTION_LABEL[reason as DefectResolutionReasonWire] ?? reason}`,
  };
}

/** The report a duplicate was cleared with, as far as the caller's already-loaded
 *  data can say — never fetched per row. */
export interface LinkedReport {
  tripNumber: string | null;
  /** When that report was made (the inspection's performedAt / reportedAt). */
  at: string;
}

/**
 * The words for a defect cleared AS A DUPLICATE (`resolvedWithInspectionId` set):
 * a known fault re-reported on several inspections is cleared by resolving any one
 * of them. Says whether the resolved report was earlier or later than this one and
 * names its trip when the caller can find it; otherwise says only that it went
 * with another report of the same fault. The caller prefixes "· ".
 */
export function clearedWithText(linked: LinkedReport | null | undefined, ownAt: string | null | undefined): string {
  if (!linked) return "cleared with another report of this fault";
  const theirs = Date.parse(linked.at);
  const mine = ownAt ? Date.parse(ownAt) : NaN;
  const when = theirs < mine ? "earlier" : theirs > mine ? "later" : null;
  if (linked.tripNumber) return `cleared with ${when ? `the ${when}` : "the"} report on ${linked.tripNumber}`;
  const article = when === "earlier" ? "an earlier" : when === "later" ? "a later" : "another";
  return `cleared with ${article} report (${formatUtcDate(linked.at)})`;
}

/** What the mechanic found for one defect line, as the dispatcher reads it. */
export const DEFECT_OUTCOME_LABEL: Record<DefectRepairOutcomeWire, string> = {
  Repaired: "Repaired",
  NoFaultFound: "No fault found",
  Deferred: "Deferred",
};

/** Outcome → chip colour + glyph. Repaired and No fault found both clear the
 *  defect (teal) and are told apart by glyph — ✓ against ○ (U+25CB, "nothing
 *  there"). Deferred leaves the defect open: gold ◐ (the caution default). */
export const DEFECT_OUTCOME_META: Record<DefectRepairOutcomeWire, { kind: StatusKind; glyph: string }> = {
  Repaired: { kind: "ontime", glyph: "✓" },
  NoFaultFound: { kind: "ontime", glyph: "○" },
  Deferred: { kind: "soon", glyph: "◐" },
};

/** Plain-language consequence of each outcome, shown beside the select. */
export const DEFECT_OUTCOME_HELP: Record<DefectRepairOutcomeWire, string> = {
  Repaired: "Clears the defect permanently.",
  NoFaultFound: "Clears the defect permanently — inspected, nothing wrong.",
  Deferred: "Leaves the defect open for a later work order. Needs a note.",
};

export const DEFECT_OUTCOMES: DefectRepairOutcomeWire[] = ["Repaired", "NoFaultFound", "Deferred"];

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
    defectLines: (w.defects ?? []).map((d) => ({
      item: d.item,
      severity: DEFECT_SEVERITY_LABEL[d.severity] ?? d.severity,
      note: d.note ?? undefined,
    })),
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
