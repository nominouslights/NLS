"use client";

import { useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type {
  DefectSeverity,
  Inspection,
  InspectionChecklistItem,
  InspectionType,
} from "@/lib/types";
import { addInspection, DVIR_CHECKLIST, resultForDefects } from "@/lib/maintenanceStore";
import { drivers } from "@/lib/data";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const SEVERITIES: DefectSeverity[] = ["Minor", "Major", "Out-of-Service"];

interface Row {
  item: string;
  defect: boolean;
  severity: DefectSeverity;
  note: string;
}

// Dispatcher data-entry for a pre/post-trip DVIR — the office fallback for when
// the Driver Field App fails in the field and the driver uses a paper backup form.

export default function InspectionEntryModal({
  unit,
  type,
  odometerKm,
  onClose,
  onSaved,
}: {
  unit: string;
  type: InspectionType;
  odometerKm: number;
  onClose: () => void;
  onSaved: (inspection: Inspection) => void;
}) {
  const driverNames = drivers.map((d) => d.name);
  const [driver, setDriver] = useState(driverNames[0] ?? "");
  const [odo, setOdo] = useState(String(odometerKm));
  const [rows, setRows] = useState<Row[]>(
    DVIR_CHECKLIST.map((item) => ({ item, defect: false, severity: "Minor", note: "" })),
  );
  const [error, setError] = useState<string | null>(null);

  function setRow(i: number, patch: Partial<Row>) {
    setRows((prev) => prev.map((r, idx) => (idx === i ? { ...r, ...patch } : r)));
  }

  const defects = rows
    .filter((r) => r.defect)
    .map((r) => ({ item: r.item, severity: r.severity, note: r.note.trim() || undefined }));
  const result = resultForDefects(defects);

  function submit() {
    if (!driver) return setError("Select the driver who performed the inspection.");
    const checklist: InspectionChecklistItem[] = rows.map((r) =>
      r.defect
        ? { item: r.item, status: "Defect", severity: r.severity, note: r.note.trim() || undefined }
        : { item: r.item, status: "Pass" },
    );
    const insp = addInspection({
      unit,
      type,
      driver,
      source: "Dispatcher Entry (paper backup)",
      enteredBy: "Dispatch",
      performedAt: new Date().toISOString().slice(0, 10),
      odometerKm: parseInt(odo, 10) || odometerKm,
      result,
      defects,
      checklist,
    });
    onSaved(insp);
    onClose();
  }

  const resultMeta = statusMeta(
    result === "Pass" ? "ontime" : result === "Fail" ? "over" : "soon",
  );

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${unit} · DVIR`}
      title={`Enter ${type} Inspection`}
      onClose={onClose}
      error={error}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            Result:{" "}
            <span style={{ color: resultMeta.t, fontWeight: 700 }}>
              {resultMeta.g} {result}
            </span>
          </span>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit}>
            SAVE INSPECTION
          </ActionButton>
        </>
      }
    >
      {/* paper-backup context banner */}
      <div
        style={{
          padding: "11px 14px",
          background: "rgba(31,111,178,.07)",
          border: `1px solid ${colors.borderActive}`,
          borderRadius: 10,
          marginBottom: 16,
          fontFamily: fonts.body,
          fontSize: 12,
          color: colors.textSecondary,
          lineHeight: 1.5,
        }}
      >
        Dispatcher paper-backup entry — use when the Driver App failed in the field and the driver
        recorded this DVIR on the backup paper form. Recorded as
        <span style={{ fontWeight: 600 }}> Dispatcher Entry (paper backup)</span>.
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 16 }}>
        <SelectField
          label="Driver"
          value={driver}
          onChange={setDriver}
          options={driverNames.map((n) => ({ value: n, label: n }))}
        />
        <NumberField label="Odometer (km)" value={odo} onChange={setOdo} min={0} step={1} />
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {rows.map((r, i) => (
          <div
            key={r.item}
            style={{ padding: "9px 11px", ...rowSurface(r.defect, r.defect ? statusMeta("over").c : colors.blue), cursor: "default" }}
          >
            <div style={{ display: "grid", gridTemplateColumns: "1fr 150px", gap: 10, alignItems: "center" }}>
              <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                {r.item}
              </span>
              <div style={{ display: "flex", gap: 6, justifyContent: "flex-end" }}>
                <Toggle active={!r.defect} label="Pass" kind="ontime" onClick={() => setRow(i, { defect: false })} />
                <Toggle active={r.defect} label="Defect" kind="over" onClick={() => setRow(i, { defect: true })} />
              </div>
            </div>
            {r.defect && (
              <div style={{ display: "grid", gridTemplateColumns: "170px 1fr", gap: 10, marginTop: 9 }}>
                <SelectField
                  label="Severity"
                  value={r.severity}
                  onChange={(v) => setRow(i, { severity: v as DefectSeverity })}
                  options={SEVERITIES.map((s) => ({ value: s, label: s }))}
                />
                <TextField label="Note" value={r.note} onChange={(v) => setRow(i, { note: v })} placeholder="Describe the defect" />
              </div>
            )}
          </div>
        ))}
      </div>

      {defects.some((d) => d.severity === "Out-of-Service" || d.severity === "Major") && (
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: statusMeta("over").t, marginTop: 12, fontWeight: 600 }}>
          ▲ Major / out-of-service defect present — you&apos;ll be prompted to generate a work order after saving.
        </div>
      )}
    </ModalShell>
  );
}

function Toggle({
  active,
  label,
  kind,
  onClick,
}: {
  active: boolean;
  label: string;
  kind: "ontime" | "over";
  onClick: () => void;
}) {
  const m = statusMeta(kind);
  return (
    <span
      onClick={onClick}
      style={{
        fontFamily: fonts.body,
        fontWeight: 600,
        fontSize: 11.5,
        padding: "4px 11px",
        borderRadius: 7,
        cursor: "pointer",
        border: `1px solid ${active ? m.bd : colors.borderSubtle}`,
        background: active ? m.bg : "transparent",
        color: active ? m.t : colors.textDim,
      }}
    >
      {active ? `${m.g} ` : ""}
      {label}
    </span>
  );
}
