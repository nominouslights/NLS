"use client";

import { useSyncExternalStore } from "react";
import { driverLeaves } from "./data";
import type { DriverLeave } from "./types";

// -----------------------------------------------------------------------------
// Leave prototype store — MOCK ONLY (leave is explicitly outside the backend
// Drivers module's scope). Adds manual-entry mutations on top of the seed
// array in lib/data.ts, mirroring lib/clientStore.ts's module-level store +
// useSyncExternalStore pattern so edits survive navigating away from and back
// to the Drivers screen. Driver records, credentials, clearances and hours of
// service live in the real API now — see lib/api/drivers.ts and lib/api/hos.ts.
// -----------------------------------------------------------------------------

// ---- id counters ------------------------------------------------------------

const counters = { lv: 4100 };
function nextId(kind: keyof typeof counters, prefix: string): string {
  counters[kind] += 1;
  return `${prefix}-${counters[kind]}`;
}

// ---- mutations --------------------------------------------------------------

export function addLeave(input: Omit<DriverLeave, "id">): DriverLeave {
  const leave: DriverLeave = { ...input, id: nextId("lv", "LV") };
  driverLeaves.push(leave);
  emit();
  return leave;
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
export function useDriverStore(): number {
  return useSyncExternalStore(
    subscribe,
    () => version,
    () => version,
  );
}
