"use client";

import type { ReactNode } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { gap, radius, type } from "@/lib/tablet";

// APP-LOCAL. A full-width note at the top of a screen — "this fires In Transit", "this is not
// wired up yet", "you are out of hours".
//
// Always colour + glyph + label. Never colour alone, and never a bare coloured bar: the four
// protected status hexes only ever appear alongside the glyph and words that mean the same
// thing, so the message survives glare, grayscale and every colour-vision deficiency.

export function StatusBanner({
  kind,
  title,
  children,
}: {
  kind: StatusKind;
  title: string;
  children?: ReactNode;
}) {
  const m = statusMeta(kind);

  return (
    <div
      style={{
        display: "flex",
        gap: gap.tight + 4,
        padding: "14px 16px",
        borderRadius: radius.control,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        marginBottom: gap.section,
      }}
    >
      <span
        style={{ color: m.t, fontSize: type.label, fontWeight: 800, lineHeight: "24px" }}
        aria-hidden
      >
        {m.g}
      </span>
      <div style={{ minWidth: 0 }}>
        <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 18, color: m.t }}>
          {title}
        </div>
        {children ? (
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: type.label,
              color: colors.textSecondary,
              marginTop: 3,
              lineHeight: 1.55,
            }}
          >
            {children}
          </div>
        ) : null}
      </div>
    </div>
  );
}
