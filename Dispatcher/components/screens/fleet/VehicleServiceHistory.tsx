"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { services, useMaintenanceStore } from "@/lib/maintenanceStore";
import { formatCad, formatKm, formatUtcDate } from "@/lib/api";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import ServiceRecordModal from "@/components/ServiceRecordModal";

// Per-vehicle service history — a reverse-chronological log of WHO did the job,
// WHAT was changed, and WHY. Mock (no backend maintenance-records domain yet).

export default function VehicleServiceHistory({
  unit,
  odometerKm,
}: {
  unit: string;
  odometerKm: number;
}) {
  useMaintenanceStore();
  const [open, setOpen] = useState(false);

  const rows = [...services.filter((s) => s.unit === unit)].sort((a, b) => (a.date < b.date ? 1 : -1));

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>Service history · {rows.length} record{rows.length === 1 ? "" : "s"}</SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setOpen(true)}>
          + LOG SERVICE
        </ActionButton>
      </div>

      {rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No service records for {unit} yet.
        </div>
      ) : (
        rows.map((s) => (
          <Panel key={s.id} style={{ marginBottom: 10 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 9 }}>
              <MonoTag color={colors.skyBlue}>{s.id}</MonoTag>
              <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 700, color: colors.headingBright }}>
                {s.category}
              </span>
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                {formatUtcDate(s.date)} · {formatKm(s.odometerKm)}
              </span>
              {s.workOrderId && <MonoTag color={statusMeta("ontime").t}>closed {s.workOrderId}</MonoTag>}
              {s.costCad != null && (
                <span style={{ marginLeft: "auto", fontFamily: fonts.mono, fontSize: 12.5, color: colors.textSecondary }}>
                  {formatCad(s.costCad)}
                </span>
              )}
            </div>

            <Field label="Who">{s.performedBy}</Field>
            <Field label="What changed">
              <span>{s.itemsChanged.join(" · ")}</span>
            </Field>
            <Field label="Why">{s.reason}</Field>
            {s.partsUsed.length > 0 && (
              <Field label="Parts">
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5 }}>
                  {s.partsUsed.map((p) => `${p.sku}×${p.qty}`).join(", ")}
                </span>
              </Field>
            )}
            {s.laborHours != null && <Field label="Labour">{s.laborHours} h</Field>}
          </Panel>
        ))
      )}

      {open && (
        <ServiceRecordModal
          unit={unit}
          odometerKm={odometerKm}
          onClose={() => setOpen(false)}
          onSaved={() => undefined}
        />
      )}
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "96px 1fr", gap: 10, padding: "3px 0" }}>
      <span
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 10,
          letterSpacing: ".08em",
          textTransform: "uppercase",
          color: colors.textLabel,
          paddingTop: 1,
        }}
      >
        {label}
      </span>
      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.5 }}>
        {children}
      </span>
    </div>
  );
}
