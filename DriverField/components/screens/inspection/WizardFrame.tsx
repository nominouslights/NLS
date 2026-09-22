"use client";

import type { ReactNode } from "react";
import { colors, statusMeta, type StatusKind } from "@/lib/theme";
import { gap, radius, wizard } from "@/lib/tablet";
import { PageHeader } from "@/components/ui/Panel";
import { MockTag, TabletChip } from "../shared";

// APP-LOCAL. The frame every wizard step renders inside.
//
// WHY NOT screens/shared.tsx's `Screen`: that shell gives its body `overflowY: "auto"`, which
// is exactly what this must not have. A driver answering one question of a legal attestation
// must never be able to leave part of that question off screen — a scrollbar here means a
// half-read check can be answered. So this is three FIXED rows (header, question, footer) with
// a progress bar between the first two, and NO scroll container anywhere in the file. The
// review step is the only surface in the flow that scrolls, and it owns that itself.
//
// Sizes come from lib/tablet.ts's `wizard` group — never a hardcoded number here, and never a
// number from lib/theme.ts, which owns colour and type family only.
//
// <MockTag/> lives in this header, so EVERY step carries one. DriverField/CLAUDE.md's rule is
// per-surface, and a wizard step is a surface a driver can land on.
//
// The 0.15s entrance is `detailfade` from the copied app/globals.css, keyed on the step id. It
// is already disabled under prefers-reduced-motion at globals.css:90-93 — no new animation, and
// no new CSS.

export function WizardFrame({
  eyebrow,
  title,
  stepId,
  progressLabel,
  progressFraction,
  progressKind,
  children,
  footer,
}: {
  eyebrow: string;
  title: string;
  /** Re-keys the body so each step fades in. An id, never an index. */
  stepId: string;
  progressLabel: string;
  /** 0–1. Determinate, monotonic, and never able to exceed 1 — see lib/inspectionSteps.ts. */
  progressFraction: number;
  progressKind: StatusKind;
  children: ReactNode;
  footer: ReactNode;
}) {
  const m = statusMeta(progressKind);
  const pct = Math.round(Math.min(1, Math.max(0, progressFraction)) * 100);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%", overflow: "hidden" }}>
      <div style={{ flex: "none", padding: `${gap.section}px ${gap.page}px ${gap.row}px` }}>
        <PageHeader
          eyebrow={eyebrow}
          title={title}
          right={
            <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
              <TabletChip kind={progressKind} label={progressLabel} />
              <MockTag />
            </div>
          }
        />
      </div>

      {/* THE BAR IS NOT A STATUS CARRIER, which is why it is allowed to be colour-only.
          It encodes POSITION, not good/bad, and it takes its colour from the same StatusKind as
          the chip directly above it — which carries colour + glyph + label, all three. The
          colour here is redundant reinforcement of a message already fully stated in words. It
          still carries role/aria-valuenow/aria-label so it is not silent to a screen reader. */}
      <div style={{ flex: "none", padding: `0 ${gap.page}px` }}>
        <div
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={pct}
          aria-label={progressLabel}
          style={{
            height: wizard.barH,
            borderRadius: wizard.barH,
            background: colors.borderSubtle,
            overflow: "hidden",
          }}
        >
          <div style={{ width: `${pct}%`, height: "100%", background: m.c }} />
        </div>
      </div>

      {/* flex:1 + minHeight:0 and NO overflow property. A step that does not fit is a layout
          bug to fix, not something to hide behind a scrollbar. */}
      <div
        key={stepId}
        className="detailfade"
        style={{
          flex: 1,
          minHeight: 0,
          display: "flex",
          flexDirection: "column",
          padding: `${gap.section}px ${gap.page}px 0`,
        }}
      >
        {children}
      </div>

      <div
        style={{
          flex: "none",
          minHeight: wizard.footerH,
          display: "flex",
          alignItems: "center",
          gap: gap.row,
          padding: `0 ${gap.page}px`,
          borderTop: `1px solid ${colors.border}`,
          background: colors.detailBg,
          borderTopLeftRadius: radius.panel,
          borderTopRightRadius: radius.panel,
        }}
      >
        {footer}
      </div>
    </div>
  );
}
