"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { liftOutOfService, recordOutOfService, type OutOfServiceDeclaration, type RecordOutOfServiceInput } from "@/lib/api/hos";
import { formatDateTimeUtc, isoOfUnix } from "@/lib/hos/display";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { DateTimeFields, nowLocal, unixOf, type LocalDateTime } from "./DateTimeFields";

// Record an enforcement out-of-service declaration (POST …/out-of-service, ComplianceAccess).
// Declared-at defaults to now; the period defaults to the regulation's 72 hours, or is
// open-ended (until lifted). One active declaration per driver — the server's 409
// Drivers.Hos.AlreadyOutOfService is shown verbatim. The lift (POST …/lift) needs a reason
// when early; the server decides whether it is early.

export const DEFAULT_OOS_HOURS = 72;

/** The request body for the form's state — exported so the test can pin the shape. */
export function outOfServiceInput(declaredAtUnix: number, hours: number, openEnded: boolean, reason: string, evidence: string): RecordOutOfServiceInput {
  return {
    declaredAtUtc: isoOfUnix(declaredAtUnix),
    reason: reason.trim(),
    ...(openEnded ? { openEnded: true } : { untilUtc: isoOfUnix(declaredAtUnix + Math.round(hours * 3600)) }),
    evidence: evidence.trim() || null,
  };
}

export default function RecordOutOfServiceModal({
  driverId,
  driverName,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  onClose: () => void;
  onSaved: (record: OutOfServiceDeclaration) => void;
}) {
  const [declaredAt, setDeclaredAt] = useState<LocalDateTime>(() => nowLocal());
  const [hours, setHours] = useState(String(DEFAULT_OOS_HOURS));
  const [openEnded, setOpenEnded] = useState(false);
  const [reason, setReason] = useState("");
  const [evidence, setEvidence] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const declaredUnix = unixOf(declaredAt);
  const hoursNumber = Number(hours);
  const untilPreview = declaredUnix !== null && !openEnded && hours.trim() !== "" && hoursNumber > 0 ? formatDateTimeUtc(isoOfUnix(declaredUnix + Math.round(hoursNumber * 3600))) : null;

  async function submit() {
    if (busy) return;
    if (declaredUnix === null) return setError("Enter when the driver was declared out of service (date and local time).");
    if (!openEnded && (hours.trim() === "" || !Number.isFinite(hoursNumber) || hoursNumber <= 0)) return setError("Enter the period in hours, or tick open-ended.");
    if (!reason.trim()) return setError("Enter the reason enforcement gave.");

    setBusy(true);
    setError(null);
    try {
      const record = await recordOutOfService(driverId, outOfServiceInput(declaredUnix, hoursNumber, openEnded, reason, evidence));
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to record the declaration — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title="Record Out of Service"
      onClose={onClose}
      error={error}
      maxWidth={640}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "RECORDING…" : "RECORD OUT OF SERVICE"}
          </ActionButton>
        </>
      }
    >
      <div style={{ marginBottom: 14 }}>
        <StatusChip kind="over" glyph="✕" label="The driver may not drive until the period ends or the declaration is lifted" />
      </div>
      <div style={{ marginBottom: 14 }}>
        <DateTimeFields label="Declared" value={declaredAt} onChange={setDeclaredAt} />
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, alignItems: "end", marginBottom: 14 }}>
        <NumberField
          label="Period (hours)"
          value={hours}
          onChange={setHours}
          min={1}
          step={1}
          disabled={openEnded}
          hint={<span style={{ color: colors.textFaint }}>— {DEFAULT_OOS_HOURS}h is the regulation&apos;s default</span>}
        />
        <label style={{ display: "flex", alignItems: "center", gap: 8, fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary, cursor: "pointer", height: 40 }}>
          <input type="checkbox" checked={openEnded} onChange={(e) => setOpenEnded(e.target.checked)} />
          Open-ended — until lifted
        </label>
      </div>
      {untilPreview && (
        <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim, marginBottom: 14 }}>Ends {untilPreview} (local)</div>
      )}
      <div style={{ marginBottom: 14 }}>
        <TextAreaField label="Reason enforcement gave" value={reason} onChange={setReason} placeholder="Roadside inspection — hours exceeded…" />
      </div>
      <TextField label="Evidence (optional)" value={evidence} onChange={setEvidence} placeholder="Inspection report number, officer, location" />
    </ModalShell>
  );
}

export function LiftOutOfServiceModal({
  driverId,
  driverName,
  recordId,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  recordId: string;
  onClose: () => void;
  onSaved: (record: OutOfServiceDeclaration) => void;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      const record = await liftOutOfService(driverId, recordId, reason.trim() || null);
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to lift the declaration — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title="Lift Out of Service"
      onClose={onClose}
      error={error}
      maxWidth={520}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "LIFTING…" : "LIFT"}
          </ActionButton>
        </>
      }
    >
      <TextAreaField label="Reason" value={reason} onChange={setReason} placeholder="Required when lifting before the period ends" />
    </ModalShell>
  );
}
