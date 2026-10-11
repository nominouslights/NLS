"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { attestPriorRecords, type HosStatus, type PriorRecordsRecord } from "@/lib/api/hos";
import { formatDateOnly, formatDateTimeUtc } from "@/lib/hos/display";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, TextAreaField } from "@/components/ui/Field";

// The prior-records attestation (s.82(1)(f)): a compliance officer's statement that the
// driver's records before they joined are on file through a date. Without it — and without
// 14 days of the driver's own ledger — the driver is not dispatchable, and the panel says so
// in the caution kind rather than hiding it. ATTEST is compliance-only.

export const PRIOR_RECORDS_MISSING_TEXT = "Prior 14 days not on file — not dispatchable";

export function PriorRecordsPanel({
  driverId,
  driverName,
  today,
  status,
  canCompliance,
  onChanged,
}: {
  driverId: string;
  driverName: string;
  today: string;
  status: HosStatus | null;
  canCompliance: boolean;
  onChanged: () => void;
}) {
  const [modal, setModal] = useState(false);
  const attestation = status?.priorRecords ?? null;

  return (
    <Panel style={{ marginBottom: 12 }}>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 12 }}>
        <SectionLabel>Prior records · s.82(1)(f)</SectionLabel>
        {canCompliance && (
          <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal(true)}>
            {attestation ? "RE-ATTEST" : "ATTEST"}
          </ActionButton>
        )}
      </div>
      {status === null ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading…</div>
      ) : attestation ? (
        <div>
          <StatusChip kind="ontime" label={`Prior records attested through ${formatDateOnly(attestation.coveredThroughDate)}`} />
          <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginTop: 8, lineHeight: 1.6 }}>
            Attested {formatDateTimeUtc(attestation.attestedAtUtc)} by user {attestation.attestedByUserId.slice(0, 8)}
            {attestation.note && (
              <>
                <br />
                {attestation.note}
              </>
            )}
          </div>
          <div style={{ marginTop: 8 }}>
            {attestation.worksForOtherCarriers ? (
              <StatusChip kind="soon" glyph="⇄" label="Works for other carriers — declare their hours on each day" />
            ) : (
              <StatusChip kind="off" glyph="⇄" label="No other carriers declared" />
            )}
          </div>
        </div>
      ) : status.recordsOnFile ? (
        <StatusChip kind="off" label="No attestation on file — the driver's own ledger covers the 14-day window" />
      ) : (
        <StatusChip kind="soon" glyph="◐" label={PRIOR_RECORDS_MISSING_TEXT} />
      )}

      {modal && (
        <AttestPriorRecordsModal
          driverId={driverId}
          driverName={driverName}
          today={today}
          existing={attestation}
          onClose={() => setModal(false)}
          onSaved={onChanged}
        />
      )}
    </Panel>
  );
}

export function AttestPriorRecordsModal({
  driverId,
  driverName,
  today,
  existing,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  today: string;
  existing: PriorRecordsRecord | null;
  onClose: () => void;
  onSaved: (record: PriorRecordsRecord) => void;
}) {
  const [coveredThrough, setCoveredThrough] = useState(existing?.coveredThroughDate ?? today);
  const [note, setNote] = useState(existing?.note ?? "");
  const [otherCarriers, setOtherCarriers] = useState(existing?.worksForOtherCarriers ?? false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!coveredThrough) return setError("Enter the date the prior records are on file through.");
    setBusy(true);
    setError(null);
    try {
      const record = await attestPriorRecords(driverId, { coveredThroughDate: coveredThrough, note: note.trim() || null, worksForOtherCarriers: otherCarriers });
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to record the attestation — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title="Attest Prior Records"
      onClose={onClose}
      error={error}
      maxWidth={560}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "RECORDING…" : "ATTEST"}
          </ActionButton>
        </>
      }
    >
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.5, marginBottom: 14 }}>
        You are stating, as the compliance officer, that the driver&apos;s records of duty status for the 14 days before their first day on file have been obtained and
        are kept at the principal place of business.
      </div>
      <div style={{ marginBottom: 14 }}>
        <DateField label="Records on file through" value={coveredThrough} onChange={setCoveredThrough} />
      </div>
      <div style={{ marginBottom: 14 }}>
        <label style={{ display: "flex", alignItems: "center", gap: 8, fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary, cursor: "pointer" }}>
          <input type="checkbox" checked={otherCarriers} onChange={(e) => setOtherCarriers(e.target.checked)} />
          The driver works for other carriers
        </label>
      </div>
      <TextAreaField label="Note (optional)" value={note} onChange={setNote} placeholder="Where the records came from" />
    </ModalShell>
  );
}
