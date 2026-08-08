"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { formatKm, formatUtcDate } from "@/lib/api";
import type { DefectSeverityWire, VehicleInspection } from "@/lib/api/maintenance";
import { prefillFromInspection, type WorkOrderPrefillWire } from "@/lib/inspectionWorkOrder";
import { DEFECT_SEVERITY_LABEL, INSPECTION_RESULT_META } from "@/lib/workOrderDisplay";
import type { VehicleOption } from "@/components/screens/fleet/vehicle-detail/shared";
import { ModalShell } from "@/components/ui/ModalShell";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import WorkOrderModal from "@/components/WorkOrderModal";

// Detail view of a live DVIR inspection — the full checklist (what passed and
// what failed), with a "create work order" action on every failed item and one
// for all defects at once. Both disappear once a work order has been generated.

interface ChecklistRow {
  item: string;
  passed: boolean;
  severity?: DefectSeverityWire;
  note?: string | null;
}

export default function InspectionDetailModal({
  inspection,
  vehicleId,
  woNumber,
  vehicles,
  onWorkOrderCreated,
  onClose,
}: {
  inspection: VehicleInspection;
  vehicleId: string;
  woNumber?: string;
  vehicles: VehicleOption[];
  onWorkOrderCreated: () => void;
  onClose: () => void;
}) {
  const [woPrefill, setWoPrefill] = useState<WorkOrderPrefillWire | null>(null);

  const typeLabel = inspection.type === "PreTrip" ? "Pre-Trip" : "Post-Trip";
  const rm = INSPECTION_RESULT_META[inspection.result] ?? INSPECTION_RESULT_META.Pass;
  const canGenerate = inspection.defects.length > 0 && !inspection.generatedWorkOrderId;

  // Checklist joined with defects by item name; manifest-derived records can
  // carry defects without a checklist, so synthesize rows from the defects then.
  const defectByItem = new Map(inspection.defects.map((d) => [d.item, d]));
  const checklist: ChecklistRow[] = inspection.checklist.length
    ? inspection.checklist.map((c) => {
        const d = defectByItem.get(c.item);
        return { item: c.item, passed: c.passed && !d, severity: d?.severity, note: d?.note };
      })
    : inspection.defects.map((d) => ({ item: d.item, passed: false, severity: d.severity, note: d.note }));
  const passed = checklist.filter((c) => c.passed).length;
  const failed = checklist.filter((c) => !c.passed).length;

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${inspection.unit} · DVIR`}
      title={`${typeLabel} Inspection${inspection.tripNumber ? ` · ${inspection.tripNumber}` : ""}`}
      onClose={onClose}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {passed} passed · {failed} failed
          </span>
          <ActionButton onClick={onClose}>CLOSE</ActionButton>
          {canGenerate && (
            <ActionButton
              variant="primary"
              onClick={() => setWoPrefill(prefillFromInspection(inspection, inspection.unit))}
            >
              GENERATE WORK ORDER FROM ALL DEFECTS
            </ActionButton>
          )}
        </>
      }
    >
      {/* summary */}
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 6, flexWrap: "wrap" }}>
        <StatusChip kind={rm.kind} label={rm.label} />
        <MonoTag color={inspection.source === "Dispatcher" ? colors.amberText : colors.textDim}>
          {inspection.source === "Dispatcher" ? "Dispatcher" : "Driver App"}
        </MonoTag>
        {inspection.generatedWorkOrderId && (
          <MonoTag color={colors.skyBlue}>→ {woNumber ?? "work order"}</MonoTag>
        )}
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: 16 }}>
        {inspection.driverName} · {formatUtcDate(inspection.performedAt)}
        {inspection.odometerKm != null ? ` · ${formatKm(inspection.odometerKm)}` : ""}
        {inspection.enteredBy ? ` · entered by ${inspection.enteredBy}` : ""}
      </div>

      {/* checklist */}
      <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
        {checklist.map((c, i) => {
          const isDefect = !c.passed;
          const meta = statusMeta(isDefect ? "over" : "ontime");
          return (
            <div
              key={`${c.item}-${i}`}
              style={{
                display: "grid",
                gridTemplateColumns: "1fr auto",
                gap: 10,
                alignItems: "center",
                padding: "9px 12px",
                borderRadius: 9,
                border: `1px solid ${isDefect ? meta.bd : colors.borderSubtle}`,
                background: isDefect ? meta.bg : colors.cardBg,
              }}
            >
              <div style={{ minWidth: 0 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                  {c.item}
                </div>
                {isDefect && (
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: meta.t, marginTop: 2 }}>
                    {c.severity ? DEFECT_SEVERITY_LABEL[c.severity] : "Defect"}
                    {c.note ? ` — ${c.note}` : ""}
                  </div>
                )}
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
                <StatusChip kind={isDefect ? "over" : "ontime"} label={isDefect ? "Defect" : "Pass"} />
                {isDefect && canGenerate && (
                  <ActionButton
                    onClick={() =>
                      setWoPrefill(
                        prefillFromInspection(inspection, inspection.unit, [
                          { item: c.item, severity: c.severity ?? "Major", note: c.note ?? null },
                        ]),
                      )
                    }
                  >
                    CREATE WORK ORDER
                  </ActionButton>
                )}
              </div>
            </div>
          );
        })}
      </div>

      {woPrefill && (
        <WorkOrderModal
          vehicles={vehicles}
          defaultVehicleId={inspection.vehicleId ?? vehicleId}
          prefill={woPrefill}
          onClose={() => setWoPrefill(null)}
          onSaved={() => {
            onWorkOrderCreated();
            onClose();
          }}
        />
      )}
    </ModalShell>
  );
}
