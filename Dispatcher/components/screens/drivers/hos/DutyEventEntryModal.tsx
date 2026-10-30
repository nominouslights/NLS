"use client";

import { useMemo, useState } from "react";
import { colors, dutyMeta, fonts, type DutyStatus as DutyState } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { transcribeDay, type DutyStatusWire, type TranscribeDayInput, type TranscriptionSource } from "@/lib/api/hos";
import { DUTY_STATES, formatRemaining, minutesOf, previewDay } from "@/lib/hos/display";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, FieldLabel, SelectField, TextField, TimeField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";

// Transcribe one duty day the driver app did not capture — an ELD printout or a paper log —
// as an ordered list of (HH:mm, status, note) rows: POST /api/drivers/{id}/hos/days/{date}/
// transcribe, all-or-nothing. The source is ELD transcription or Manual (paper backup) ONLY:
// a dispatcher's entry is definitionally not a Driver App record, so that source is never
// offered. The live preview runs the engine mirror on the rows alone (a synthetic one-day
// ledger) and shows the derived totals plus any violation the server will record; the
// dispatcher must acknowledge a violation before the POST.

export const TRANSCRIPTION_SOURCES: TranscriptionSource[] = ["ELD transcription", "Manual (paper backup)"];

export interface EntryRow {
  time: string;
  status: DutyState;
  note: string;
}

/** The four rows the convenience fields produce, in order. */
export function quickFillRows(onDutyStart: string, driveStart: string, driveStop: string, onDutyEnd: string): EntryRow[] {
  return [
    { time: onDutyStart, status: "On Duty", note: "" },
    { time: driveStart, status: "Driving", note: "" },
    { time: driveStop, status: "On Duty", note: "" },
    { time: onDutyEnd, status: "Off Duty", note: "" },
  ];
}

/** Null when the rows are valid; otherwise the first problem, row-numbered. */
export function validateRows(rows: readonly EntryRow[]): string | null {
  if (rows.length === 0) return "Add at least one duty row.";
  let last = -1;
  for (let i = 0; i < rows.length; i++) {
    const m = minutesOf(rows[i].time);
    if (m === null) return `Row ${i + 1}: enter a time (HH:MM).`;
    if (m <= last) return `Row ${i + 1}: times must be strictly increasing.`;
    last = m;
  }
  return null;
}

export default function DutyEventEntryModal({
  driverId,
  driverName,
  defaultDate,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  defaultDate: string;
  onClose: () => void;
  onSaved: (localDate: string) => void;
}) {
  // One id per open: a retry after a network failure replays rather than duplicates.
  const [transcriptionId] = useState(() => crypto.randomUUID());
  const [date, setDate] = useState(defaultDate);
  const [source, setSource] = useState<TranscriptionSource>("ELD transcription");
  const [enteredBy, setEnteredBy] = useState("");
  const [carrierName, setCarrierName] = useState("");
  const [onDutyStart, setOnDutyStart] = useState("");
  const [driveStart, setDriveStart] = useState("");
  const [driveStop, setDriveStop] = useState("");
  const [onDutyEnd, setOnDutyEnd] = useState("");
  const [rows, setRows] = useState<EntryRow[]>([]);
  const [acknowledged, setAcknowledged] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const rowsError = validateRows(rows);
  const preview = useMemo(() => (rows.length > 0 && rowsError === null && date ? previewDay(date, rows) : null), [date, rows, rowsError]);
  const previewDayTotals = preview?.days.find((d) => d.date === date) ?? null;
  const previewViolations = preview?.violations.filter((v) => v.date === date && !v.suppressed) ?? [];

  function updateRow(i: number, patch: Partial<EntryRow>) {
    setRows((r) => r.map((row, j) => (j === i ? { ...row, ...patch } : row)));
    setAcknowledged(false);
  }

  function fillRows() {
    if (!onDutyStart || !driveStart || !driveStop || !onDutyEnd) {
      return setError("Enter all four times to fill the rows.");
    }
    setError(null);
    setRows(quickFillRows(onDutyStart, driveStart, driveStop, onDutyEnd));
    setAcknowledged(false);
  }

  async function submit() {
    if (busy) return;
    if (!date) return setError("Enter the date for this day.");
    if (!enteredBy.trim()) return setError("Enter who is transcribing this day.");
    if (rowsError) return setError(rowsError);
    if (previewViolations.length > 0 && !acknowledged) {
      return setError("The server will record the violation shown below — tick the acknowledgement to continue.");
    }

    const input: TranscribeDayInput = {
      transcriptionId,
      source,
      enteredBy: enteredBy.trim(),
      carrierName: carrierName.trim() || null,
      entries: rows.map((r) => ({ time: r.time, status: r.status as DutyStatusWire, note: r.note.trim() || null })),
    };

    setBusy(true);
    setError(null);
    try {
      const res = await transcribeDay(driverId, date, input);
      onSaved(res.localDate);
      onClose();
    } catch (e) {
      // All-or-nothing on the server: nothing was written, so the message is the whole story.
      setError(e instanceof ApiError ? e.message : "Failed to record the day — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title="Transcribe Duty Day"
      onClose={onClose}
      error={error}
      maxWidth={760}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "RECORDING…" : "RECORD DAY"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <DateField label="Date" value={date} onChange={(v) => { setDate(v); setAcknowledged(false); }} />
        <SelectField
          label="Source"
          value={source}
          onChange={(v) => setSource(v as TranscriptionSource)}
          options={TRANSCRIPTION_SOURCES.map((s) => ({ value: s, label: s }))}
          hint={<span style={{ color: colors.textFaint }}>— never a Driver App record</span>}
        />
        <TextField label="Entered by" value={enteredBy} onChange={setEnteredBy} placeholder="Dispatch — your name" />
        <TextField label="Carrier (optional)" value={carrierName} onChange={setCarrierName} placeholder="Only for another carrier's hours" />
      </div>

      <div style={{ marginTop: 18 }}>
        <FieldLabel>Quick fill — one shift, four rows</FieldLabel>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr auto", gap: 10, alignItems: "end" }}>
          <TimeField label="On-duty start" value={onDutyStart} onChange={setOnDutyStart} />
          <TimeField label="Drive start" value={driveStart} onChange={setDriveStart} />
          <TimeField label="Drive stop" value={driveStop} onChange={setDriveStop} />
          <TimeField label="On-duty end" value={onDutyEnd} onChange={setOnDutyEnd} />
          <ActionButton onClick={fillRows} style={{ height: 40 }}>
            FILL ROWS
          </ActionButton>
        </div>
      </div>

      <div style={{ marginTop: 18 }}>
        <div style={{ display: "flex", alignItems: "center", marginBottom: 6 }}>
          <FieldLabel>Duty rows (local time, in order)</FieldLabel>
          <ActionButton style={{ marginLeft: "auto" }} onClick={() => { setRows((r) => [...r, { time: "", status: "On Duty", note: "" }]); setAcknowledged(false); }}>
            + ADD ROW
          </ActionButton>
        </div>
        {rows.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>No rows yet — fill from the shift times above or add rows one at a time.</div>
        ) : (
          <div data-testid="entry-rows">
            {rows.map((row, i) => {
              const m = dutyMeta(row.status);
              return (
                <div
                  key={i}
                  data-testid="entry-row"
                  style={{
                    display: "grid",
                    gridTemplateColumns: "120px 190px 1fr auto",
                    gap: 10,
                    alignItems: "end",
                    padding: "6px 0",
                    borderTop: i === 0 ? undefined : `1px solid ${colors.borderSubtle}`,
                  }}
                >
                  <TimeField label={`Row ${i + 1}`} value={row.time} onChange={(v) => updateRow(i, { time: v })} />
                  <SelectField
                    label="Status"
                    value={row.status}
                    onChange={(v) => updateRow(i, { status: v as DutyState })}
                    options={DUTY_STATES.map((s) => ({ value: s, label: `${dutyMeta(s).glyph} ${s}` }))}
                    hint={<span style={{ color: m.text }}>{m.glyph}</span>}
                  />
                  <TextField label="Note (optional)" value={row.note} onChange={(v) => updateRow(i, { note: v })} />
                  <span
                    onClick={() => { setRows((r) => r.filter((_, j) => j !== i)); setAcknowledged(false); }}
                    style={{ fontFamily: fonts.semiCondensed, fontWeight: 600, fontSize: 10, letterSpacing: ".08em", textTransform: "uppercase", color: colors.textDim, cursor: "pointer", paddingBottom: 12 }}
                  >
                    Remove
                  </span>
                </div>
              );
            })}
          </div>
        )}
        {rows.length > 0 && rowsError && (
          <div style={{ marginTop: 8 }}>
            <StatusChip kind="soon" label={rowsError} />
          </div>
        )}
      </div>

      {previewDayTotals && (
        <div style={{ marginTop: 18, padding: "12px 14px", borderRadius: 10, background: colors.cardBg, border: `1px solid ${colors.borderSubtle}` }}>
          <FieldLabel>Preview — what the engine derives from these rows</FieldLabel>
          <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>
            Driving {formatRemaining(previewDayTotals.drivingSeconds)} · On duty {formatRemaining(previewDayTotals.onDutySeconds)} · Off duty{" "}
            {formatRemaining(previewDayTotals.offDutySeconds)}
          </div>
          {previewViolations.length === 0 ? (
            <div style={{ marginTop: 8 }}>
              <StatusChip kind="ontime" label="No violation on this day" />
            </div>
          ) : (
            <div style={{ marginTop: 8, display: "flex", flexDirection: "column", gap: 6 }}>
              {previewViolations.map((v, i) => (
                <StatusChip key={`${v.rule}-${i}`} kind="over" label={v.message} />
              ))}
              <label style={{ display: "flex", alignItems: "center", gap: 8, fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary, cursor: "pointer" }}>
                <input type="checkbox" checked={acknowledged} onChange={(e) => setAcknowledged(e.target.checked)} />
                The server will record this violation — continue?
              </label>
            </div>
          )}
        </div>
      )}
    </ModalShell>
  );
}
