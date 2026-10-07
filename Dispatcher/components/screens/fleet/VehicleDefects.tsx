"use client";

import { useEffect, useRef, useState } from "react";
import type { Vehicle } from "@/lib/api";
import type { VehicleDefectWire } from "@/lib/api/maintenance";
import { prefillFromDefects, type PendingWorkOrder, type WorkOrderPrefillWire } from "@/lib/inspectionWorkOrder";
import type { VehicleOption } from "@/components/screens/fleet/vehicle-detail/shared";
import DefectsPanel from "@/components/fleet/DefectsPanel";
import InspectionEntryModal from "@/components/InspectionEntryModal";
import WorkOrderModal from "@/components/WorkOrderModal";

// The vehicle's defect backlog — the only surface with the resolved-history
// toggle, the Re-report button and CREATE WORK ORDER. Re-reporting opens the
// ordinary DVIR entry modal prefilled with the item, pointed back at the
// inspection whose resolution it supersedes: a fault that comes back is a NEW
// defect, never a reopen of the old one. Creating a work order opens the
// ordinary work-order modal with the chosen defects attached.

/** The defect read model catches up on the projector's 5s poll. */
const PROJECTION_RECONCILE_MS = 6000;

const defectKey = (d: { inspectionId: string; item: string }) => `${d.inspectionId}:${d.item}`;

export default function VehicleDefects({ vehicle, vehicles = [] }: { vehicle: Vehicle; vehicles?: VehicleOption[] }) {
  const [reReport, setReReport] = useState<VehicleDefectWire | null>(null);
  const [woPrefill, setWoPrefill] = useState<{ prefill: WorkOrderPrefillWire; keys: string[] } | null>(null);
  // Rows a work order was just raised for — shown as such until the read model
  // reflects the link, so the same defect is not offered twice in the gap. Each
  // mark heals itself: DefectsPanel reports, after every load, the marks that
  // load settled (the row now shows the work order / is resolved, or the load
  // came after the mark's own recheck window — whichever action triggered it).
  // Nothing here clears marks wholesale on a timer, so a later refresh (a
  // re-report, say) can never strand one.
  const [pending, setPending] = useState<ReadonlyMap<string, PendingWorkOrder>>(new Map());
  const [refresh, setRefresh] = useState(0);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  function scheduleRefresh() {
    // Refresh once now (in case the projection is already there) and again
    // after the projector has certainly run. Replacing an earlier timer is
    // safe: the new one fires later, so it is still past every older mark's
    // recheck window.
    setRefresh((n) => n + 1);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setRefresh((n) => n + 1), PROJECTION_RECONCILE_MS);
  }

  function onWorkOrderCreated(keys: string[], workOrderId: string) {
    const mark: PendingWorkOrder = { workOrderId, recheckAfter: Date.now() + PROJECTION_RECONCILE_MS };
    setPending((prev) => {
      const next = new Map(prev);
      for (const k of keys) next.set(k, mark);
      return next;
    });
    scheduleRefresh();
  }

  function onSettled(keys: string[]) {
    setPending((prev) => {
      if (!keys.some((k) => prev.has(k))) return prev;
      const next = new Map(prev);
      for (const k of keys) next.delete(k);
      return next;
    });
  }

  // The vehicle itself must be selectable in the work-order modal even when the
  // parent passed no options.
  const vehicleOptions = vehicles.some((v) => v.id === vehicle.id)
    ? vehicles
    : [{ id: vehicle.id, unit: vehicle.unitNumber, label: vehicle.unitNumber }, ...vehicles];

  return (
    <div>
      <DefectsPanel
        vehicleId={vehicle.id}
        unit={vehicle.unitNumber}
        variant="inline"
        canResolve
        showResolvedToggle
        maxRows={null}
        refreshKey={refresh}
        onReReport={setReReport}
        onCreateWorkOrder={(rows) =>
          setWoPrefill({ prefill: prefillFromDefects(rows, vehicle.unitNumber), keys: rows.map(defectKey) })
        }
        pendingWorkOrders={pending}
        onPendingWorkOrdersSettled={onSettled}
      />

      {reReport && (
        <InspectionEntryModal
          vehicleId={vehicle.id}
          unit={vehicle.unitNumber}
          type="PreTrip"
          odometerKm={vehicle.odometerKm}
          prefill={{
            item: reReport.item,
            severity: reReport.severity,
            note: reReport.note,
            recurrenceOfInspectionId: reReport.inspectionId,
          }}
          onClose={() => setReReport(null)}
          onSaved={() => scheduleRefresh()}
        />
      )}

      {woPrefill && (
        <WorkOrderModal
          vehicles={vehicleOptions}
          defaultVehicleId={vehicle.id}
          prefill={woPrefill.prefill}
          onClose={() => setWoPrefill(null)}
          onSaved={(id) => onWorkOrderCreated(woPrefill.keys, id)}
        />
      )}
    </div>
  );
}
