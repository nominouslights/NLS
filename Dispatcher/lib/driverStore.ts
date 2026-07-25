"use client";

import { useSyncExternalStore } from "react";
import { driverLeaves, hosLogs } from "./data";
import type { DriverLeave, HosLogEntry } from "./types";

// -----------------------------------------------------------------------------
// HOS & leave prototype store — MOCK ONLY (HOS/leave is explicitly outside the
// backend Drivers module's scope). Adds manual-entry mutations on top of the
// seed arrays in lib/data.ts, mirroring lib/clientStore.ts's module-level
// store + useSyncExternalStore pattern so edits survive navigating away from
// and back to the Drivers screen. Driver records, credentials, and clearances
// live in the real API now — see lib/api/drivers.ts.
// -----------------------------------------------------------------------------

// ---- id counters ------------------------------------------------------------

const counters = { hos: 2100, lv: 4100 };
function nextId(kind: keyof typeof counters, prefix: string): string {
  counters[kind] += 1;
  return `${prefix}-${counters[kind]}`;
}

// ---- mutations --------------------------------------------------------------

export function addHosLogEntry(input: Omit<HosLogEntry, "id">): HosLogEntry {
  const entry: HosLogEntry = { ...input, id: nextId("hos", "HOS") };
  hosLogs.unshift(entry);
  emit();
  return entry;
}

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
