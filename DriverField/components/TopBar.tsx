"use client";

import { colors, fonts } from "@/lib/theme";
import { bar, radius, touch } from "@/lib/tablet";
import { SyncPill } from "@/components/ui-tablet/SyncPill";
import { DutyChip } from "@/components/ui-tablet/DutyChip";
import HeaderClock from "@/components/HeaderClock";
import { logout } from "@/lib/auth";
import { currentDriver, activeTrip } from "@/lib/data";
import { dutyStateOf, OUT_OF_SERVICE_GLYPH, PAPER_LOG_GLYPH, PAPER_LOG_LABEL } from "@/lib/hos/display";
import { HosWire } from "@/lib/hos/engine";
import { useHosLedger } from "@/lib/hos/useHosLedger";
import { TabletChip } from "@/components/screens/shared";

// ADAPTED FROM Budgeting/components/TopBar.tsx, but the right-hand side is rebuilt from
// scratch. What changed and why:
//
//   • 72px tall, not 56 — every control in it must clear the 44px touch floor.
//   • The search field and its ⌘K hint are GONE. There is no keyboard on this tablet, and a
//     control nobody can use still costs the width a driver needs for the things they can.
//   • The bell and the primary-action pill are replaced by the three things a driver needs
//     visible at all times without navigating: what duty state they are in, whether their work
//     has reached the server, and which trip they are on.
//
// The duty chip reads the ledger's current status (lib/hos/ledger.ts) through dutyMeta() — the
// same source as the DutyControl and the Hours screen, so the bar can never disagree with the
// figures. Beside it, two compliance states that must be visible on every screen for as long
// as they stand: OUT OF SERVICE (vermillion with its own ✕ — a glyph override on the `over`
// colour, never a new hex) and PAPER-LOG MODE (gold ✎: an ELD malfunction is open, so the
// driver keeps paper RODS until it is fixed). Both come from the same ledger snapshot. The sync
// pill is not optional chrome — architecture §8 requires sync state to be visible at all times,
// and this is the only place in the app that is always on screen.

export default function TopBar() {
  const { evaluation, outOfService, paperLogMode } = useHosLedger();
  const duty = dutyStateOf(evaluation.currentStatus);

  return (
    <div
      style={{
        height: bar.height,
        flex: "none",
        background: colors.topbarBg,
        borderBottom: `1px solid ${colors.border}`,
        display: "flex",
        alignItems: "center",
        gap: 16,
        padding: "0 20px",
      }}
    >
      <div style={{ display: "inline-flex", alignItems: "baseline", gap: 3, flex: "none" }}>
        <span
          style={{
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 24,
            letterSpacing: ".02em",
            color: colors.headingBright,
          }}
        >
          NORTHERN
        </span>
        <span
          style={{
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 24,
            letterSpacing: ".02em",
            color: colors.amberText,
          }}
        >
          LINK
        </span>
        <span
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 12,
            letterSpacing: ".16em",
            textTransform: "uppercase",
            color: colors.textDim,
            marginLeft: 8,
          }}
        >
          Driver
        </span>
      </div>

      {/* Active trip — the answer to "what am I doing", readable without navigating. */}
      <div
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: 10,
          minHeight: touch.min,
          padding: "0 14px",
          borderRadius: radius.control,
          background: colors.inputBg,
          border: `1px solid ${colors.borderSubtle}`,
        }}
      >
        <span style={{ fontFamily: fonts.mono, fontSize: 14, color: colors.textSecondary }}>
          {activeTrip.tripNumber}
        </span>
        <span style={{ fontFamily: fonts.body, fontSize: 15, color: colors.textDim }}>
          {activeTrip.origin} → {activeTrip.destination}
        </span>
      </div>

      <div style={{ flex: 1 }} />

      {/* Duty state: colour + glyph + the word, from dutyMeta(). Never the tint alone. */}
      <DutyChip state={duty} size="bar" />

      {outOfService !== null ? <TabletChip kind="over" glyph={OUT_OF_SERVICE_GLYPH} label={HosWire.OutOfServiceLabel} /> : null}
      {paperLogMode ? <TabletChip kind="soon" glyph={PAPER_LOG_GLYPH} label={PAPER_LOG_LABEL} /> : null}

      <SyncPill />

      <HeaderClock />

      <div
        style={{
          fontFamily: fonts.body,
          fontSize: 15,
          color: colors.textSecondary,
          whiteSpace: "nowrap",
        }}
      >
        {currentDriver.name}
      </div>

      <button
        onClick={() => void logout()}
        style={{
          minHeight: touch.min,
          minWidth: touch.min,
          padding: "0 16px",
          borderRadius: radius.control,
          border: `1px solid ${colors.borderStrong}`,
          background: colors.cardBg,
          color: colors.textMuted,
          fontFamily: fonts.semiCondensed,
          fontSize: 13,
          fontWeight: 600,
          letterSpacing: ".1em",
          textTransform: "uppercase",
          cursor: "pointer",
          flex: "none",
        }}
      >
        Sign out
      </button>
    </div>
  );
}
