"use client";

import { useSyncExternalStore } from "react";
import type { StatusKind } from "./theme";
import type { ServiceRecord, VehicleDocument } from "./types";

// Standard DVIR checklist (NSC Standard 11 / trip inspection). Single source of
// truth for the dispatcher inspection-entry modal.
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
// Vehicle registration, work orders, shops, and DVIR inspections are real
// (lib/api.ts, lib/api/maintenance.ts). What remains here — compliance
// documents and the standalone service-history log — is an in-memory
// prototype. A module-level store (not per-component state) so edits survive
// navigating away from and back to the merged Fleet & Maintenance screen,
// which Console.tsx unmounts on nav. Keyed by `unit` (unitNumber), matching
// the existing dtcAlerts convention in lib/data.ts.
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

// ---- id counters ------------------------------------------------------------
// Declared before the seed arrays: the seed builders below call nextId() during
// module init, so `counters` must already be initialized (no TDZ).

const counters: Record<string, number> = { doc: 5000, svc: 4200 };
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
