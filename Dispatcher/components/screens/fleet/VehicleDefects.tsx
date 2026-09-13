"use client";

import { useEffect, useRef, useState } from "react";
import type { Vehicle } from "@/lib/api";
import type { VehicleDefectWire } from "@/lib/api/maintenance";
import DefectsPanel from "@/components/fleet/DefectsPanel";
import InspectionEntryModal from "@/components/InspectionEntryModal";

// The vehicle's defect backlog — the only surface with the resolved-history
// toggle and the Re-report button. Re-reporting opens the ordinary DVIR entry
// modal prefilled with the item, pointed back at the inspection whose
// resolution it supersedes: a fault that comes back is a NEW defect, never a
// reopen of the old one.

export default function VehicleDefects({ vehicle }: { vehicle: Vehicle }) {
  const [reReport, setReReport] = useState<VehicleDefectWire | null>(null);
  const [refresh, setRefresh] = useState(0);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (timer.current) clearTimeout(timer.current);
    };
  }, []);

  function onSaved() {
    // The new DVIR reaches the defect read model on the projector's 5s poll, so
    // refresh once now (in case it is already there) and again after it has
    // certainly run.
    setRefresh((n) => n + 1);
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => setRefresh((n) => n + 1), 6000);
  }

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
          onSaved={onSaved}
        />
      )}
    </div>
  );
}
