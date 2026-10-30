"use client";

import { useState } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { gap, radius, type } from "@/lib/tablet";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, TextAreaField, TextField, TimeField } from "@/components/ui/Field";
import { AttestationTile } from "@/components/ui-tablet/AttestationTile";
import { StatusButton, TouchButton } from "@/components/ui-tablet/TouchButton";
import {
  ADVERSE_EXTENSION_MAX_MINUTES,
  ADVERSE_EXTENSION_MIN_MINUTES,
  declareExceptionPayload,
  ELD_CODE_MAX,
  eldMalfunctionDraftError,
  eldMalfunctionPayload,
  exceptionDraftError,
  EXCEPTION_LOCATION_MAX,
  EXCEPTION_REASON_MAX,
  OTHER_CARRIER_LOOKBACK_DAYS,
  otherCarrierDraftError,
  otherCarrierEvents,
  type DeclareExceptionCommandPayload,
  type DeclarableExceptionKind,
  type EldMalfunctionCommandPayload,
  type OtherCarrierEvent,
} from "@/lib/hos/complianceCommand";
import { clockLabel, dutyStateOf, localIsoToUnix } from "@/lib/hos/display";
import { addDays, HosWire, type DateOnly } from "@/lib/hos/engine";
import { CALENDAR } from "@/lib/hos/ledger";

// The three compliance sheets the Hours screen opens. Each one collects a draft, runs the
// client-side mirror of the aggregate's rule (lib/hos/complianceCommand.ts — named there) so
// the refusal reads on the tablet before the server would park it, and hands ONE built payload
// back to the screen, which enqueues it. No sheet touches the queue or the API itself.
//
// Inputs are the copied Field primitives at their desktop size. They sit under the 44px touch
// floor, and that is left visible rather than hidden behind a scale(): a ui-tablet field set
// is owed, and the attestation tiles and buttons around them are at size already.

// --- an adverse-conditions or emergency exception -----------------------------------

export function ExceptionSheet({
  nowUnix,
  onClose,
  onDeclare,
}: {
  /** The device clock at the moment the sheet opened: the exception's effective-from. */
  nowUnix: number;
  onClose: () => void;
  onDeclare: (payload: DeclareExceptionCommandPayload) => void;
}) {
  const [kind, setKind] = useState<DeclarableExceptionKind>(HosWire.AdverseDrivingConditionsKind as DeclarableExceptionKind);
  const [reason, setReason] = useState("");
  const [location, setLocation] = useState("");
  const [notKnown, setNotKnown] = useState(false);
  const [minutes, setMinutes] = useState("");
  const adverse = kind === HosWire.AdverseDrivingConditionsKind;

  const draft = { kind, effectiveFromUnix: nowUnix, reason, location, notKnownBeforeDeparture: notKnown, extraDrivingMinutes: minutes, eldRemarkRecorded: false };
  const error = exceptionDraftError(draft, nowUnix);

  return (
    <ModalShell
      eyebrow={`Hours-of-service exception · from ${clockLabel(nowUnix)}`}
      title="Record an exception"
      onClose={onClose}
      maxWidth={800}
      footer={
        <>
          <TouchButton variant="secondary" onClick={onClose}>
            Cancel
          </TouchButton>
          <TouchButton onClick={() => onDeclare(declareExceptionPayload(draft, nowUnix))} disabled={error !== null} disabledReason={error ?? undefined}>
            Declare {adverse ? "adverse conditions" : "emergency"}
          </TouchButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: gap.section }}>
        <div role="group" aria-label="Exception kind" style={{ display: "flex", gap: gap.row, flexWrap: "wrap" }}>
          <StatusButton
            kind="soon"
            label={HosWire.AdverseDrivingConditionsKind}
            active={adverse}
            onClick={() => setKind(HosWire.AdverseDrivingConditionsKind as DeclarableExceptionKind)}
          />
          <StatusButton kind="over" label={HosWire.EmergencyKind} active={!adverse} onClick={() => setKind(HosWire.EmergencyKind as DeclarableExceptionKind)} />
        </div>

        {adverse ? (
          <>
            <div role="group" aria-label="Adverse conditions attestation" style={{ display: "flex" }}>
              <AttestationTile
                label="Conditions were not knowable before departure — a forecast or 511 warning before leaving does not qualify"
                on={notKnown}
                onToggle={() => setNotKnown((v) => !v)}
              />
            </div>
            <div style={{ display: "flex", gap: gap.row, alignItems: "flex-start" }}>
              <div style={{ width: 260, flex: "none" }}>
                <NumberField
                  label={`Extra driving needed (minutes, ${ADVERSE_EXTENSION_MIN_MINUTES}–${ADVERSE_EXTENSION_MAX_MINUTES})`}
                  value={minutes}
                  onChange={setMinutes}
                  min={ADVERSE_EXTENSION_MIN_MINUTES}
                  step={1}
                  placeholder="60"
                />
              </div>
              <div style={{ flex: 1 }}>
                <TextField label="Where (optional)" value={location} onChange={setLocation} maxLength={EXCEPTION_LOCATION_MAX} placeholder="Highway 391 north of Nelson House" />
              </div>
            </div>
            <TextAreaField
              label="What the conditions are"
              value={reason}
              onChange={setReason}
              hint={`(${reason.trim().length}/${EXCEPTION_REASON_MAX})`}
              placeholder="Whiteout since km 40; convoy at 30 km/h behind the plow"
            />
            <Note kind="soon">
              The extension moves the driving and on-duty ceilings for this shift and day only, by at most two hours (s.76).
              The 8 hours of consecutive off-duty time are still required and the 70-hour cycle is unchanged.
            </Note>
          </>
        ) : (
          <>
            <TextField label="Where you stopped" value={location} onChange={setLocation} maxLength={EXCEPTION_LOCATION_MAX} placeholder="Shoulder, Highway 6 at the Grand Rapids turnoff" />
            <TextAreaField
              label="Why — what the emergency is"
              value={reason}
              onChange={setReason}
              hint={`(${reason.trim().length}/${EXCEPTION_REASON_MAX})`}
              placeholder="Medical emergency on board; waiting for EMS"
            />
            <Note kind="over">
              An emergency lets you reach a safe place; the limits are not extended, and any breach inside the emergency is
              recorded as covered by it, not erased.
            </Note>
          </>
        )}

        <Note kind="info">Dispatch is notified of the declaration and acknowledges it on the console. Annotate the bus&rsquo;s ELD with the same exception — that is the record.</Note>

        {error !== null ? <Note kind="off">{error}</Note> : null}
      </div>
    </ModalShell>
  );
}

// --- an ELD malfunction report ---------------------------------------------------

export function EldMalfunctionSheet({
  today,
  nowUnix,
  onClose,
  onReport,
}: {
  today: DateOnly;
  nowUnix: number;
  onClose: () => void;
  onReport: (payload: EldMalfunctionCommandPayload) => void;
}) {
  const [code, setCode] = useState("");
  const [time, setTime] = useState(clockLabel(nowUnix));
  const [description, setDescription] = useState("");

  const noticedAtUnix = /^\d{2}:\d{2}$/.test(time) ? localIsoToUnix(`${today}T${time}:00`) : Number.NaN;
  const draft = { code, noticedAtUnix, description };
  const error = eldMalfunctionDraftError(draft, nowUnix);

  return (
    <ModalShell
      eyebrow="ELD malfunction · s.78.2"
      title="Report an ELD malfunction"
      onClose={onClose}
      maxWidth={760}
      footer={
        <>
          <TouchButton variant="secondary" onClick={onClose}>
            Cancel
          </TouchButton>
          <TouchButton onClick={() => onReport(eldMalfunctionPayload(draft, nowUnix, null))} disabled={error !== null} disabledReason={error ?? undefined}>
            Report malfunction
          </TouchButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: gap.section }}>
        <div style={{ display: "flex", gap: gap.row }}>
          <div style={{ width: 240, flex: "none" }}>
            <TextField label="Code the ELD displayed" value={code} onChange={setCode} mono maxLength={ELD_CODE_MAX} placeholder="P, E, T or the vendor's code" />
          </div>
          <div style={{ width: 200, flex: "none" }}>
            <TimeField label={`Time noticed (today, ${today})`} value={time} onChange={setTime} />
          </div>
        </div>
        <TextAreaField label="What happened" value={description} onChange={setDescription} placeholder="Screen frozen on the duty graph since the Thompson depot; reboot did not clear it" />
        <Note kind="soon">
          From the time you noticed it, keep paper records of duty status until the device is repaired. Dispatch
          transcribes them into this ledger and a compliance officer closes the report. Paper logs are the sanctioned
          fallback — this warns dispatch, it does not stop you driving.
        </Note>
        {error !== null ? <Note kind="off">{error}</Note> : null}
      </div>
    </ModalShell>
  );
}

// --- hours worked for another carrier --------------------------------------------

export function OtherCarrierSheet({
  today,
  driverName,
  onClose,
  onDeclare,
}: {
  today: DateOnly;
  /** The roster name that goes on the wire as `enteredBy`. */
  driverName: string;
  onClose: () => void;
  onDeclare: (events: OtherCarrierEvent[], carrierName: string) => void;
}) {
  const [date, setDate] = useState<DateOnly>(addDays(today, -1));
  const [carrierName, setCarrierName] = useState("");
  const [onDutyMinutes, setOnDutyMinutes] = useState("");
  const [drivingMinutes, setDrivingMinutes] = useState("");

  const draft = { date, carrierName, onDutyMinutes, drivingMinutes };
  const error = otherCarrierDraftError(draft, today);
  const events = error === null ? otherCarrierEvents(draft, today, CALENDAR) : [];

  return (
    <ModalShell
      eyebrow="Work for another carrier · s.82(1)(f)"
      title="Declare other-carrier hours"
      onClose={onClose}
      maxWidth={800}
      footer={
        <>
          <TouchButton variant="secondary" onClick={onClose}>
            Cancel
          </TouchButton>
          <TouchButton onClick={() => onDeclare(events, carrierName.trim())} disabled={error !== null} disabledReason={error ?? undefined}>
            Declare {events.length > 0 ? `${events.length} duty events` : "hours"}
          </TouchButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: gap.section }}>
        <div style={{ display: "flex", gap: gap.row }}>
          <div style={{ width: 200, flex: "none" }}>
            <DateField label="Day" value={date} onChange={setDate} />
          </div>
          <div style={{ flex: 1 }}>
            <TextField label="Carrier" value={carrierName} onChange={setCarrierName} placeholder="Gardewine North" />
          </div>
        </div>
        <div style={{ display: "flex", gap: gap.row }}>
          <div style={{ width: 240, flex: "none" }}>
            <NumberField label="On duty (minutes, including driving)" value={onDutyMinutes} onChange={setOnDutyMinutes} min={1} step={1} placeholder="480" />
          </div>
          <div style={{ width: 240, flex: "none" }}>
            <NumberField label="Of which driving (minutes)" value={drivingMinutes} onChange={setDrivingMinutes} min={0} step={1} placeholder="300" />
          </div>
        </div>

        {events.length > 0 ? (
          <Note kind="info">
            Laid out from the start of {date}:{" "}
            {events.map((e, i) => `${i > 0 ? " → " : ""}${clockLabel(e.atUnix)} ${dutyStateOf(e.status)}`).join("")}. Each
            is its own duty event, source &ldquo;{HosWire.OtherCarrierSource}&rdquo;, entered by {driverName}.
          </Note>
        ) : null}

        <Note kind="soon">
          Work for another carrier counts toward your on-duty time, your daily limits and the 70-hour cycle exactly as
          work here does. Only the last {OTHER_CARRIER_LOOKBACK_DAYS} days can be declared from the tablet; dispatch
          keys anything older from your records.
        </Note>
        {error !== null ? <Note kind="off">{error}</Note> : null}
      </div>
    </ModalShell>
  );
}

// --- shared ------------------------------------------------------------------------

/** One note line: colour + glyph + words in the kind's colour, never the tint alone. */
function Note({ kind, children }: { kind: StatusKind; children: React.ReactNode }) {
  const m = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "flex-start",
        gap: 10,
        padding: "12px 16px",
        borderRadius: radius.control,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        fontFamily: fonts.body,
        fontSize: type.label,
        color: kind === "info" || kind === "off" ? colors.textSecondary : m.t,
        lineHeight: 1.5,
      }}
    >
      <span aria-hidden style={{ fontWeight: 800, color: m.t }}>
        {m.g}
      </span>
      <span>{children}</span>
    </div>
  );
}
