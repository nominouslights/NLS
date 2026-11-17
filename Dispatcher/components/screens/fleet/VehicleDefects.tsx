"use client";

import { useState } from "react";
import type { Vehicle } from "@/lib/api";
import type { VehicleDefectWire } from "@/lib/api/maintenance";
import { prefillFromDefects, type PendingWorkOrder, type WorkOrderPrefillWire } from "@/lib/inspectionWorkOrder";
import type { VehicleOption } from "@/components/screens/fleet/vehicle-detail/shared";
import DefectsPanel from "@/components/fleet/DefectsPanel";
import InspectionEntryModal from "@/components/InspectionEntryModal";
import WorkOrderModal from "@/components/WorkOrderModal";
import { PROJECTION_RECONCILE_MS } from "@/lib/defectSync";

// The vehicle's defect backlog — the only surface with the resolved-history
// toggle, the Re-report button and CREATE WORK ORDER. Re-reporting opens the
// ordinary DVIR entry modal prefilled with the item, pointed back at the
// inspection whose resolution it supersedes: a fault that comes back is a NEW
// defect, never a reopen of the old one. Creating a work order opens the
// ordinary work-order modal with the chosen defects attached.

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
  //
  // There is deliberately NO refresh timer here. Both writes this tab starts
  // announce themselves through lib/defectSync AFTER calling onSaved
  // (WorkOrderModal, InspectionEntryModal), and DefectsPanel's
  // useDefectsChanged turns that into the one delayed reload,
  // PROJECTION_RECONCILE_MS later — which, because the mark is set first, always
  // lands past the mark's recheck window and settles it. A second timer here
  // only doubled that reload.
  const [pending, setPending] = useState<ReadonlyMap<string, PendingWorkOrder>>(new Map());

  function onWorkOrderCreated(keys: string[], workOrderId: string) {
    const mark: PendingWorkOrder = { workOrderId, recheckAfter: Date.now() + PROJECTION_RECONCILE_MS };
    setPending((prev) => {
      const next = new Map(prev);
      for (const k of keys) next.set(k, mark);
      return next;
    });
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
          // The reload comes from the modal's own defectSync announcement.
          onSaved={() => {}}
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
