"use client";

import { useState } from "react";
import type { LeaveType } from "@/lib/types";
import { addLeave } from "@/lib/driverStore";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

const LEAVE_TYPES: LeaveType[] = ["Vacation", "Sick", "Leave Without Pay"];

// Add a leave record (vacation / sick / unpaid) for a driver. Add-only,
// mirroring ClearanceFormModal — writes to lib/driverStore.

export default function LeaveFormModal({
  driverId,
  driverName,
  onClose,
}: {
  driverId: number;
  driverName: string;
  onClose: () => void;
}) {
  const [type, setType] = useState<LeaveType>(LEAVE_TYPES[0]);
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [hours, setHours] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  function submit() {
    if (!startDate) return setError("Enter a start date.");
    if (!endDate) return setError("Enter an end date.");
    if (endDate < startDate) return setError("End date must be on or after start date.");

    addLeave({
      driverId,
      type,
      startDate,
      endDate,
      hours: hours ? Number(hours) : undefined,
      note: note.trim() || undefined,
    });
    onClose();
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Leave`}
      title="Add Leave"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit}>
            ADD LEAVE
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <SelectField
          label="Type"
          value={type}
          onChange={(v) => setType(v as LeaveType)}
          options={LEAVE_TYPES.map((t) => ({ value: t, label: t }))}
        />
        <NumberField label="Hours (optional)" value={hours} onChange={setHours} min={0} step={0.5} placeholder="8" />
        <DateField label="Start date" value={startDate} onChange={setStartDate} />
        <DateField label="End date" value={endDate} onChange={setEndDate} />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextField label="Note (optional)" value={note} onChange={setNote} placeholder="Approved by dispatch" />
      </div>
    </ModalShell>
  );
}
