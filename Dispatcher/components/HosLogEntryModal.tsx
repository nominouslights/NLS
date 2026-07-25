"use client";

import { useState } from "react";
import type { DutyStatus } from "@/lib/types";
import { ApiError } from "@/lib/api";
import { addDriverHosEntry } from "@/lib/api/drivers";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextField, TimeField, DateField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const DUTY_OPTIONS: DutyStatus[] = ["Off Duty", "On Duty", "Driving"];

// Log a manual (paper backup) HOS entry — for spans the driver app didn't
// capture (offline, contractor paper log, etc). POST
// /api/drivers/{driverId}/hos (Drivers module). Source is set server-side to
// "Manual (paper backup)": dispatcher-entered HOS is definitionally not a
// Driver App entry, so it isn't a form field and isn't sent.
//
// On-duty / driving hours are entered as clock times (on-duty start, drive
// start, drive stop, on-duty end — all on the same day) and the hour totals
// are derived from those, rather than typed directly. The on-duty ↔ drive
// gaps capture pre-trip (on-duty start → drive start) and post-trip (drive
// stop → on-duty end) time. Off-duty hours isn't entered at all — it's
// derived as the rest of the 24h day not spent on duty.

/** "HH:MM" → minutes since midnight. */
function toMinutes(t: string): number {
  const [h, m] = t.split(":").map(Number);
  return h * 60 + m;
}

/** Hours between two "HH:MM" clock times, same-day only, rounded to 1dp. */
function hoursBetween(start: string, end: string): number {
  return Math.round(((toMinutes(end) - toMinutes(start)) / 60) * 10) / 10;
}

/** Rest of the 24h day not spent on duty, rounded to 1dp, floored at 0. */
function offDutyHoursFor(onDutyH: number): number {
  return Math.round(Math.max(24 - onDutyH, 0) * 10) / 10;
}

export default function HosLogEntryModal({
  driverId,
  driverName,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  onClose: () => void;
  onSaved: (newId: string) => void;
}) {
  const [date, setDate] = useState("");
  const [duty, setDuty] = useState<DutyStatus>("On Duty");
  const [onDutyStart, setOnDutyStart] = useState("");
  const [driveStart, setDriveStart] = useState("");
  const [driveStop, setDriveStop] = useState("");
  const [onDutyEnd, setOnDutyEnd] = useState("");
  const [enteredBy, setEnteredBy] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!date) return setError("Enter the date for this entry.");
    if (!onDutyStart) return setError("Enter the on-duty start time.");
    if (!driveStart) return setError("Enter the drive start time.");
    if (!driveStop) return setError("Enter the drive stop time.");
    if (!onDutyEnd) return setError("Enter the on-duty end time.");
    if (toMinutes(driveStart) < toMinutes(onDutyStart)) {
      return setError("Drive start must be on or after on-duty start.");
    }
    if (toMinutes(driveStop) < toMinutes(driveStart)) {
      return setError("Drive stop must be on or after drive start.");
    }
    if (toMinutes(onDutyEnd) < toMinutes(driveStop)) {
      return setError("On-duty end must be on or after drive stop.");
    }
    if (!enteredBy.trim()) return setError("Enter who's logging this entry.");

    const onDutyH = hoursBetween(onDutyStart, onDutyEnd);

    setBusy(true);
    setError(null);
    try {
      const newId = await addDriverHosEntry(driverId, {
        date,
        duty,
        onDutyH,
        drivingH: hoursBetween(driveStart, driveStop),
        offDutyH: offDutyHoursFor(onDutyH),
        enteredBy: enteredBy.trim(),
        note: note.trim() || null,
      });
      onSaved(newId);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to log the entry — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title="Log HOS Entry"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "LOGGING…" : "LOG ENTRY"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <DateField label="Date" value={date} onChange={setDate} />
        <SelectField
          label="Duty status"
          value={duty}
          onChange={(v) => setDuty(v as DutyStatus)}
          options={DUTY_OPTIONS.map((d) => ({ value: d, label: d }))}
        />
        <TimeField label="On-duty start" value={onDutyStart} onChange={setOnDutyStart} />
        <TimeField label="Drive start" value={driveStart} onChange={setDriveStart} />
        <TimeField label="Drive stop" value={driveStop} onChange={setDriveStop} />
        <TimeField label="On-duty end" value={onDutyEnd} onChange={setOnDutyEnd} />
        <TextField label="Entered by" value={enteredBy} onChange={setEnteredBy} placeholder="Dispatch — your name" />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextField label="Note (optional)" value={note} onChange={setNote} placeholder="App offline in Leaf Rapids — logged on paper" />
      </div>
    </ModalShell>
  );
}
