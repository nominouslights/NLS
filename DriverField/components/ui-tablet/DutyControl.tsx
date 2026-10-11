"use client";

import { useState } from "react";
import { colors, dutyMeta, fonts, statusMeta } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { enqueue } from "@/lib/sync/queue";
import { dutyCommandPayload } from "@/lib/hos/dutyCommand";
import { cannotDriveBecause, DUTY_STATES, dutyStateOf, engineStatusOf } from "@/lib/hos/display";
import { appendDutyEvent, nowUnix } from "@/lib/hos/ledger";
import { useHosLedger } from "@/lib/hos/useHosLedger";
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
// Two options can be disabled, and each says why ON SCREEN, under the control, not in a
// tooltip a gloved hand never sees:
//   - Driving, when the engine says the driver may not drive now (the first blocking reason,
//     in the driver's words). The Driving button is never disabled while the driver is already
//     Driving — stopping is always allowed.
//   - Personal Conveyance, in this slice: PC needs a start odometer and three attestations on
//     the event (the endpoint refuses it without them — PcOdometerRequired,
//     PcAttestationRequired), and that modal is the next slice. Present and visibly
//     unavailable, rather than absent, so the control's shape is final.

const PC_REASON = "Odometer and attestation entry arrive with the next update";

export function DutyControl() {
  const { evaluation } = useHosLedger();
  const [captureError, setCaptureError] = useState<string | null>(null);
  const current = dutyStateOf(evaluation.currentStatus);
  const drivingBlocked = current === "Driving" ? null : cannotDriveBecause(evaluation);

  function disabledReason(state: DutyState): string | null {
    if (state === "Driving") return drivingBlocked;
    if (state === "Personal Conveyance") return PC_REASON;
    return null;
  }

  async function select(next: DutyState) {
    if (next === current || disabledReason(next) !== null) return;
    const status = engineStatusOf(next);
    const atUnix = nowUnix();
    const atUtc = new Date(atUnix * 1000).toISOString();
    try {
      const commandId = await enqueue("hos.duty", (id) => dutyCommandPayload(status, atUtc, id));
      appendDutyEvent({ status, clientEventId: commandId, atUnix });
      setCaptureError(null);
    } catch (err) {
      setCaptureError(err instanceof Error ? err.message : String(err));
    }
  }

  const reasons = DUTY_STATES.map((s) => [s, disabledReason(s)] as const).filter(([, r]) => r !== null);

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
              onClick={() => void select(state)}
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

      {reasons.length > 0 || captureError !== null ? (
        <div style={{ display: "flex", flexDirection: "column", gap: gap.tight, marginTop: gap.row }}>
          {captureError !== null ? (
            <ReasonLine kind="over">
              <strong style={{ fontWeight: 600 }}>Not recorded</strong> — this tablet could not hold the change: {captureError}
            </ReasonLine>
          ) : null}
          {reasons.map(([state, reason]) => (
            <ReasonLine key={state} kind={state === "Driving" ? "over" : "info"}>
              <strong style={{ fontWeight: 600 }}>{state} unavailable</strong> — {reason}
            </ReasonLine>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function ReasonLine({ kind, children }: { kind: "over" | "info"; children: React.ReactNode }) {
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
