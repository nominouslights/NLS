"use client";

import { useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError, formatKm, formatUtcDate, type Vehicle } from "@/lib/api";
import {
  listInspections,
  listVehicleWorkOrders,
  type InspectionType,
  type VehicleInspection,
  type WorkOrderWire,
} from "@/lib/api/maintenance";
import { prefillFromInspection, type WorkOrderPrefillWire } from "@/lib/inspectionWorkOrder";
import { DEFECT_SEVERITY_LABEL, INSPECTION_RESULT_META } from "@/lib/workOrderDisplay";
import type { VehicleOption } from "@/components/screens/fleet/vehicle-detail/shared";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import InspectionEntryModal from "@/components/InspectionEntryModal";
import InspectionDetailModal from "@/components/fleet/InspectionDetailModal";
import WorkOrderModal from "@/components/WorkOrderModal";

// Per-vehicle DVIR log (live Fleet API) — driver-app / manifest-derived
// inspections and dispatcher paper-backup entries in one list. Open a row for
// the full checklist, turn defects into work orders (per-defect or all at
// once), and a Major / Out-of-Service entry auto-offers a prefilled work order
// right after saving.

export default function VehicleInspections({
  vehicle,
  vehicles,
}: {
  vehicle: Vehicle;
  vehicles: VehicleOption[];
}) {
  const unit = vehicle.unitNumber;
  const vehicleId = vehicle.id;

  const [entryType, setEntryType] = useState<InspectionType | null>(null);
  const [detail, setDetail] = useState<VehicleInspection | null>(null);
  const [woPrefill, setWoPrefill] = useState<WorkOrderPrefillWire | null>(null);
  const [reload, setReload] = useState(0);
  const [loadError, setLoadError] = useState<string | null>(null);
  // Rows are tagged with the unit they were fetched for, so switching vehicles
  // never shows another unit's inspections while the new fetch is in flight.
  const [inspFetch, setInspFetch] = useState<{ unit: string; rows: VehicleInspection[] } | null>(null);
  // Work orders resolve generatedWorkOrderId GUIDs to their WO-n numbers for
  // the back-link tags. Fail-soft: links fall back to a generic label.
  const [woFetch, setWoFetch] = useState<{ vehicleId: string; rows: WorkOrderWire[] } | null>(null);

  useEffect(() => {
    let active = true;
    listInspections({ unit }).then(
      (fresh) => {
        if (active) {
          setInspFetch({ unit, rows: fresh });
          setLoadError(null);
        }
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load inspections.");
      },
    );
    listVehicleWorkOrders(vehicleId).then(
      (fresh) => {
        if (active) setWoFetch({ vehicleId, rows: fresh });
      },
      (e) => {
        console.error("Work orders unavailable:", e);
      },
    );
    return () => {
      active = false;
    };
  }, [unit, vehicleId, reload]);

  const fetchedRows = inspFetch?.unit === unit ? inspFetch.rows : null;
  const rows = fetchedRows
    ? [...fetchedRows].sort((a, b) => (a.performedAt < b.performedAt ? 1 : -1))
    : null;
  const woNumberById = new Map(
    (woFetch?.vehicleId === vehicleId ? woFetch.rows : []).map((w) => [w.id, w.number]),
  );
  const woLink = (id: string) => woNumberById.get(id) ?? "work order";

  async function onInspectionSaved(id: string) {
    // Refetch and use the returned rows directly — no state-timing race between
    // the list update and the auto-offer below.
    let fresh: VehicleInspection[];
    try {
      fresh = await listInspections({ unit });
    } catch {
      setReload((n) => n + 1);
      return;
    }
    setInspFetch({ unit, rows: fresh });
    const insp = fresh.find((i) => i.id === id);
    // Auto-offer a work order when a major / out-of-service defect was recorded.
    if (insp && insp.defects.some((d) => d.severity === "Major" || d.severity === "OutOfService")) {
      setWoPrefill(prefillFromInspection(insp, unit));
    }
  }

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14, gap: 8 }}>
        <SectionLabel>Pre / post-trip inspections · DVIR</SectionLabel>
        <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
          <ActionButton variant="primary" onClick={() => setEntryType("PreTrip")}>
            ENTER PRE-TRIP
          </ActionButton>
          <ActionButton onClick={() => setEntryType("PostTrip")}>ENTER POST-TRIP</ActionButton>
        </div>
      </div>

      {loadError ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600, padding: "6px 2px" }}>
          ▲ {loadError}
        </div>
      ) : rows === null ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          Loading inspections…
        </div>
      ) : rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No inspections recorded for {unit}.
        </div>
      ) : (
        rows.map((insp) => {
          const rm = INSPECTION_RESULT_META[insp.result] ?? INSPECTION_RESULT_META.Pass;
          const hasDefects = insp.defects.length > 0;
          return (
            <Panel key={insp.id} style={{ marginBottom: 10, cursor: "pointer" }}>
              <div onClick={() => setDetail(insp)}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8, flexWrap: "wrap" }}>
                  {insp.tripNumber && <MonoTag color={colors.skyBlue}>{insp.tripNumber}</MonoTag>}
                  <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.headingBright }}>
                    {insp.type === "PreTrip" ? "Pre-Trip" : "Post-Trip"}
                  </span>
                  <StatusChip kind={rm.kind} label={rm.label} />
                  <MonoTag color={insp.source === "Dispatcher" ? colors.amberText : colors.textDim}>
                    {insp.source === "Dispatcher" ? "Dispatcher" : "Driver App"}
                  </MonoTag>
                  <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11.5, color: colors.skyBlue }}>
                    View details ›
                  </span>
                </div>

                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: hasDefects ? 8 : 0 }}>
                  {insp.driverName} · {formatUtcDate(insp.performedAt)}
                  {insp.odometerKm != null ? ` · ${formatKm(insp.odometerKm)}` : ""}
                  {insp.enteredBy ? ` · entered by ${insp.enteredBy}` : ""}
                </div>

                {hasDefects && (
                  <ul style={{ margin: "0 0 4px", paddingLeft: 18 }}>
                    {insp.defects.map((d, i) => (
                      <li key={i} style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6 }}>
                        <span style={{ fontWeight: 600, color: colors.textSecondary }}>{d.item}</span> —{" "}
                        {DEFECT_SEVERITY_LABEL[d.severity]}
                        {d.note ? `: ${d.note}` : ""}
                      </li>
                    ))}
                  </ul>
                )}

                {insp.generatedWorkOrderId && (
                  <MonoTag color={colors.skyBlue}>→ {woLink(insp.generatedWorkOrderId)}</MonoTag>
                )}
              </div>
            </Panel>
          );
        })
      )}

      {entryType && (
        <InspectionEntryModal
          vehicleId={vehicleId}
          unit={unit}
          type={entryType}
          odometerKm={vehicle.odometerKm}
          onClose={() => setEntryType(null)}
          onSaved={onInspectionSaved}
        />
      )}
      {detail && (
        <InspectionDetailModal
          inspection={detail}
          vehicleId={vehicleId}
          woNumber={detail.generatedWorkOrderId ? woLink(detail.generatedWorkOrderId) : undefined}
          vehicles={vehicles}
          onWorkOrderCreated={() => setReload((n) => n + 1)}
          onClose={() => setDetail(null)}
        />
      )}
      {woPrefill && (
        <WorkOrderModal
          vehicles={vehicles}
          defaultVehicleId={vehicleId}
          prefill={woPrefill}
          onClose={() => setWoPrefill(null)}
          onSaved={() => setReload((n) => n + 1)}
        />
      )}
    </div>
  );
}
