"use client";

import { useEffect, useState } from "react";
import { ApiError } from "./api/transport";
import { listVehicleDefects, type VehicleDefectWire } from "./api/maintenance";
import { useDefectsChanged } from "./defectSync";

// A unit's open defects, for the inspection forms' known-fault hints
// (TripInspectionModal, InspectionEntryModal). Read-only and WARN ONLY, like
// DefectsPanel: the result is shown, never gated on, and never reported up.

export type OpenDefectsLoad =
  /** No vehicle to ask about — no request is issued. */
  | { status: "none" }
  | { status: "loading" }
  | { status: "error"; message: string }
  | { status: "ready"; rows: VehicleDefectWire[] };

/**
 * Loads `listVehicleDefects(vehicleId)` (open only, server order). The result is
 * TAGGED with the vehicle it was fetched for, the way DefectsPanel tags its rows:
 * when the form's unit changes, the previous unit's result — or its failure —
 * stops matching at once and can never paint under the new unit, even if its
 * request lands after the new one was sent. Re-reads after a defect changes
 * anywhere in the console (lib/defectSync).
 */
export function useVehicleOpenDefects(vehicleId: string | null): OpenDefectsLoad {
  const [fetched, setFetched] = useState<
    { vehicleId: string; rows: VehicleDefectWire[] } | { vehicleId: string; error: string } | null
  >(null);
  const [reload, setReload] = useState(0);

  useEffect(() => {
    if (!vehicleId) return;
    let active = true;
    listVehicleDefects(vehicleId).then(
      (rows) => {
        if (active) setFetched({ vehicleId, rows });
      },
      (e) => {
        if (active) {
          setFetched({ vehicleId, error: e instanceof ApiError ? e.message : "Failed to load open defects." });
        }
      },
    );
    return () => {
      active = false;
    };
  }, [vehicleId, reload]);

  useDefectsChanged(() => setReload((n) => n + 1));

  if (!vehicleId) return { status: "none" };
  if (!fetched || fetched.vehicleId !== vehicleId) return { status: "loading" };
  if ("error" in fetched) return { status: "error", message: fetched.error };
  // The endpoint lists open defects only; filtered again so a resolved row can
  // never be presented as a known open fault.
  return { status: "ready", rows: fetched.rows.filter((r) => r.resolvedAtUtc == null) };
}
