"use client";

import { useEffect, useState } from "react";
import { colors, fonts, type StatusKind } from "@/lib/theme";
import type { Inspection, InspectionType, WorkOrderPrefill } from "@/lib/types";
import { inspections, useMaintenanceStore } from "@/lib/maintenanceStore";
import { prefillFromInspection } from "@/lib/inspectionWorkOrder";
import {
  formatKm,
  formatUtcDate,
  listInspections,
  type InspectionResultWire,
  type VehicleInspection,
} from "@/lib/api";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import InspectionEntryModal from "@/components/InspectionEntryModal";
import InspectionDetailModal from "@/components/fleet/InspectionDetailModal";
import WorkOrderModal from "@/components/WorkOrderModal";

// Per-vehicle DVIR log. Backend inspections (Fleet API, fed by submitted trip
// manifests over RabbitMQ) render first in their own labeled section; below
// them, the existing mock-store log where dispatchers enter a pre/post-trip
// inspection (the office fallback for a failed Driver App), open its full
// checklist, and turn defects into work orders.

/** Wire result → chip colour + label (glyph comes from StatusChip). */
const RESULT_META: Record<InspectionResultWire, { kind: StatusKind; label: string }> = {
  Pass: { kind: "ontime", label: "Pass" },
  PassWithDefects: { kind: "soon", label: "Pass with defects" },
  Fail: { kind: "over", label: "Fail" },
};

export default function VehicleInspections({
  unit,
  odometerKm,
  units,
}: {
  unit: string;
  odometerKm: number;
  units: { value: string; label: string }[];
}) {
  useMaintenanceStore();
  const [entryType, setEntryType] = useState<InspectionType | null>(null);
  const [detail, setDetail] = useState<Inspection | null>(null);
  const [woPrefill, setWoPrefill] = useState<WorkOrderPrefill | null>(null);
  // Rows are tagged with the unit they were fetched for, so switching vehicles
  // never shows another unit's inspections while the new fetch is in flight.
  const [apiFetch, setApiFetch] = useState<{ unit: string; rows: VehicleInspection[] }>({
    unit: "",
    rows: [],
  });
  const apiRows = apiFetch.unit === unit ? apiFetch.rows : [];

  useEffect(() => {
    // Backend inspections (Fleet API) — derived from submitted trip manifests
    // via the RabbitMQ consumer. Fail silent: the mock DVIR log below still works.
    let active = true;
    listInspections({ unit }).then(
      (fresh) => {
        if (active) setApiFetch({ unit, rows: fresh });
      },
      (e) => {
        console.error("Backend inspections unavailable:", e);
      },
    );
    return () => {
      active = false;
    };
  }, [unit]);

  const rows = [...inspections.filter((i) => i.unit === unit)].sort((a, b) =>
    a.performedAt < b.performedAt ? 1 : -1,
  );

  function onInspectionSaved(insp: Inspection) {
    // Auto-offer a work order when a major / out-of-service defect was recorded.
    const qualifies = insp.defects.some((d) => d.severity === "Major" || d.severity === "Out-of-Service");
    if (qualifies) setWoPrefill(prefillFromInspection(insp, unit));
  }

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14, gap: 8 }}>
        <SectionLabel>Pre / post-trip inspections · DVIR</SectionLabel>
        <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
          <ActionButton variant="primary" onClick={() => setEntryType("Pre-Trip")}>
            ENTER PRE-TRIP
          </ActionButton>
          <ActionButton onClick={() => setEntryType("Post-Trip")}>ENTER POST-TRIP</ActionButton>
        </div>
      </div>

      {/* Backend inspections (Trips → Fleet via integration events) — above the mock log */}
      {apiRows.length > 0 && (
        <div style={{ marginBottom: 18 }}>
          <SectionLabel>From trip manifests · Fleet API</SectionLabel>
          {apiRows.map((insp) => {
            const rm = RESULT_META[insp.result] ?? RESULT_META.Pass;
            return (
              <Panel key={insp.id} style={{ marginBottom: 10 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8, flexWrap: "wrap" }}>
                  <MonoTag color={colors.skyBlue}>{insp.tripNumber}</MonoTag>
                  <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.headingBright }}>
                    {insp.type === "PreTrip" ? "Pre-Trip" : "Post-Trip"}
                  </span>
                  <StatusChip kind={rm.kind} label={rm.label} />
                  <MonoTag color={insp.source === "Dispatcher" ? colors.amberText : colors.textDim}>
                    {insp.source === "Dispatcher" ? "Dispatcher" : "Driver App"}
                  </MonoTag>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: insp.defects.length ? 8 : 0 }}>
                  {insp.driverName} · {formatUtcDate(insp.performedAt)}
                  {insp.odometerKm != null ? ` · ${formatKm(insp.odometerKm)}` : ""}
                  {insp.enteredBy ? ` · entered by ${insp.enteredBy}` : ""}
                </div>
                {insp.defects.length > 0 && (
                  <ul style={{ margin: "0 0 4px", paddingLeft: 18 }}>
                    {insp.defects.map((d, i) => (
                      <li key={i} style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6 }}>
                        <span style={{ fontWeight: 600, color: colors.textSecondary }}>{d.item}</span> —{" "}
                        {d.severity === "OutOfService" ? "Out of service" : d.severity}
                        {d.note ? `: ${d.note}` : ""}
                      </li>
                    ))}
                  </ul>
                )}
              </Panel>
            );
          })}
          <SectionLabel>Console DVIR log (mock)</SectionLabel>
        </div>
      )}

      {rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No inspections recorded for {unit}.
        </div>
      ) : (
        rows.map((insp) => {
          const hasDefects = insp.defects.length > 0;
          return (
            <Panel key={insp.id} style={{ marginBottom: 10, cursor: "pointer" }}>
              <div onClick={() => setDetail(insp)}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8, flexWrap: "wrap" }}>
                  <MonoTag color={colors.skyBlue}>{insp.id}</MonoTag>
                  <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.headingBright }}>
                    {insp.type}
                  </span>
                  <StatusChip kind={insp.k} label={insp.result} />
                  <MonoTag color={insp.source.startsWith("Dispatcher") ? colors.amberText : colors.textDim}>
                    {insp.source}
                  </MonoTag>
                  <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11.5, color: colors.skyBlue }}>
                    View details ›
                  </span>
                </div>

                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: hasDefects ? 8 : 0 }}>
                  {insp.driver} · {formatUtcDate(insp.performedAt)} · {formatKm(insp.odometerKm)}
                  {insp.enteredBy ? ` · entered by ${insp.enteredBy}` : ""}
                </div>

                {hasDefects && (
                  <ul style={{ margin: "0 0 4px", paddingLeft: 18 }}>
                    {insp.defects.map((d, i) => (
                      <li key={i} style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6 }}>
                        <span style={{ fontWeight: 600, color: colors.textSecondary }}>{d.item}</span> — {d.severity}
                        {d.note ? `: ${d.note}` : ""}
                      </li>
                    ))}
                  </ul>
                )}

                {insp.generatedWorkOrderId && <MonoTag color={colors.skyBlue}>→ {insp.generatedWorkOrderId}</MonoTag>}
              </div>
            </Panel>
          );
        })
      )}

      {entryType && (
        <InspectionEntryModal
          unit={unit}
          type={entryType}
          odometerKm={odometerKm}
          onClose={() => setEntryType(null)}
          onSaved={onInspectionSaved}
        />
      )}
      {detail && (
        <InspectionDetailModal inspection={detail} units={units} onClose={() => setDetail(null)} />
      )}
      {woPrefill && (
        <WorkOrderModal
          units={units}
          defaultUnit={unit}
          prefill={woPrefill}
          onClose={() => setWoPrefill(null)}
          onSaved={() => undefined}
        />
      )}
    </div>
  );
}
