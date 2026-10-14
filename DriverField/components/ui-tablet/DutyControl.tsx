"use client";

import { useState } from "react";
import { colors, dutyMeta, fonts, statusMeta } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { assignedVehicle } from "@/lib/data";
import { enqueue, failedCommands } from "@/lib/sync/queue";
import { useSyncState } from "@/lib/sync/useSyncState";
import { dutyCommandPayload, isDutyCommandPayload } from "@/lib/hos/dutyCommand";
import { cannotDriveBecause, DUTY_STATES, dutyStateOf, engineStatusOf, kmLabel, pcDayVerdict, todayPcDecikm } from "@/lib/hos/display";
import { HOS_LIMITS } from "@/lib/hos/engine";
import { appendDutyEvent, CALENDAR, nowUnix } from "@/lib/hos/ledger";
import { useHosLedger } from "@/lib/hos/useHosLedger";
import { PersonalConveyanceModal, type PcModalMode } from "@/components/ui-tablet/PersonalConveyanceModal";
import type { DutyState } from "@/lib/types";

// APP-LOCAL. The duty-status switch — the single most-used control in the app.
//
// Four large explicit buttons, not a cycle or a dropdown. Duty status is the record a
// regulator reads; a driver must be able to change it correctly at a glance, with a glove, in
// a vehicle, and must never be able to change it by accident with one stray tap on a control
// that cycles. The four states and their colour + glyph + label come from the copied
// lib/theme.ts's dutyMeta() — nothing is mapped by hand here.
//
// The wire strings are exact: "Off Duty" / "On Duty" / "Driving" / "Personal Conveyance" match
// HosWire's constants byte for byte, pinned by lib/wire.test.ts. Do not "tidy" them.
//
// What a tap does, in order: enqueue the hos.duty command (every mutation goes through the sync
// queue — never a direct API call), then append the same event optimistically to the ledger
// store so every figure on every screen moves now. The payload carries source "Driver App" and
// the command's own id as clientEventId (lib/hos/dutyCommand.ts); the queue sends it with that
// id as the eventId and Idempotency-Key, and the ledger's next fetch reconciles the optimistic
// event against the server's. If the server refuses it, the Hours screen shows the refusal and
// the figures drop the event. If the device cannot hold the command at all, the tap does
// nothing and says so under the control — a change that looked recorded and was not is worse.
//
// PERSONAL CONVEYANCE goes through the same kind with two more stops. Tapping it opens the
// sheet (PersonalConveyanceModal) for the start odometer and the four attestations; the event
// carries them (PcOdometerRequired / PcAttestationRequired otherwise). Tapping ANY other state
// while in personal conveyance opens the sheet for the END odometer first, because the event
// that closes the segment must carry it (PcOdometerEndRequired) and it may not be lower than
// the start (PcOdometerDecreasing) — both refused on the tablet before the server would.
//
// Options can be disabled, and each says why ON SCREEN, under the control, not in a tooltip a
// gloved hand never sees:
//   - Driving, when the engine says the driver may not drive now (the first blocking reason,
//     in the driver's words — "Out of service" among them, from the span the ledger feeds
//     the engine). Never disabled while already Driving — stopping is always allowed.
//   - Personal Conveyance, while out of service (the engine treats PC while out of service as
//     driving), once today's 75.0 km allowance is used up (the server would answer
//     PcAllowanceExhausted), or after the server has said exactly that today.

export function DutyControl() {
  const snapshot = useHosLedger();
  const { evaluation, outOfService } = snapshot;
  // Re-render when a drain parks a refusal, so a PcAllowanceExhausted answer greys PC at once.
  useSyncState();
  const [captureError, setCaptureError] = useState<string | null>(null);
  const [pcModal, setPcModal] = useState<(PcModalMode & { next?: DutyState }) | null>(null);
  const current = dutyStateOf(evaluation.currentStatus);
  const drivingBlocked = current === "Driving" ? null : cannotDriveBecause(evaluation);
  const pcDecikm = todayPcDecikm(evaluation);
  const pcBlocked = current === "Personal Conveyance" ? null : pcReason(snapshot.outOfService !== null, evaluation.blockingReasons.includes("OutOfService"), pcDecikm, snapshot.asOfUnix);

  function disabledReason(state: DutyState): string | null {
    if (state === "Driving") return drivingBlocked;
    if (state === "Personal Conveyance") return pcBlocked;
    return null;
  }

  async function record(next: DutyState, extras: Parameters<typeof dutyCommandPayload>[3], odometerDecikm: number | null) {
    const status = engineStatusOf(next);
    const atUnix = nowUnix();
    const atUtc = new Date(atUnix * 1000).toISOString();
    try {
      const commandId = await enqueue("hos.duty", (id) => dutyCommandPayload(status, atUtc, id, extras));
      appendDutyEvent({ status, clientEventId: commandId, atUnix, odometerDecikm });
      setCaptureError(null);
    } catch (err) {
      setCaptureError(err instanceof Error ? err.message : String(err));
    }
  }

  function select(next: DutyState) {
    if (next === current || disabledReason(next) !== null) return;
    if (next === "Personal Conveyance") {
      setPcModal({ mode: "start", suggestedKm: lastKnownOdometerKm(snapshot.events) });
      return;
    }
    if (current === "Personal Conveyance") {
      setPcModal({ mode: "end", startKm: openSegmentStartKm(snapshot.events), next });
      return;
    }
    void record(next, {}, null);
  }

  function confirmPc(km: number) {
    const modal = pcModal;
    setPcModal(null);
    if (modal === null) return;
    const decikm = Math.round(km * 10);
    if (modal.mode === "start") {
      void record("Personal Conveyance", { odometerStartKm: km, pcNoPassengersOrCargo: true, pcNoTrailer: true, pcNoWork: true }, decikm);
    } else if (modal.next !== undefined) {
      void record(modal.next, { odometerEndKm: km }, decikm);
    }
  }

  const reasons = DUTY_STATES.map((s) => [s, disabledReason(s)] as const).filter(([, r]) => r !== null);
  const pcVerdict = pcDayVerdict(pcDecikm);

  return (
    <div>
      <div role="group" aria-label="Duty status" style={{ display: "flex", gap: 10, flexWrap: "wrap" }}>
        {DUTY_STATES.map((state) => {
          const m = dutyMeta(state);
          const active = current === state;
          const reason = disabledReason(state);
          const disabled = reason !== null && !active;
          return (
            <button
              key={state}
              onClick={() => select(state)}
              aria-pressed={active}
              aria-disabled={disabled || undefined}
              disabled={disabled}
              title={disabled ? reason ?? undefined : undefined}
              style={{
                flex: "1 1 0",
                minWidth: 180,
                minHeight: touch.primary + 12,
                borderRadius: radius.control,
                border: `2px solid ${active ? m.color : colors.border}`,
                background: active ? m.color : colors.cardBg,
                color: active ? "#FFFFFF" : colors.textMuted,
                fontFamily: fonts.semiCondensed,
                fontSize: type.value,
                fontWeight: 600,
                letterSpacing: ".06em",
                textTransform: "uppercase",
                cursor: disabled ? "not-allowed" : "pointer",
                opacity: disabled ? 0.45 : 1,
                display: "inline-flex",
                alignItems: "center",
                justifyContent: "center",
                gap: 10,
              }}
            >
              <span aria-hidden style={{ fontSize: type.value }}>
                {m.glyph}
              </span>
              {state}
            </button>
          );
        })}
      </div>

      {reasons.length > 0 || captureError !== null || current === "Personal Conveyance" || pcDecikm > 0 ? (
        <div style={{ display: "flex", flexDirection: "column", gap: gap.tight, marginTop: gap.row }}>
          {captureError !== null ? (
            <ReasonLine kind="over">
              <strong style={{ fontWeight: 600 }}>Not recorded</strong> — this tablet could not hold the change: {captureError}
            </ReasonLine>
          ) : null}
          {reasons.map(([state, reason]) => (
            <ReasonLine key={state} kind={state === "Driving" || outOfService !== null ? "over" : "info"}>
              <strong style={{ fontWeight: 600 }}>{state} unavailable</strong> — {reason}
            </ReasonLine>
          ))}
          {current === "Personal Conveyance" || pcDecikm > 0 ? (
            <ReasonLine kind={pcVerdict.kind}>
              <strong style={{ fontWeight: 600 }}>PC today: {kmLabel(pcVerdict.usedDecikm)}</strong> — {pcVerdict.label}
              {current === "Personal Conveyance" ? ". Your next duty change asks for the odometer reading; kilometres are what you enter, not a live feed." : "."}
            </ReasonLine>
          ) : null}
        </div>
      ) : null}

      {pcModal !== null ? (
        <PersonalConveyanceModal mode={pcModal} todayPcDecikm={pcDecikm} onClose={() => setPcModal(null)} onConfirm={confirmPc} />
      ) : null}
    </div>
  );
}

/**
 * Why Personal Conveyance is unavailable, or null. Out of service first (the engine treats
 * personal conveyance while out of service as driving, and the span blocks driving); then the
 * day's 75.0 km allowance (DutyDay refuses to open a segment at or past it); then the
 * server's own PcAllowanceExhausted answer to a change made today, parked on Hours.
 */
export function pcReason(outOfService: boolean, engineOutOfService: boolean, pcDecikm: number, asOfUnix: number): string | null {
  if (outOfService || engineOutOfService) return "Out of service — personal conveyance is not permitted until the declaration is lifted";
  if (pcDecikm >= HOS_LIMITS.pcDailyLimitDecikm) {
    return `The ${kmLabel(HOS_LIMITS.pcDailyLimitDecikm)} personal-conveyance allowance for today is used up; further driving is work`;
  }
  const today = CALENDAR.dayOf(asOfUnix);
  const refused = failedCommands().find((c) => {
    if (c.kind !== "hos.duty" || c.errorCode !== "Drivers.Hos.PcAllowanceExhausted" || !isDutyCommandPayload(c.payload)) return false;
    const at = Date.parse(c.payload.atUtc);
    return Number.isFinite(at) && CALENDAR.dayOf(Math.floor(at / 1000)) === today;
  });
  if (refused !== undefined) return `The server refused personal conveyance today — ${refused.error ?? "the allowance is used up"}`;
  return null;
}

/** The start reading of the open personal-conveyance segment: the last event's, when it is PC. */
function openSegmentStartKm(events: readonly { status: string; odometerDecikm: number | null }[]): number | null {
  const last = events.at(-1);
  if (last === undefined || last.status !== "PersonalConveyance" || last.odometerDecikm === null) return null;
  return last.odometerDecikm / 10;
}

/**
 * A suggestion for the start odometer: the last reading any event on the ledger carried, else
 * the assigned vehicle's last known reading from the mock fleet row (the one mock value this
 * control touches — it is a prefill the driver corrects, never a figure that goes to the wire
 * unread).
 */
function lastKnownOdometerKm(events: readonly { odometerDecikm: number | null }[]): number | null {
  for (let i = events.length - 1; i >= 0; i -= 1) {
    const odo = events[i].odometerDecikm;
    if (odo !== null) return odo / 10;
  }
  return assignedVehicle.odometerKm;
}

function ReasonLine({ kind, children }: { kind: "over" | "info" | "soon" | "ontime" | "off"; children: React.ReactNode }) {
  const m = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        fontFamily: fonts.body,
        fontSize: type.label,
        color: m.t,
      }}
    >
      <span aria-hidden style={{ fontWeight: 800 }}>
        {m.g}
      </span>
      <span>{children}</span>
    </div>
  );
}
