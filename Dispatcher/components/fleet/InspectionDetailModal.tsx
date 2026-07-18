"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import type { Inspection, InspectionChecklistItem, WorkOrderPrefill } from "@/lib/types";
import { buildChecklist } from "@/lib/maintenanceStore";
import { prefillFromInspection } from "@/lib/inspectionWorkOrder";
import { formatKm, formatUtcDate } from "@/lib/api";
import { ModalShell } from "@/components/ui/ModalShell";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import WorkOrderModal from "@/components/WorkOrderModal";

// Detail view of a DVIR inspection — the full checklist (what passed and what
// failed), with a "create work order" action on every failed item.

export default function InspectionDetailModal({
  inspection,
  units,
  onClose,
}: {
  inspection: Inspection;
  units: { value: string; label: string }[];
  onClose: () => void;
}) {
  const [woPrefill, setWoPrefill] = useState<WorkOrderPrefill | null>(null);

  const checklist: InspectionChecklistItem[] = inspection.checklist ?? buildChecklist(inspection.defects);
  const passed = checklist.filter((c) => c.status === "Pass").length;
  const failed = checklist.filter((c) => c.status === "Defect").length;

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${inspection.unit} · DVIR`}
      title={`${inspection.id} · ${inspection.type}`}
      onClose={onClose}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {passed} passed · {failed} failed
          </span>
          <ActionButton onClick={onClose}>CLOSE</ActionButton>
          {inspection.defects.length > 0 && !inspection.generatedWorkOrderId && (
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
        <StatusChip kind={inspection.k} label={inspection.result} />
        <MonoTag color={inspection.source.startsWith("Dispatcher") ? colors.amberText : colors.textDim}>
          {inspection.source}
        </MonoTag>
        {inspection.generatedWorkOrderId && <MonoTag color={colors.skyBlue}>→ {inspection.generatedWorkOrderId}</MonoTag>}
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: 16 }}>
        {inspection.driver} · {formatUtcDate(inspection.performedAt)} · {formatKm(inspection.odometerKm)}
        {inspection.enteredBy ? ` · entered by ${inspection.enteredBy}` : ""}
      </div>

      {/* checklist */}
      <div style={{ display: "flex", flexDirection: "column", gap: 5 }}>
        {checklist.map((c, i) => {
          const isDefect = c.status === "Defect";
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
                    {c.severity}
                    {c.note ? ` — ${c.note}` : ""}
                  </div>
                )}
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
                <StatusChip kind={isDefect ? "over" : "ontime"} label={isDefect ? "Defect" : "Pass"} />
                {isDefect && !inspection.generatedWorkOrderId && (
                  <ActionButton
                    onClick={() =>
                      setWoPrefill(
                        prefillFromInspection(inspection, inspection.unit, [
                          { item: c.item, severity: c.severity ?? "Major", note: c.note },
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
          units={units}
          defaultUnit={inspection.unit}
          prefill={woPrefill}
          onClose={() => setWoPrefill(null)}
          onSaved={onClose}
        />
      )}
    </ModalShell>
  );
}
