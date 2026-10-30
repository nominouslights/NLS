"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { recordDutyEvent, type DutyEventInput, type EventSourceWire } from "@/lib/api/hos";
import { HosWire } from "@/lib/hos/engine";
import { HOS_CALENDAR, isoOfUnix } from "@/lib/hos/display";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";

// Hours the driver worked for another carrier on a day (s.82(1)(f) — declared, untimed).
// The backend has no separate endpoint: the declaration is laid out from the day's start as
// ordinary duty events with source "Other carrier (declared)" — On Duty → Driving → Off Duty —
// and posted one at a time through POST …/hos/duty-events. Each event keeps its own eventId
// for the life of the modal, so a retry after a failed POST replays rather than duplicates.

export const OTHER_CARRIER_SOURCE: EventSourceWire = HosWire.OtherCarrierSource as EventSourceWire;

/** The longest declarable day: 23h59m, because an Off Duty event at 24h falls on the next day. */
export const OTHER_CARRIER_MAX_MINUTES = 24 * 60 - 1;

export interface OtherCarrierDraft {
  date: string;
  carrierName: string;
  onDutyMinutes: string;
  drivingMinutes: string;
}

function wholeMinutes(raw: string): number | null {
  const n = Number(raw);
  return raw.trim() === "" || !Number.isInteger(n) || n < 0 ? null : n;
}

/** Null when the declaration may be sent; otherwise the first problem. */
export function validateOtherCarrier(draft: OtherCarrierDraft): string | null {
  if (!draft.date) return "Enter the date the driver worked for the other carrier.";
  if (!draft.carrierName.trim()) return "Enter the other carrier's name.";
  const onDuty = wholeMinutes(draft.onDutyMinutes);
  const driving = wholeMinutes(draft.drivingMinutes);
  if (onDuty === null || onDuty < 1) return "Enter the on-duty time in whole minutes (at least 1).";
  if (onDuty > OTHER_CARRIER_MAX_MINUTES) return `On-duty time cannot exceed ${OTHER_CARRIER_MAX_MINUTES} minutes in one day.`;
  if (driving === null) return "Enter the driving time in whole minutes (0 when none).";
  if (driving > onDuty) return "Driving time cannot exceed on-duty time.";
  return null;
}

/**
 * The events a valid draft becomes, in posting order, from the day's start (America/Winnipeg):
 * On Duty at 00:00 (when some on-duty time was not driving), Driving after the non-driving
 * on-duty time, Off Duty when the on-duty time ends. `newId` supplies each event's id.
 */
export function otherCarrierEvents(draft: OtherCarrierDraft, note: string, newId: () => string): DutyEventInput[] {
  const dayStart = HOS_CALENDAR.dayStartUnix(draft.date);
  const onDuty = Number(draft.onDutyMinutes);
  const driving = Number(draft.drivingMinutes);
  const nonDriving = onDuty - driving;
  const base = { source: OTHER_CARRIER_SOURCE, carrierName: draft.carrierName.trim(), note: note.trim() || null } as const;
  const events: DutyEventInput[] = [];
  if (nonDriving > 0) events.push({ eventId: newId(), atUtc: isoOfUnix(dayStart), status: "On Duty", ...base });
  if (driving > 0) events.push({ eventId: newId(), atUtc: isoOfUnix(dayStart + nonDriving * 60), status: "Driving", ...base });
  events.push({ eventId: newId(), atUtc: isoOfUnix(dayStart + onDuty * 60), status: "Off Duty", ...base });
  return events;
}

export default function OtherCarrierHoursModal({
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
  // Three ids minted once per open — a retry replays the same events (200), never duplicates.
  const [ids] = useState(() => [crypto.randomUUID(), crypto.randomUUID(), crypto.randomUUID()]);
  const [date, setDate] = useState(defaultDate);
  const [carrierName, setCarrierName] = useState("");
  const [onDutyMinutes, setOnDutyMinutes] = useState("");
  const [drivingMinutes, setDrivingMinutes] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const draft: OtherCarrierDraft = { date, carrierName, onDutyMinutes, drivingMinutes };
  const problem = validateOtherCarrier(draft);

  async function submit() {
    if (busy) return;
    if (problem) return setError(problem);
    let next = 0;
    const events = otherCarrierEvents(draft, note, () => ids[next++]);

    setBusy(true);
    setError(null);
    try {
      // In order: the day's events must arrive chronologically for the no-same-status rule.
      for (const event of events) {
        await recordDutyEvent(driverId, event);
      }
      onSaved(date);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to record the other carrier's hours — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title="Other Carrier Hours"
      onClose={onClose}
      error={error}
      maxWidth={620}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "RECORDING…" : "RECORD HOURS"}
          </ActionButton>
        </>
      }
    >
      <div style={{ marginBottom: 14 }}>
        <StatusChip kind="info" glyph="⇄" label={`Recorded as ${OTHER_CARRIER_SOURCE} events from the day's start — they count against the driver's day and cycle`} />
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
        <DateField label="Date" value={date} onChange={setDate} />
        <TextField label="Carrier" value={carrierName} onChange={setCarrierName} placeholder="The other carrier's name" />
        <NumberField label="On duty (minutes)" value={onDutyMinutes} onChange={setOnDutyMinutes} min={1} step={1} placeholder="Including driving" />
        <NumberField label="Driving (minutes)" value={drivingMinutes} onChange={setDrivingMinutes} min={0} step={1} placeholder="0 when none" />
      </div>
      <TextField label="Note (optional)" value={note} onChange={setNote} placeholder="Where the figures came from" />
      {problem && (onDutyMinutes || drivingMinutes || carrierName) && (
        <div style={{ marginTop: 10 }}>
          <StatusChip kind="soon" label={problem} />
        </div>
      )}
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 12, lineHeight: 1.5 }}>
        The declaration is untimed; the engine lays it out from the day&apos;s start and counts it conservatively.
      </div>
    </ModalShell>
  );
}
