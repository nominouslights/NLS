"use client";

import type { ReactNode } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { radius, type } from "@/lib/tablet";

// APP-LOCAL. The big-number tile on the Today screen.
//
// components/ui/MetricTile.tsx is a protected copy and reads fine at desktop size, but the
// number a driver needs to read from a mounted tablet without leaning in is the whole point of
// this screen — so the figure gets tablet.type.metric (34px) rather than the console's ~22px.
//
// The status glyph and label sit next to the value, never behind it as a tint alone.

export function TouchTile({
  label,
  value,
  unit,
  kind,
  statusLabel,
  footnote,
}: {
  label: string;
  value: string;
  unit?: string;
  kind?: StatusKind;
  statusLabel?: string;
  footnote?: ReactNode;
}) {
  const m = kind ? statusMeta(kind) : null;

  return (
    <div
      style={{
        flex: "1 1 0",
        minWidth: 0,
        padding: "16px 18px",
        borderRadius: radius.panel,
        background: colors.cardBg,
        border: `1px solid ${colors.borderSubtle}`,
        boxShadow: colors.shadowCard,
      }}
    >
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: type.group,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
        }}
      >
        {label}
      </div>

      <div style={{ display: "flex", alignItems: "baseline", gap: 6, marginTop: 6 }}>
        <span
          style={{
            fontFamily: fonts.mono,
            fontSize: type.metric,
            fontWeight: 500,
            fontVariantNumeric: "tabular-nums",
            color: m ? m.t : colors.headingBright,
            lineHeight: 1.1,
          }}
        >
          {value}
        </span>
        {unit ? (
          <span
            style={{
              fontFamily: fonts.body,
              fontSize: type.label,
              color: colors.textDim,
            }}
          >
            {unit}
          </span>
        ) : null}
      </div>

      {m && statusLabel ? (
        <div
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: 6,
            marginTop: 8,
            padding: "3px 10px",
            borderRadius: 7,
            background: m.bg,
            border: `1px solid ${m.bd}`,
            color: m.t,
            fontFamily: fonts.semiCondensed,
            fontSize: 13,
            fontWeight: 600,
            letterSpacing: ".06em",
            textTransform: "uppercase",
          }}
        >
          <span aria-hidden>{m.g}</span>
          {statusLabel}
        </div>
      ) : null}

      {footnote ? (
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 14,
            color: colors.textDim,
            marginTop: 8,
            lineHeight: 1.5,
          }}
        >
          {footnote}
        </div>
      ) : null}
    </div>
  );
}
