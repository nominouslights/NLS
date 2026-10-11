"use client";

import { useState } from "react";
import { colors, dutyMeta, fonts, type DutyStatus as DutyState } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { amendDutyEvent, type AmendDutyEventInput, type DutyDaySummary, type DutyEventRecord, type DutyStatusWire, type TranscriptionSource } from "@/lib/api/hos";
import { DUTY_STATES, dutyStateFromWire, formatClockUtc, formatDateOnly, localTimeToUnix } from "@/lib/hos/display";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField, TextField, TimeField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { TRANSCRIPTION_SOURCES } from "./DutyEventEntryModal";

// Amend one duty event: POST /api/drivers/{id}/hos/duty-events/{eventId}/amend. The original
// is voided and a replacement (new time / status / note, same local day) takes its place; the
// reason is mandatory and travels with the replacement, and a certified day falls back to
// "recertification required" — the driver, not the console, recertifies.

function localTimeOf(iso: string): string {
  const t = formatClockUtc(iso);
  return t === "—" ? "" : t;
}

export default function AmendEventModal({
  driverId,
  driverName,
  day,
  event,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  day: DutyDaySummary;
  event: DutyEventRecord;
  onClose: () => void;
  onSaved: (localDate: string, recertificationRequired: boolean) => void;
}) {
  const [replacementEventId] = useState(() => crypto.randomUUID());
  const [time, setTime] = useState(() => localTimeOf(event.atUtc));
  const [status, setStatus] = useState<DutyState>(() => dutyStateFromWire(event.status));
  const [source, setSource] = useState<TranscriptionSource>(() =>
    TRANSCRIPTION_SOURCES.includes(event.source as TranscriptionSource) ? (event.source as TranscriptionSource) : "Manual (paper backup)",
  );
  const [enteredBy, setEnteredBy] = useState(event.enteredBy ?? "");
  const [note, setNote] = useState(event.note ?? "");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const original = dutyMeta(dutyStateFromWire(event.status));

  async function submit() {
    if (busy) return;
    if (!reason.trim()) return setError("Enter the reason for this amendment.");
    const atUnix = time ? localTimeToUnix(day.date, time) : null;
    if (atUnix === null) return setError("Enter the corrected time (HH:MM).");

    const input: AmendDutyEventInput = {
      replacementEventId,
      atUtc: new Date(atUnix * 1000).toISOString(),
      status: status as DutyStatusWire,
      reason: reason.trim(),
      source,
      enteredBy: enteredBy.trim() || null,
      note: note.trim() || null,
    };

    setBusy(true);
    setError(null);
    try {
      const res = await amendDutyEvent(driverId, event.eventId, input);
      onSaved(res.localDate, res.recertificationRequired);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to amend the event — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service · ${formatDateOnly(day.date)}`}
      title="Amend Duty Event"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "AMENDING…" : "AMEND EVENT"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14, fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
        <span>Original</span>
        <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>{localTimeOf(event.atUtc) || "—"}</span>
        <span style={{ display: "inline-flex", alignItems: "center", gap: 5, fontWeight: 600, color: original.text }}>
          <span style={{ fontSize: 9, color: original.color }}>{original.glyph}</span>
          {dutyStateFromWire(event.status)}
        </span>
        <span style={{ color: colors.textDim }}>· {event.source}</span>
        {day.certified && <StatusChip kind="soon" glyph="◷" label="Certified day — the driver will need to recertify" />}
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TimeField label="Corrected time" value={time} onChange={setTime} />
        <SelectField
          label="Corrected status"
          value={status}
          onChange={(v) => setStatus(v as DutyState)}
          options={DUTY_STATES.map((s) => ({ value: s, label: `${dutyMeta(s).glyph} ${s}` }))}
        />
        <SelectField
          label="Source"
          value={source}
          onChange={(v) => setSource(v as TranscriptionSource)}
          options={TRANSCRIPTION_SOURCES.map((s) => ({ value: s, label: s }))}
        />
        <TextField label="Entered by" value={enteredBy} onChange={setEnteredBy} placeholder="Dispatch — your name" />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextField label="Note (optional)" value={note} onChange={setNote} />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextAreaField label="Reason for amendment" value={reason} onChange={setReason} placeholder="Paper log reads 08:15, not 08:00" rows={2} />
      </div>
    </ModalShell>
  );
}
