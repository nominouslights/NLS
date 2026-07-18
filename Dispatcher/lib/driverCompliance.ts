import { driverCredentials, hosLogs } from "./data";
import type { StatusKind } from "./theme";
import type { DriverCredential, HosLogEntry } from "./types";

// Pure selectors over the driver-compliance mock data (no React). Keep data.ts
// declarative — derivation lives here, mirroring inspectionWorkOrder.ts.

/** Credential type display order — licence first, then the mandatory checks,
 *  then optional creds last. */
const TYPE_ORDER: Record<DriverCredential["type"], number> = {
  "Licence Class": 0,
  "Police Record Check": 1,
  "Drug & Alcohol": 2,
  "First Aid": 3,
  "Work Permit": 4,
};

export function credentialsFor(driverId: number): DriverCredential[] {
  return driverCredentials
    .filter((c) => c.driverId === driverId)
    .sort((a, b) => TYPE_ORDER[a.type] - TYPE_ORDER[b.type]);
}

export function hosLogsFor(driverId: number): HosLogEntry[] {
  return hosLogs
    .filter((h) => h.driverId === driverId)
    .sort((a, b) => b.date.localeCompare(a.date)); // newest first
}

/** Soon/expired credentials for the selected driver — powers the detail alert. */
export function expiringCredentials(driverId: number): DriverCredential[] {
  return credentialsFor(driverId).filter((c) => c.k === "soon" || c.k === "over");
}

// over (expired) is worse than soon (expiring) is worse than ontime (valid).
const SEVERITY: Record<StatusKind, number> = {
  over: 3,
  soon: 2,
  ontime: 1,
  off: 0,
  info: 0,
};

/** Worst credential status for a driver — drives the roster alert flag.
 *  Returns null when the driver has no soon/over credential. */
export function worstCredentialKind(driverId: number): StatusKind | null {
  let worst: StatusKind | null = null;
  for (const c of credentialsFor(driverId)) {
    if (c.k !== "soon" && c.k !== "over") continue;
    if (worst === null || SEVERITY[c.k] > SEVERITY[worst]) worst = c.k;
  }
  return worst;
}
