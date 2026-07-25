"use client";

import { useSyncExternalStore } from "react";
import type { StatusKind } from "./theme";
import type {
  Inspection,
  InspectionChecklistItem,
  InspectionDefect,
  ServiceRecord,
  Shop,
  VehicleDocument,
  WorkOrder,
  WorkOrderPriority,
} from "./types";

// Standard DVIR checklist (NSC Standard 11 / trip inspection). Single source of
// truth shared by the entry modal, the detail view, and seed backfill.
export const DVIR_CHECKLIST = [
  "Service brakes",
  "Parking brake",
  "Steering",
  "Lights & reflectors",
  "Tires & wheels",
  "Windshield & wipers",
  "Mirrors",
  "Horn",
  "Coupling / hitch",
  "Emergency equipment",
  "Seats & seatbelts",
  "Exhaust / fluid leaks",
] as const;

// -----------------------------------------------------------------------------
// Fleet & Maintenance prototype store — MOCK ONLY (no backend domain yet).
//
// Vehicle registration is real (lib/api.ts). Everything here — compliance
// documents, service history, work orders, and DVIR inspections — is an
// in-memory prototype. A module-level store (not per-component state) so edits
// survive navigating away from and back to the merged Fleet & Maintenance
// screen, which Console.tsx unmounts on nav. Keyed by `unit` (unitNumber),
// matching the existing pmReminders / dtcAlerts convention in lib/data.ts.
// -----------------------------------------------------------------------------

// ---- status derivation helpers ---------------------------------------------

const EXPIRY_SOON_DAYS = 60;

/** Compliance-document status from its expiry date. No expiry → always valid. */
export function docStatusFor(expiry: string | null, now: Date = new Date()): StatusKind {
  if (!expiry) return "ontime";
  const days = (new Date(expiry).getTime() - now.getTime()) / 86_400_000;
  if (days < 0) return "over";
  if (days <= EXPIRY_SOON_DAYS) return "soon";
  return "ontime";
}

/** Priority → chip colour. Critical/High vermillion, Medium gold, Low blue. */
export function priorityKind(priority: WorkOrderPriority): StatusKind {
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
export function workOrderKind(wo: WorkOrder): StatusKind {
  if (wo.status === "Completed") return "ontime";
  if (wo.status === "Cancelled") return "off";
  return priorityKind(wo.priority);
}

/** DVIR result → chip colour. */
export function inspectionKind(result: Inspection["result"]): StatusKind {
  if (result === "Pass") return "ontime";
  if (result === "Fail") return "over";
  return "soon";
}

/** DVIR result derived from its worst defect. */
export function resultForDefects(defects: Inspection["defects"]): Inspection["result"] {
  if (defects.some((d) => d.severity === "Out-of-Service" || d.severity === "Major")) return "Fail";
  if (defects.length > 0) return "Pass with defects";
  return "Pass";
}

/** Full pass/fail checklist from a defect list: every standard item passes
 *  except those with a recorded defect; non-standard defect items are appended. */
export function buildChecklist(defects: InspectionDefect[]): InspectionChecklistItem[] {
  const byItem = new Map(defects.map((d) => [d.item, d]));
  const standard: InspectionChecklistItem[] = DVIR_CHECKLIST.map((item) => {
    const d = byItem.get(item);
    return d
      ? { item, status: "Defect", severity: d.severity, note: d.note }
      : { item, status: "Pass" };
  });
  const extra: InspectionChecklistItem[] = defects
    .filter((d) => !DVIR_CHECKLIST.includes(d.item as (typeof DVIR_CHECKLIST)[number]))
    .map((d) => ({ item: d.item, status: "Defect", severity: d.severity, note: d.note }));
  return [...standard, ...extra];
}

// ---- id counters ------------------------------------------------------------
// Declared before the seed arrays: the seed builders below call nextId() during
// module init, so `counters` must already be initialized (no TDZ).

const counters: Record<string, number> = { doc: 5000, svc: 4200, wo: 1040, dvir: 2210, shop: 3 };
function nextId(kind: keyof typeof counters): number {
  counters[kind] += 1;
  return counters[kind];
}

// ---- seed data --------------------------------------------------------------

export const documents: VehicleDocument[] = [
  mkDoc("U-01", "Registration", "U-01_registration_2027.pdf", 214, "MB Public Insurance", "2026-01-12", "2027-01-31"),
  mkDoc("U-01", "Insurance / MPI", "U-01_MPI_cert.pdf", 188, "MB Public Insurance", "2026-02-01", "2026-08-20"),
  mkDoc("U-01", "NSC Safety Certificate", "U-01_NSC11_safety.pdf", 402, "Thompson Certified Shop", "2025-12-15", "2026-06-30"),
  mkDoc("U-02", "Registration", "U-02_registration.pdf", 205, "MB Public Insurance", "2026-03-04", "2027-03-31"),
  mkDoc("U-02", "Insurance / MPI", "U-02_MPI_cert.pdf", 191, "MB Public Insurance", "2026-03-04", "2026-11-15"),
  mkDoc("U-03", "Registration", "U-03_registration.pdf", 210, "MB Public Insurance", "2026-05-20", "2026-09-05"),
];

export const services: ServiceRecord[] = [
  {
    id: "SVC-4102",
    unit: "U-01",
    date: "2026-06-10",
    performedBy: "M. Cardinal · Thompson shop",
    category: "Preventive",
    odometerKm: 210400,
    itemsChanged: ["Engine oil & filter", "Air filter", "Chassis lube"],
    reason: "Scheduled 15,000 km preventive service (mileage-based PM).",
    partsUsed: [
      { sku: "FLT-OIL-15W40", qty: 1 },
      { sku: "FLT-AIRFLT", qty: 1 },
    ],
    laborHours: 2.5,
    costCad: 480,
  },
  {
    id: "SVC-4098",
    unit: "U-02",
    date: "2026-05-22",
    performedBy: "R. Sinclair · Miller the Mover",
    category: "Repair",
    odometerKm: 154900,
    itemsChanged: ["Front brake pads", "Front rotors (machined)"],
    reason: "Front pads measured below 3 mm at periodic inspection; rotors scored.",
    partsUsed: [{ sku: "FLT-BRK-PAD-F", qty: 1 }],
    laborHours: 3,
    costCad: 620,
    workOrderId: "WO-1030",
  },
];

export const workOrders: WorkOrder[] = [
  mkWo("WO-1031", "U-01", "Front brake inspection & measure", "Driver reported soft pedal on U-01; inspect front brakes and measure pad thickness.", "Open", "High", "Manual", { assignedTo: "Thompson shop", dueDate: "2026-07-18", lineItems: ["Inspect front brakes", "Measure pad thickness"] }),
  mkWo("WO-1032", "U-03", "Wiper motor intermittent", "DTC B1000 raised for wiper motor; wipers cut out intermittently.", "Awaiting Parts", "Medium", "DTC Alert", { sourceRef: "B1000", assignedTo: "Thompson shop", lineItems: ["Replace wiper motor", "Clear DTC B1000"] }),
  mkWo("WO-1030", "U-02", "Replace front brake pads & rotors", "Post-trip DVIR flagged front pads below limit.", "Completed", "High", "Post-Trip Inspection", { sourceRef: "DVIR-2200", assignedTo: "Miller the Mover", lineItems: ["Replace front pads", "Machine rotors"], completedAt: "2026-05-22", resolvingServiceId: "SVC-4098" }),
];

export const inspections: Inspection[] = [
  mkInsp("DVIR-2201", "U-01", "Pre-Trip", "J. Spence", "Driver App", 210300, [], { tripId: "T-8841" }),
  mkInsp("DVIR-2202", "U-03", "Pre-Trip", "L. Moose", "Dispatcher Entry (paper backup)", 98750, [{ item: "Wiper blade — driver side", severity: "Minor", note: "Streaking; replace at next PM" }], { enteredBy: "Dispatch · A. Klein" }),
  mkInsp("DVIR-2200", "U-02", "Post-Trip", "R. Beardy", "Driver App", 154860, [{ item: "Front brakes", severity: "Out-of-Service", note: "Soft pedal, pads at limit" }], { generatedWorkOrderId: "WO-1030" }),
];

export const shops: Shop[] = [
  {
    id: "SHOP-01",
    name: "Thompson Certified Vehicle Inspection",
    contactName: "D. Fontaine",
    phone: "(204) 677-4120",
    email: "service@thompsoncvi.mb.ca",
    address: "142 Hayes Rd, Thompson, MB  R8N 1M4",
    gstBusinessNo: "81723 4471 RT0001",
    mpiAccredited: true,
    inspectionStationNo: "MB-1187",
    suppliesParts: false,
  },
  {
    id: "SHOP-02",
    name: "Miller the Mover — Fleet Services",
    contactName: "R. Sinclair",
    phone: "(204) 778-6301",
    email: "shop@millerthemover.ca",
    address: "8 Selkirk Ave, Thompson, MB  R8N 0M5",
    gstBusinessNo: "72991 0088 RT0001",
    mpiAccredited: false,
    suppliesParts: false,
  },
  {
    id: "SHOP-03",
    name: "NAPA Auto Parts — Thompson",
    contactName: "Parts counter",
    phone: "(204) 677-2277",
    email: "thompson@napacanada.com",
    address: "83 Station Rd, Thompson, MB  R8N 0N3",
    mpiAccredited: false,
    suppliesParts: true,
  },
];

// ---- seed builders ----------------------------------------------------------

function mkDoc(
  unit: string,
  type: VehicleDocument["type"],
  fileName: string,
  fileSizeKb: number,
  uploadedBy: string,
  uploadedAt: string,
  expiry: string | null,
): VehicleDocument {
  return {
    id: `DOC-${nextId("doc")}`,
    unit,
    type,
    fileName,
    fileSizeKb,
    uploadedBy,
    uploadedAt,
    expiry,
    k: docStatusFor(expiry),
  };
}

function mkWo(
  id: string,
  unit: string,
  title: string,
  description: string,
  status: WorkOrder["status"],
  priority: WorkOrderPriority,
  source: WorkOrder["source"],
  extra: Partial<WorkOrder>,
): WorkOrder {
  const base: WorkOrder = {
    id,
    unit,
    title,
    description,
    status,
    priority,
    source,
    k: priorityKind(priority),
    createdBy: "Dispatch",
    createdAt: "2026-06-01",
    lineItems: [],
  };
  const merged = { ...base, ...extra };
  merged.k = workOrderKind(merged);
  return merged;
}

function mkInsp(
  id: string,
  unit: string,
  type: Inspection["type"],
  driver: string,
  source: Inspection["source"],
  odometerKm: number,
  defects: Inspection["defects"],
  extra: Partial<Inspection>,
): Inspection {
  const result = resultForDefects(defects);
  return {
    id,
    unit,
    type,
    driver,
    source,
    odometerKm,
    defects,
    checklist: buildChecklist(defects),
    performedAt: "2026-07-13",
    result,
    k: inspectionKind(result),
    ...extra,
  };
}

// ---- mutations --------------------------------------------------------------

export function addDocument(d: Omit<VehicleDocument, "id" | "k">): VehicleDocument {
  const doc: VehicleDocument = { ...d, id: `DOC-${nextId("doc")}`, k: docStatusFor(d.expiry) };
  documents.unshift(doc);
  emit();
  return doc;
}

export function addService(s: Omit<ServiceRecord, "id">): ServiceRecord {
  const rec: ServiceRecord = { ...s, id: `SVC-${nextId("svc")}` };
  services.unshift(rec);
  emit();
  return rec;
}

export function addWorkOrder(w: Omit<WorkOrder, "id" | "createdAt" | "k">): WorkOrder {
  const wo: WorkOrder = {
    ...w,
    id: `WO-${nextId("wo")}`,
    createdAt: new Date().toISOString().slice(0, 10),
    k: priorityKind(w.priority),
  };
  wo.k = workOrderKind(wo);
  workOrders.unshift(wo);
  emit();
  return wo;
}

export function updateWorkOrder(id: string, patch: Partial<WorkOrder>): void {
  const wo = workOrders.find((w) => w.id === id);
  if (!wo) return;
  Object.assign(wo, patch);
  wo.k = workOrderKind(wo);
  emit();
}

export function addInspection(i: Omit<Inspection, "id" | "k">): Inspection {
  const insp: Inspection = {
    ...i,
    id: `DVIR-${nextId("dvir")}`,
    k: inspectionKind(i.result),
    checklist: i.checklist ?? buildChecklist(i.defects),
  };
  inspections.unshift(insp);
  emit();
  return insp;
}

export function addShop(s: Omit<Shop, "id">): Shop {
  const shop: Shop = { ...s, id: `SHOP-${String(nextId("shop")).padStart(2, "0")}` };
  shops.push(shop);
  emit();
  return shop;
}

export function updateShop(id: string, patch: Partial<Shop>): void {
  const shop = shops.find((s) => s.id === id);
  if (!shop) return;
  Object.assign(shop, patch);
  emit();
}

/** Link an inspection to a work order it generated (called after WO creation). */
export function linkInspectionWorkOrder(inspectionId: string, workOrderId: string): void {
  const insp = inspections.find((i) => i.id === inspectionId);
  if (!insp) return;
  insp.generatedWorkOrderId = workOrderId;
  emit();
}

// ---- subscription (useSyncExternalStore) ------------------------------------

let version = 0;
const listeners = new Set<() => void>();

function emit(): void {
  version += 1;
  listeners.forEach((l) => l());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Subscribe a component to store mutations. Returns a version number that
 *  changes on every mutation, forcing a re-read of the exported arrays. */
export function useMaintenanceStore(): number {
  return useSyncExternalStore(
    subscribe,
    () => version,
    () => version,
  );
}
