"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { logPmCompletion, type PmEntryStatusWire } from "@/lib/api/pm";
import { listVehicleWorkOrders, type WorkOrderWire } from "@/lib/api/maintenance";
import { OPEN_WIRE_STATUSES } from "@/lib/workOrderDisplay";
import { KIND_LABEL, TASK_LABEL, pmIntervalLabel } from "@/lib/pmDisplay";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { MonoTag } from "@/components/ui/Chip";

// Log one PM completion against a plan entry (item or overhaul) — the
// append-only per-unit service record that drives the computed due schedule.
// The entry's kind travels automatically from the selected plan line; an
// optional link to one of the vehicle's OPEN work orders can be attached
// (open ones only — a completed WO is already closed out via its own flow).

function todayIso(): string {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export default function RecordPmCompletionModal({
  vehicleId,
  unit,
  currentOdometerKm,
  entries,
  prefillCode,
  onClose,
  onSaved,
}: {
  vehicleId: string;
  unit: string;
  currentOdometerKm: number | null;
  entries: PmEntryStatusWire[];
  prefillCode?: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [code, setCode] = useState(
    prefillCode && entries.some((e) => e.code === prefillCode) ? prefillCode : entries[0]?.code ?? "",
  );
  const [performedAt, setPerformedAt] = useState(todayIso());
  const [odo, setOdo] = useState(currentOdometerKm != null ? String(currentOdometerKm) : "");
  const [performedBy, setPerformedBy] = useState("");
  const [measurement, setMeasurement] = useState("");
  const [notes, setNotes] = useState("");
  const [workOrderId, setWorkOrderId] = useState("");
  const [workOrders, setWorkOrders] = useState<WorkOrderWire[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const entry = entries.find((e) => e.code === code) ?? null;

  useEffect(() => {
    let active = true;
    // Optional work-order link. Fail-soft: on error the select simply offers
    // only "No linked work order".
    listVehicleWorkOrders(vehicleId).then(
      (rows) => {
        if (active) setWorkOrders(rows.filter((w) => OPEN_WIRE_STATUSES.includes(w.status)));
      },
      (e) => {
        console.error("Work orders unavailable:", e);
      },
    );
    return () => {
      active = false;
    };
  }, [vehicleId]);

  async function submit() {
    if (busy) return;
    if (!entry) return setError("Select the plan entry that was serviced.");
    if (!performedAt) return setError("Enter the date the work was performed.");
    // Strict parse — this writes into the append-only completion ledger, so
    // reject anything that is not a plain whole number of km (parseInt would
    // silently read "1e5" as 1 and truncate decimals).
    const odoText = odo.trim();
    const odoNum = Number(odoText);
    if (!odoText || !Number.isInteger(odoNum) || odoNum < 0)
      return setError("Enter the odometer reading at time of service — a whole number of km, 0 or more.");
    if (!performedBy.trim()) return setError("Enter who performed the work (shop or technician).");

    setBusy(true);
    setError(null);
    try {
      await logPmCompletion(vehicleId, {
        code: entry.code,
        kind: entry.kind,
        performedAt,
        odometerKm: odoNum,
        performedBy: performedBy.trim(),
        workOrderId: workOrderId || null,
        measurement: measurement.trim() || null,
        notes: notes.trim() || null,
      });
      onSaved();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to record the service — please try again.");
    }
  }

  const entryOptions = entries.map((e) => ({
    value: e.code,
    label: `${e.code} — ${e.component} (${e.task ? TASK_LABEL[e.task] : KIND_LABEL[e.kind]})`,
  }));

  const woOptions = [
    { value: "", label: "No linked work order" },
    ...workOrders.map((w) => ({ value: w.id, label: `${w.number} — ${w.title}` })),
  ];

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${unit} · Preventive maintenance`}
      title="Record Service"
      onClose={onClose}
      error={error}
      maxWidth={640}
      footer={
        <>
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy || entries.length === 0}>
            {busy ? "SAVING…" : "RECORD SERVICE"}
          </ActionButton>
        </>
      }
    >
      {entries.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
          The assigned plan has no entries to record against.
        </div>
      ) : (
        <>
          <div style={{ marginBottom: 14 }}>
            <SelectField label="Plan entry serviced" value={code} onChange={setCode} options={entryOptions} />
            {entry && (
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 7 }}>
                <MonoTag color={entry.kind === "Overhaul" ? colors.amberText : undefined}>
                  {KIND_LABEL[entry.kind].toUpperCase()}
                </MonoTag>
                <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                  {entry.system} · every {pmIntervalLabel(entry)}
                </span>
              </div>
            )}
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
            <DateField label="Performed on" value={performedAt} onChange={setPerformedAt} />
            <NumberField
              label="Odometer at service (km)"
              value={odo}
              onChange={setOdo}
              min={0}
              step={1}
              placeholder={currentOdometerKm != null ? String(currentOdometerKm) : "0"}
            />
            <TextField
              label="Performed by"
              value={performedBy}
              onChange={setPerformedBy}
              placeholder="e.g. Thompson Certified Shop — R. Dumas"
            />
            <SelectField label="Linked work order (optional)" value={workOrderId} onChange={setWorkOrderId} options={woOptions} />
          </div>

          <div style={{ marginTop: 14, display: "flex", flexDirection: "column", gap: 14 }}>
            <TextField
              label="Measurement (optional)"
              value={measurement}
              onChange={setMeasurement}
              placeholder='e.g. "Front pads 7 mm / rear 6 mm" — condition evidence for overhaul-early decisions'
            />
            <TextAreaField label="Notes (optional)" value={notes} onChange={setNotes} rows={2} />
          </div>
        </>
      )}
    </ModalShell>
  );
}
