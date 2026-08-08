"use client";

import { useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createInspection,
  type DefectSeverityWire,
  type InspectionResultWire,
  type InspectionType,
} from "@/lib/api/maintenance";
import { DVIR_CHECKLIST } from "@/lib/maintenanceStore";
import { DEFECT_SEVERITY_LABEL, INSPECTION_RESULT_META } from "@/lib/workOrderDisplay";
import { listDrivers } from "@/lib/api/drivers";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const SEVERITIES: DefectSeverityWire[] = ["Minor", "Major", "OutOfService"];

interface Row {
  item: string;
  defect: boolean;
  severity: DefectSeverityWire;
  note: string;
}

// Dispatcher data-entry for a pre/post-trip DVIR — the office fallback for when
// the Driver Field App fails in the field and the driver uses a paper backup form.
// Saves to the live Fleet API (source "Dispatcher"), which also advances the
// vehicle odometer.

export default function InspectionEntryModal({
  vehicleId,
  unit,
  type,
  odometerKm,
  onClose,
  onSaved,
}: {
  vehicleId: string;
  unit: string;
  type: InspectionType;
  odometerKm: number;
  onClose: () => void;
  onSaved: (inspectionId: string) => void;
}) {
  const typeLabel = type === "PreTrip" ? "Pre-Trip" : "Post-Trip";

  // Driver roster from the real Drivers API (Active drivers only).
  const [driverNames, setDriverNames] = useState<string[]>([]);
  const [driver, setDriver] = useState("");

  useEffect(() => {
    let active = true;
    listDrivers().then(
      (rows) => {
        if (active) {
          const names = rows.filter((d) => d.status === "Active").map((d) => d.name);
          setDriverNames(names);
          setDriver((cur) => cur || names[0] || "");
        }
      },
      () => {
        // Roster unavailable — the driver select stays empty and submit's
        // "select the driver" validation surfaces the problem.
      },
    );
    return () => {
      active = false;
    };
  }, []);
  const [odo, setOdo] = useState(String(odometerKm));
  const [rows, setRows] = useState<Row[]>(
    DVIR_CHECKLIST.map((item) => ({ item, defect: false, severity: "Minor", note: "" })),
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function setRow(i: number, patch: Partial<Row>) {
    setRows((prev) => prev.map((r, idx) => (idx === i ? { ...r, ...patch } : r)));
  }

  const defects = rows
    .filter((r) => r.defect)
    .map((r) => ({ item: r.item, severity: r.severity, note: r.note.trim() || null }));
  const result: InspectionResultWire = defects.some(
    (d) => d.severity === "OutOfService" || d.severity === "Major",
  )
    ? "Fail"
    : defects.length > 0
      ? "PassWithDefects"
      : "Pass";

  async function submit() {
    if (busy) return;
    if (!driver) return setError("Select the driver who performed the inspection.");
    setBusy(true);
    setError(null);
    try {
      const id = await createInspection({
        type,
        source: "Dispatcher",
        vehicleId,
        unit,
        driverName: driver,
        enteredBy: "Dispatch",
        odometerKm: parseInt(odo, 10) || odometerKm,
        checklist: rows.map((r) => ({ item: r.item, passed: !r.defect })),
        defects,
      });
      onSaved(id);
      onClose();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to save the inspection — please try again.");
    }
  }

  const resultMeta = statusMeta(INSPECTION_RESULT_META[result].kind);

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${unit} · DVIR`}
      title={`Enter ${typeLabel} Inspection`}
      onClose={onClose}
      error={error}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            Result:{" "}
            <span style={{ color: resultMeta.t, fontWeight: 700 }}>
              {resultMeta.g} {INSPECTION_RESULT_META[result].label}
            </span>
          </span>
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "SAVE INSPECTION"}
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
        <span style={{ fontWeight: 600 }}> Dispatcher</span> entry.
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
                  onChange={(v) => setRow(i, { severity: v as DefectSeverityWire })}
                  options={SEVERITIES.map((s) => ({ value: s, label: DEFECT_SEVERITY_LABEL[s] }))}
                />
                <TextField label="Note" value={r.note} onChange={(v) => setRow(i, { note: v })} placeholder="Describe the defect" />
              </div>
            )}
          </div>
        ))}
      </div>

      {defects.some((d) => d.severity === "OutOfService" || d.severity === "Major") && (
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
