"use client";

import { useEffect, useState } from "react";
import { ApiError } from "@/lib/api/transport";
import { hasClearanceFor, todayIso } from "@/lib/api/trips";
import {
  listDriverClearances,
  listDrivers,
  type DriverClearanceRecord,
  type DriverRecord,
} from "@/lib/api/drivers";

// The driver picker's roster + client-clearance check, shared by REASSIGN
// (AssignModal), the return-leg modal and CORRECT DRIVER. Clearance is a
// UI-side warning only, never a block: a failed clearance fetch degrades to
// "no clearances on file" (warn) instead of failing the roster.

export interface DriverRosterEntry {
  driver: DriverRecord;
  /** Held a clearance for the trip's client in force on `asOf` (always true when the trip has no client). */
  cleared: boolean;
}

export interface DriverRosterOptions {
  /** The trip's client — null skips every clearance request (nothing to clear). */
  clientName: string | null;
  /** yyyy-MM-dd the clearance must be in force on. Default today (live assignment);
   *  a historical correction passes the trip's serviceDate. */
  asOf?: string;
  /** "active" — Active drivers only (assignment). "all" — every driver on record,
   *  Inactive/Deactivated included (correction). */
  include: "active" | "all";
  /** Driver left off the list (the trip's current driver on a correction). */
  excludeDriverId?: string | null;
}

/** Loads the roster, then fetches clearances only for the drivers it lists
 *  (once each), and only when the trip has a client. */
export async function loadDriverRoster({
  clientName,
  asOf,
  include,
  excludeDriverId = null,
}: DriverRosterOptions): Promise<DriverRosterEntry[]> {
  const drivers = (await listDrivers()).filter(
    (d) => (include === "all" || d.status === "Active") && d.id !== excludeDriverId,
  );
  if (!clientName) return drivers.map((driver) => ({ driver, cleared: true }));

  const day = asOf ?? todayIso();
  const clearances = await Promise.all(
    drivers.map((d) => listDriverClearances(d.id).catch(() => [] as DriverClearanceRecord[])),
  );
  return drivers.map((driver, i) => ({ driver, cleared: hasClearanceFor(clearances[i], clientName, day) }));
}

/** React wrapper over loadDriverRoster: `roster` is null while loading; `error`
 *  is the server message (verbatim) or a generic fallback. */
export function useDriverRoster({ clientName, asOf, include, excludeDriverId = null }: DriverRosterOptions): {
  roster: DriverRosterEntry[] | null;
  error: string | null;
} {
  const [roster, setRoster] = useState<DriverRosterEntry[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    loadDriverRoster({ clientName, asOf, include, excludeDriverId }).then(
      (rows) => {
        if (active) setRoster(rows);
      },
      (e: unknown) => {
        if (active) setError(e instanceof ApiError ? e.message : "Failed to load drivers.");
      },
    );
    return () => {
      active = false;
    };
  }, [clientName, asOf, include, excludeDriverId]);

  return { roster, error };
}
