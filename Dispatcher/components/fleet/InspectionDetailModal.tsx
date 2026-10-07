"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { formatKm, formatUtcDate } from "@/lib/api";
import type { DefectSeverityWire, InspectionDefectWire, VehicleInspection } from "@/lib/api/maintenance";
import { isAttachable, prefillFromInspection, type WorkOrderPrefillWire } from "@/lib/inspectionWorkOrder";
import {
  DEFECT_RESOLUTION_LABEL,
  DEFECT_RESOLUTION_META,
  DEFECT_SEVERITY_LABEL,
  INSPECTION_RESULT_META,
} from "@/lib/workOrderDisplay";
import type { VehicleOption } from "@/components/screens/fleet/vehicle-detail/shared";
import { ModalShell } from "@/components/ui/ModalShell";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import WorkOrderModal from "@/components/WorkOrderModal";
import { ResolveDefectModal } from "@/components/fleet/DefectsPanel";

// Detail view of a live DVIR inspection — the full checklist (what passed and
// what failed). Each failed item reads its own state from the defect: Resolved,
// on an open work order, or open — and only an open, unattached defect offers
// RESOLVE and CREATE WORK ORDER. "All defects" attaches every open, unattached
// defect; a defect already on an open work order is never offered twice.

interface ChecklistRow {
  item: string;
  passed: boolean;
  severity?: DefectSeverityWire;
  note?: string | null;
  defect?: InspectionDefectWire;
}

export default function InspectionDetailModal({
  inspection,
  vehicleId,
  woNumber,
  workOrderNumberOf,
  vehicles,
  onWorkOrderCreated,
  onChanged,
  onClose,
}: {
  inspection: VehicleInspection;
  vehicleId: string;
  /** Number of the legacy whole-inspection work order (`generatedWorkOrderId`). */
  woNumber?: string;
  /** Resolves a defect's `workOrderId` to its WO-n number. */
  workOrderNumberOf?: (id: string) => string | undefined;
  vehicles: VehicleOption[];
  onWorkOrderCreated: () => void;
  /** A defect was resolved here — the parent refetches. */
  onChanged?: () => void;
  onClose: () => void;
}) {
  const [woPrefill, setWoPrefill] = useState<WorkOrderPrefillWire | null>(null);
  const [resolving, setResolving] = useState<ChecklistRow | null>(null);
  // The inspection list is a projected read model (≈5s behind a write), so a
  // defect resolved from HERE is shown resolved immediately rather than
  // offering RESOLVE again until the projection catches up.
  const [resolvedHere, setResolvedHere] = useState<ReadonlySet<string>>(new Set());

  const typeLabel = inspection.type === "PreTrip" ? "Pre-Trip" : "Post-Trip";
  const rm = INSPECTION_RESULT_META[inspection.result] ?? INSPECTION_RESULT_META.Pass;
  const openDefects = inspection.defects.filter((d) => isAttachable(d) && !resolvedHere.has(d.item));

  // Checklist joined with defects by item name; manifest-derived records can
  // carry defects without a checklist, so synthesize rows from the defects then.
  const defectByItem = new Map(inspection.defects.map((d) => [d.item, d]));
  const checklist: ChecklistRow[] = inspection.checklist.length
    ? inspection.checklist.map((c) => {
        const d = defectByItem.get(c.item);
        return { item: c.item, passed: c.passed && !d, severity: d?.severity, note: d?.note, defect: d };
      })
    : inspection.defects.map((d) => ({ item: d.item, passed: false, severity: d.severity, note: d.note, defect: d }));
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
          {openDefects.length > 1 && (
            <ActionButton
              variant="primary"
              onClick={() => setWoPrefill(prefillFromInspection(inspection, inspection.unit, openDefects))}
            >
              CREATE WORK ORDER FOR ALL {openDefects.length} OPEN DEFECTS
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
              <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap", justifyContent: "flex-end" }}>
                <StatusChip kind={isDefect ? "over" : "ontime"} label={isDefect ? "Defect" : "Pass"} />
                {isDefect && (
                  <DefectState
                    row={c}
                    resolvedHere={resolvedHere.has(c.item)}
                    workOrderNumberOf={workOrderNumberOf}
                    onResolve={() => setResolving(c)}
                    onCreateWorkOrder={(d) => setWoPrefill(prefillFromInspection(inspection, inspection.unit, [d]))}
                  />
                )}
              </div>
            </div>
          );
        })}
      </div>

      {resolving && (
        <ResolveDefectModal
          inspectionId={inspection.id}
          item={resolving.item}
          unit={inspection.unit}
          severity={resolving.severity}
          onClose={() => setResolving(null)}
          onResolved={() => {
            setResolvedHere((prev) => new Set([...prev, resolving.item]));
            setResolving(null);
            onChanged?.();
          }}
        />
      )}

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

/** Resolved / On WO-x / open (RESOLVE + CREATE WORK ORDER) — from the defect's own fields. */
function DefectState({
  row,
  resolvedHere,
  workOrderNumberOf,
  onResolve,
  onCreateWorkOrder,
}: {
  row: ChecklistRow;
  resolvedHere: boolean;
  workOrderNumberOf?: (id: string) => string | undefined;
  onResolve: () => void;
  onCreateWorkOrder: (d: InspectionDefectWire) => void;
}) {
  const d = row.defect;
  if (resolvedHere) return <StatusChip kind="ontime" label="Resolved" />;
  // A checklist failure with no defect entry cannot be addressed by the API.
  if (!d) return null;
  if (d.resolvedAtUtc != null) {
    const reason = d.resolutionReason;
    const m = reason ? DEFECT_RESOLUTION_META[reason] : undefined;
    return (
      <StatusChip
        kind={m?.kind ?? "ontime"}
        glyph={m?.glyph}
        label={reason ? `Resolved · ${DEFECT_RESOLUTION_LABEL[reason] ?? reason}` : "Resolved"}
      />
    );
  }
  if (d.workOrderId != null) {
    return <StatusChip kind="soon" label={`On ${workOrderNumberOf?.(d.workOrderId) ?? "open work order"}`} />;
  }
  return (
    <>
      <ActionButton onClick={onResolve}>RESOLVE</ActionButton>
      <ActionButton onClick={() => onCreateWorkOrder(d)}>CREATE WORK ORDER</ActionButton>
    </>
  );
}
