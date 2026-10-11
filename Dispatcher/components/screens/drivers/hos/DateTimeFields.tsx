"use client";

import { DateField, TimeField } from "@/components/ui/Field";
import { localDateTimeOf, localTimeToUnix } from "@/lib/hos/display";

// A local (America/Winnipeg) date + time pair for the compliance modals — the regulation's
// clock, not the browser's. `value` is the pair as typed; `unixOf` resolves it to an
// instant (DST-safe via localTimeToUnix) or null when either half is missing or malformed.

export interface LocalDateTime {
  date: string;
  time: string;
}

export function nowLocal(): LocalDateTime {
  return localDateTimeOf(Math.floor(Date.now() / 1000));
}

export function unixOf(value: LocalDateTime): number | null {
  if (!value.date || !value.time) return null;
  return localTimeToUnix(value.date, value.time);
}

export function DateTimeFields({
  label,
  value,
  onChange,
  hint,
  disabled = false,
}: {
  label: string;
  value: LocalDateTime;
  onChange: (v: LocalDateTime) => void;
  hint?: React.ReactNode;
  disabled?: boolean;
}) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10 }}>
      <DateField label={`${label} — date`} value={value.date} onChange={(date) => onChange({ ...value, date })} hint={hint} disabled={disabled} />
      <TimeField label={`${label} — time (local)`} value={value.time} onChange={(time) => onChange({ ...value, time })} disabled={disabled} />
    </div>
  );
}
