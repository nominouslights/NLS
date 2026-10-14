"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  approveOffDutyDeferral,
  declareHosException,
  type ApproveDeferralInput,
  type DeclarableExceptionKind,
  type DeclareExceptionInput,
  type HosExceptionRecord,
} from "@/lib/api/hos";
import { listTrips, type TripRecord } from "@/lib/api/trips";
import { HOS_LIMITS, HosWire } from "@/lib/hos/engine";
import { isoOfUnix } from "@/lib/hos/display";
import { ModalShell } from "@/components/ui/ModalShell";
import { FieldLabel, NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { DateTimeFields, nowLocal, unixOf, type LocalDateTime } from "./DateTimeFields";

// Record an hours-of-service exception for a driver. Two modes, two routes:
//   declare  — POST …/hos/exceptions (HosAccess): Adverse driving conditions (s.76 — the
//              driver must attest the conditions were not knowable before departure, and
//              asks for 1–120 extra minutes of driving) or Emergency (s.76(2)).
//   deferral — POST …/hos/exceptions/deferral (ComplianceAccess only): an s.16 off-duty
//              deferral tied to a specific trip, 1–120 minutes, starting in the future.
// Every refusal is the server's sentence verbatim; the client checks only what it can.

export const DECLARABLE_KINDS: DeclarableExceptionKind[] = [HosWire.AdverseDrivingConditionsKind, HosWire.EmergencyKind] as DeclarableExceptionKind[];

export const ADVERSE_ATTESTATION_TEXT =
  "Conditions were not knowable before departure — forecast/511 before leaving does not qualify";

export const EXTENSION_MIN_MINUTES = 1;
export const EXTENSION_MAX_MINUTES = HOS_LIMITS.adverseMaxExtensionSeconds / 60;
export const DEFERRAL_MIN_MINUTES = 1;
export const DEFERRAL_MAX_MINUTES = HOS_LIMITS.deferralMaxSeconds / 60;

/** Whole minutes inside [min, max], else null. */
export function minutesInRange(raw: string, min: number, max: number): number | null {
  const n = Number(raw);
  if (raw.trim() === "" || !Number.isInteger(n) || n < min || n > max) return null;
  return n;
}

export interface AdverseDraft {
  attested: boolean;
  extraMinutes: string;
  reason: string;
}

/** Null when an adverse declaration may be sent; otherwise the first problem. */
export function validateAdverse(draft: AdverseDraft): string | null {
  if (!draft.reason.trim()) return "Enter the reason — what the conditions were.";
  if (!draft.attested) return "Tick the attestation: adverse conditions must not have been knowable before departure.";
  if (minutesInRange(draft.extraMinutes, EXTENSION_MIN_MINUTES, EXTENSION_MAX_MINUTES) === null) {
    return `Extra driving must be a whole number of minutes from ${EXTENSION_MIN_MINUTES} to ${EXTENSION_MAX_MINUTES}.`;
  }
  return null;
}

function tripLabel(t: TripRecord): string {
  return `${t.tripNumber} · ${t.serviceDate} ${t.windowStart.slice(0, 5)} · ${t.origin} → ${t.destination}`;
}

export default function HosExceptionModal({
  driverId,
  driverName,
  today,
  mode,
  onClose,
  onSaved,
}: {
  driverId: string;
  driverName: string;
  today: string;
  mode: "declare" | "deferral";
  onClose: () => void;
  onSaved: (record: HosExceptionRecord) => void;
}) {
  const [kind, setKind] = useState<DeclarableExceptionKind>(DECLARABLE_KINDS[0]);
  const [from, setFrom] = useState<LocalDateTime>(() => nowLocal());
  const [to, setTo] = useState<LocalDateTime>({ date: "", time: "" });
  const [reason, setReason] = useState("");
  const [location, setLocation] = useState("");
  const [attested, setAttested] = useState(false);
  const [extraMinutes, setExtraMinutes] = useState("");
  const [eldRemark, setEldRemark] = useState(false);
  const [deferredMinutes, setDeferredMinutes] = useState("");
  const [tripId, setTripId] = useState("");
  const [trips, setTrips] = useState<TripRecord[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The deferral's trip picker: this driver's upcoming trips. Best-effort — an
  // unreachable Trips API falls back to a free trip-id field rather than blocking.
  useEffect(() => {
    if (mode !== "deferral") return;
    let active = true;
    listTrips({ driverId, from: today, excludeCancelled: true }).then(
      (page) => {
        if (!active) return;
        const upcoming = page.items.filter((t) => t.status === "Scheduled").sort((a, b) => a.serviceDate.localeCompare(b.serviceDate) || a.windowStart.localeCompare(b.windowStart));
        setTrips(upcoming);
        if (upcoming.length > 0) setTripId((id) => id || upcoming[0].id);
      },
      () => {
        if (active) setTrips([]);
      },
    );
    return () => {
      active = false;
    };
  }, [mode, driverId, today]);

  const isAdverse = kind === HosWire.AdverseDrivingConditionsKind;

  async function submit() {
    if (busy) return;
    const fromUnix = unixOf(from);
    if (fromUnix === null) return setError("Enter when the exception takes effect (date and local time).");
    const toUnix = to.date || to.time ? unixOf(to) : null;
    if ((to.date || to.time) && toUnix === null) return setError("Enter both the date and the time the conditions ended, or leave both blank.");

    if (mode === "deferral") {
      if (!tripId.trim()) return setError("Choose the trip this deferral is approved for.");
      const minutes = minutesInRange(deferredMinutes, DEFERRAL_MIN_MINUTES, DEFERRAL_MAX_MINUTES);
      if (minutes === null) return setError(`Deferred off-duty must be a whole number of minutes from ${DEFERRAL_MIN_MINUTES} to ${DEFERRAL_MAX_MINUTES}.`);
      if (!reason.trim()) return setError("Enter the reason for the deferral.");
      const input: ApproveDeferralInput = {
        tripId: tripId.trim(),
        effectiveFromUtc: isoOfUnix(fromUnix),
        deferredOffDutyMinutes: minutes,
        reason: reason.trim(),
        location: location.trim() || null,
        eldRemarkRecorded: eldRemark,
      };
      await send(() => approveOffDutyDeferral(driverId, input));
      return;
    }

    if (isAdverse) {
      const problem = validateAdverse({ attested, extraMinutes, reason });
      if (problem) return setError(problem);
    } else if (!reason.trim()) {
      return setError("Enter the reason — what the emergency was.");
    }

    const input: DeclareExceptionInput = {
      kind,
      effectiveFromUtc: isoOfUnix(fromUnix),
      reason: reason.trim(),
      effectiveToUtc: toUnix === null ? null : isoOfUnix(toUnix),
      location: location.trim() || null,
      eldRemarkRecorded: eldRemark,
      ...(isAdverse ? { notKnownBeforeDeparture: true, extraDrivingMinutes: Number(extraMinutes) } : {}),
    };
    await send(() => declareHosException(driverId, input));
  }

  async function send(call: () => Promise<HosExceptionRecord>) {
    setBusy(true);
    setError(null);
    try {
      const record = await call();
      onSaved(record);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to record the exception — please try again.");
      setBusy(false);
    }
  }

  const checkbox = (checked: boolean, onChange: (v: boolean) => void, text: string) => (
    <label style={{ display: "flex", alignItems: "flex-start", gap: 8, fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary, cursor: "pointer", lineHeight: 1.45 }}>
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} style={{ marginTop: 2 }} />
      {text}
    </label>
  );

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title={mode === "deferral" ? "Approve Off-Duty Deferral" : "Record Exception"}
      onClose={onClose}
      error={error}
      maxWidth={720}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "RECORDING…" : mode === "deferral" ? "APPROVE DEFERRAL" : "RECORD EXCEPTION"}
          </ActionButton>
        </>
      }
    >
      {mode === "deferral" ? (
        <div style={{ marginBottom: 14 }}>
          <StatusChip kind="info" label="Compliance Officer approval · s.16 — the deferral is tied to one trip and must start in the future" />
        </div>
      ) : (
        <div style={{ marginBottom: 14 }}>
          <SelectField
            label="Kind"
            value={kind}
            onChange={(v) => setKind(v as DeclarableExceptionKind)}
            options={DECLARABLE_KINDS.map((k) => ({ value: k, label: k }))}
            hint={<span style={{ color: colors.textFaint }}>— a deferral is approved by a compliance officer, not declared</span>}
          />
        </div>
      )}

      {mode === "deferral" && (
        <div style={{ marginBottom: 14 }}>
          {trips === null ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Loading this driver&apos;s upcoming trips…</div>
          ) : trips.length > 0 ? (
            <SelectField label="Trip" value={tripId} onChange={setTripId} options={trips.map((t) => ({ value: t.id, label: tripLabel(t) }))} />
          ) : (
            <TextField label="Trip id" value={tripId} onChange={setTripId} mono placeholder="No upcoming trips found — paste the trip id" />
          )}
        </div>
      )}

      <div style={{ marginBottom: 14 }}>
        <DateTimeFields label={mode === "deferral" ? "Effective from" : "Conditions arose"} value={from} onChange={setFrom} />
      </div>
      {mode === "declare" && (
        <div style={{ marginBottom: 14 }}>
          <DateTimeFields label="Conditions ended (optional)" value={to} onChange={setTo} />
        </div>
      )}

      <div style={{ display: "grid", gridTemplateColumns: mode === "deferral" || isAdverse ? "1fr 1fr" : "1fr", gap: 14, marginBottom: 14 }}>
        {mode === "deferral" ? (
          <NumberField
            label="Deferred off-duty (minutes)"
            value={deferredMinutes}
            onChange={setDeferredMinutes}
            min={DEFERRAL_MIN_MINUTES}
            step={1}
            placeholder={`${DEFERRAL_MIN_MINUTES}–${DEFERRAL_MAX_MINUTES}`}
          />
        ) : (
          isAdverse && (
            <NumberField
              label="Extra driving (minutes)"
              value={extraMinutes}
              onChange={setExtraMinutes}
              min={EXTENSION_MIN_MINUTES}
              step={1}
              placeholder={`${EXTENSION_MIN_MINUTES}–${EXTENSION_MAX_MINUTES}`}
              hint={<span style={{ color: colors.textFaint }}>— at most 2h, this shift and day only</span>}
            />
          )
        )}
        <TextField label="Location (optional)" value={location} onChange={setLocation} placeholder="Where the driver was" />
      </div>

      <div style={{ marginBottom: 14 }}>
        <TextAreaField
          label="Reason"
          value={reason}
          onChange={setReason}
          placeholder={mode === "deferral" ? "Why the off-duty time is deferred to day two" : isAdverse ? "Whiteout on PTH 6 north of Grand Rapids…" : "What happened"}
        />
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
        {mode === "declare" && isAdverse && (
          <div data-testid="adverse-attestation">
            <FieldLabel>Attestation (required)</FieldLabel>
            {checkbox(attested, setAttested, ADVERSE_ATTESTATION_TEXT)}
          </div>
        )}
        {checkbox(eldRemark, setEldRemark, "ELD remark recorded — the annotation is on the bus's ELD as well")}
      </div>
    </ModalShell>
  );
}

/** A note for the record on reject / withdraw: the one-field confirmation the two actions share. */
export function ExceptionNoteModal({
  driverName,
  title,
  action,
  prompt,
  onClose,
  onConfirm,
}: {
  driverName: string;
  title: string;
  action: string;
  prompt: string;
  onClose: () => void;
  onConfirm: (note: string | null) => Promise<void>;
}) {
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await onConfirm(note.trim() || null);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "The action failed — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${driverName} · Hours of Service`}
      title={title}
      onClose={onClose}
      error={error}
      maxWidth={520}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "WORKING…" : action}
          </ActionButton>
        </>
      }
    >
      <TextAreaField label={prompt} value={note} onChange={setNote} placeholder="For the record (optional)" />
    </ModalShell>
  );
}
