"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { COMPANY, telHref } from "@/lib/company";
import type { OutOfServiceRecord } from "@/lib/api/hos";
import { OUT_OF_SERVICE_GLYPH, whenLabel } from "@/lib/hos/display";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import Hours from "@/components/screens/Hours";

// APP-LOCAL. What the tablet shows while an out-of-service declaration (s.91) is in force —
// rendered full-screen by Console.tsx in place of the rail and every screen, because a driver
// enforcement has stopped must not be able to navigate to a trip and start it. The declaration
// comes from the server (GET …/hos/status `outOfService`; a compliance officer records it from
// the console), through the ledger store, which also hands the span to the rule engine — so the
// DutyControl greys Driving with "Out of service" from the same evaluation as every other figure.
//
// Vermillion with its own ✕ (the design system's `over` colour and a glyph override — a fifth
// StatusKind or a new hex is not an option in a copied theme), the until-instant in local time,
// enforcement's reason verbatim, the dispatch number on a tel: button, and one way out of the
// notice: a read-only view of the Hours screen, where the duty control still offers Off Duty and
// On Duty (yard work is allowed) and nothing that moves the bus.

function callDispatch(): void {
  window.location.href = telHref(COMPANY.dispatchPhone);
}

function unixOf(iso: string | null): number | null {
  if (iso === null) return null;
  const ms = Date.parse(iso);
  return Number.isFinite(ms) ? Math.floor(ms / 1000) : null;
}

export function outOfServiceTitle(record: OutOfServiceRecord): string {
  const until = unixOf(record.untilUtc);
  return until === null ? "Out of service until lifted — do not drive" : `Out of service until ${whenLabel(until)} — do not drive`;
}

export default function OutOfServiceScreen({ record }: { record: OutOfServiceRecord }) {
  const [view, setView] = useState<"notice" | "hours">("notice");
  const m = statusMeta("over");
  const from = unixOf(record.fromUtc);

  if (view === "hours") {
    return (
      <div style={{ flex: 1, minWidth: 0, display: "flex", flexDirection: "column", background: colors.mainBg }}>
        <div
          style={{
            flex: "none",
            display: "flex",
            alignItems: "center",
            gap: gap.row,
            padding: `${gap.row}px ${gap.page}px`,
            background: m.bg,
            borderBottom: `1px solid ${m.bd}`,
          }}
        >
          <span aria-hidden style={{ color: m.t, fontWeight: 800, fontSize: type.value }}>
            {OUT_OF_SERVICE_GLYPH}
          </span>
          <span style={{ flex: 1, fontFamily: fonts.body, fontWeight: 600, fontSize: type.label, color: m.t }}>
            {outOfServiceTitle(record)}. Off Duty and On Duty only — nothing here moves the bus.
          </span>
          <TouchButton variant="secondary" onClick={() => setView("notice")}>
            Back to the notice
          </TouchButton>
        </div>
        <div style={{ flex: 1, minHeight: 0 }}>
          <Hours readOnly />
        </div>
      </div>
    );
  }

  return (
    <div
      role="region"
      aria-label="Out of service"
      style={{
        flex: 1,
        minWidth: 0,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: gap.page,
        background: colors.mainBg,
      }}
    >
      <div
        style={{
          width: "100%",
          maxWidth: 860,
          background: colors.cardBg,
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
            {OUT_OF_SERVICE_GLYPH}
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
              Out of service · declared {from === null ? "" : whenLabel(from)}
            </div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, color: colors.headingBright, lineHeight: 1.1 }}>
              {outOfServiceTitle(record)}
            </div>
          </div>
        </div>

        <div style={{ fontFamily: fonts.body, fontSize: type.value, color: colors.textPrimary, lineHeight: 1.45 }}>
          <span style={{ fontFamily: fonts.semiCondensed, fontSize: type.group, letterSpacing: ".14em", textTransform: "uppercase", color: colors.textLabel, display: "block", marginBottom: 4 }}>
            Reason
          </span>
          {record.reason}
        </div>

        <div style={{ fontFamily: fonts.body, fontSize: type.label, color: colors.textSecondary, lineHeight: 1.55 }}>
          Nothing on this tablet will assign, claim or start a trip for you while this stands, and Driving and
          Personal Conveyance are unavailable on the duty control. You may still record Off Duty and On Duty for
          yard work. Call dispatch before doing anything else.
        </div>

        <div style={{ display: "flex", gap: gap.row, flexWrap: "wrap" }}>
          <TouchButton variant="danger" onClick={callDispatch} style={{ minHeight: touch.rail, fontSize: 20, flex: "1 1 320px" }}>
            Call dispatch {COMPANY.dispatchPhone}
          </TouchButton>
          <TouchButton variant="secondary" onClick={() => setView("hours")} style={{ minHeight: touch.rail, flex: "1 1 220px" }}>
            View hours
          </TouchButton>
        </div>
      </div>
    </div>
  );
}
