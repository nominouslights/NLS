"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { COMPANY, telHref } from "@/lib/company";
import { ALERT_LIMIT_LABEL } from "@/lib/hos/display";
import { dismissAlert, useHosAlerts } from "@/lib/hos/useHosAlerts";
import { ModalShell } from "@/components/ui/ModalShell";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { TouchButton } from "@/components/ui-tablet/TouchButton";

// APP-LOCAL. The hours alerts as the driver meets them, on every screen (Console renders this
// once, above the screen switch). The rules live in lib/hos/useHosAlerts.ts; this file only
// draws three things:
//
//   1. The 60/30-minute warning — the copied ModalShell, dismissible, with the dispatch number
//      on a tel: button beside OK.
//   2. The stop modal at 0 — NOT ModalShell: that shell draws a ✕ that closes, and a close
//      control that does nothing is worse than none. An app-local full-bleed panel with one
//      action (call dispatch) and no way to dismiss; it clears only when duty leaves Driving.
//   3. The out-of-hours banner that stays up afterwards for the rest of the duty period.
//
// Colour + glyph + words on all three, from statusMeta("over") / ("soon"). Never the tint alone.

function callDispatch(): void {
  window.location.href = telHref(COMPANY.dispatchPhone);
}

function minutesLabel(seconds: number): string {
  return `${Math.round(seconds / 60)} min`;
}

export default function HosAlerts() {
  const { modal, outOfHours } = useHosAlerts();

  return (
    <>
      {outOfHours !== null && (modal === null || modal.dismissible) ? (
        <div style={{ padding: `${gap.section}px ${gap.page}px 0` }}>
          <StatusBanner kind="over" title={`Out of hours — ${ALERT_LIMIT_LABEL[outOfHours]} is used up.`}>
            Do not drive. Stop at the nearest safe location if you have not already, and call dispatch at{" "}
            {COMPANY.dispatchPhone}. The figures on Hours say when your next off-duty period ends.
          </StatusBanner>
        </div>
      ) : null}

      {modal !== null && modal.dismissible ? (
        <ModalShell
          eyebrow="Hours of service"
          title={modal.reminder ? "Reminder — approaching a limit" : "Approaching a limit"}
          onClose={dismissAlert}
          footer={
            <>
              <TouchButton variant="secondary" onClick={callDispatch}>
                Call dispatch {COMPANY.dispatchPhone}
              </TouchButton>
              <TouchButton onClick={dismissAlert}>OK</TouchButton>
            </>
          }
        >
          <AlertLine kind="soon">
            <strong style={{ fontWeight: 600 }}>
              {ALERT_LIMIT_LABEL[modal.limit]} ends within {minutesLabel(modal.thresholdSeconds)}.
            </strong>{" "}
            Plan to stop at the nearest safe location and call dispatch ({COMPANY.dispatchPhone}).
          </AlertLine>
        </ModalShell>
      ) : null}

      {modal !== null && !modal.dismissible ? <StopPanel limit={ALERT_LIMIT_LABEL[modal.limit]} /> : null}
    </>
  );
}

function AlertLine({ kind, children }: { kind: "soon" | "over"; children: React.ReactNode }) {
  const m = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        gap: 12,
        alignItems: "flex-start",
        fontFamily: fonts.body,
        fontSize: type.value,
        lineHeight: 1.4,
        color: colors.textPrimary,
      }}
    >
      <span aria-hidden style={{ color: m.t, fontWeight: 800, fontSize: type.value }}>
        {m.g}
      </span>
      <span>{children}</span>
    </div>
  );
}

/** The non-dismissible stop modal. One action. No close. */
function StopPanel({ limit }: { limit: string }) {
  const m = statusMeta("over");
  return (
    <div
      role="alertdialog"
      aria-modal="true"
      aria-label="Stop at the nearest safe location and call dispatch"
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 110,
        background: colors.scrim,
        backdropFilter: "blur(3px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: gap.page,
      }}
    >
      <div
        style={{
          width: "100%",
          maxWidth: 760,
          background: colors.mainBg,
          border: `3px solid ${m.c}`,
          borderRadius: radius.panel + 4,
          boxShadow: colors.shadowPop,
          padding: "28px 32px",
          display: "flex",
          flexDirection: "column",
          gap: gap.section,
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
          <span aria-hidden style={{ color: m.t, fontWeight: 800, fontSize: type.metric, lineHeight: 1 }}>
            {m.g}
          </span>
          <div>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: type.group,
                letterSpacing: ".16em",
                textTransform: "uppercase",
                color: m.t,
                marginBottom: 4,
              }}
            >
              Out of hours · {limit}
            </div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, color: colors.headingBright, lineHeight: 1.1 }}>
              Stop at the nearest safe location and call dispatch
            </div>
          </div>
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: type.value, color: colors.textPrimary, lineHeight: 1.45 }}>
          {limit} has run out. You may not drive until the required off-duty time has passed. This
          panel stays until your duty status leaves Driving.
        </div>
        <TouchButton variant="danger" onClick={callDispatch} style={{ minHeight: touch.rail, fontSize: 20 }}>
          Call dispatch {COMPANY.dispatchPhone}
        </TouchButton>
      </div>
    </div>
  );
}
