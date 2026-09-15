"use client";

import type { ReactNode } from "react";
import { chipStyle, colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { PageHeader } from "@/components/ui/Panel";

// The page-shell convention every screen follows, matching Budgeting's screens/shared.tsx but
// at tablet spacing: a fixed header block, then a scrolling body that owns the overflow. The
// `detailfade` class is the shared 0.15s entrance from app/globals.css (it respects
// prefers-reduced-motion).

export function Screen({
  eyebrow,
  title,
  right,
  children,
}: {
  eyebrow: string;
  title: string;
  right?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div
      style={{ display: "flex", flexDirection: "column", height: "100%" }}
      className="detailfade"
    >
      <div style={{ flex: "none", padding: `${gap.section}px ${gap.page}px ${gap.row}px` }}>
        <PageHeader eyebrow={eyebrow} title={title} right={right} />
      </div>
      <div
        style={{
          flex: 1,
          minHeight: 0,
          overflowY: "auto",
          padding: `0 ${gap.page}px ${gap.page}px`,
        }}
      >
        {children}
      </div>
    </div>
  );
}

/**
 * The "this is mock data" tag. EVERY screen in this app carries one.
 *
 * Budgeting keys its mock rows to ids no real record can match, so a screen already wired to
 * the API shows its empty state instead of a fiction. That trick needs real data to work
 * against, and this app has none — every value on every screen is invented. So the tag is the
 * mechanism: nothing here should be demonstrable as working software, and the moment sync and
 * the API land, these tags come off screen by screen.
 */
export function MockTag() {
  return (
    <span
      style={{
        fontFamily: fonts.mono,
        fontSize: 12,
        padding: "4px 9px",
        border: `1px solid ${colors.borderStrong}`,
        borderRadius: 5,
        color: colors.textDim,
        letterSpacing: ".04em",
        whiteSpace: "nowrap",
      }}
      title="Mock data. This app calls no domain endpoint — only auth is live."
    >
      MOCK
    </span>
  );
}

/** A numeric cell. Tabular figures so columns line up and a changing digit can't jitter it. */
export function Num({
  children,
  color,
  size = type.value,
  weight = 500,
}: {
  children: ReactNode;
  color?: string;
  size?: number;
  weight?: number;
}) {
  return (
    <span
      style={{
        fontFamily: fonts.mono,
        fontSize: size,
        fontWeight: weight,
        fontVariantNumeric: "tabular-nums",
        color: color ?? colors.textSecondary,
        whiteSpace: "nowrap",
      }}
    >
      {children}
    </span>
  );
}

/** A tappable card row — the tablet's answer to a table row. */
export function CardRow({
  children,
  onClick,
  accent,
  muted,
}: {
  children: ReactNode;
  onClick?: () => void;
  accent?: string;
  muted?: boolean;
}) {
  return (
    <div
      onClick={onClick}
      role={onClick ? "button" : undefined}
      tabIndex={onClick ? 0 : undefined}
      onKeyDown={
        onClick
          ? (e) => {
              if (e.key === "Enter" || e.key === " ") {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
      style={{
        display: "flex",
        alignItems: "center",
        gap: gap.row,
        minHeight: touch.primary + 12,
        padding: "14px 18px",
        marginBottom: 10,
        borderRadius: radius.panel,
        background: colors.cardBg,
        border: `1px solid ${colors.borderSubtle}`,
        boxShadow: accent ? `inset 4px 0 0 ${accent}, ${colors.shadowCard}` : colors.shadowCard,
        cursor: onClick ? "pointer" : "default",
        opacity: muted ? 0.62 : 1,
      }}
    >
      {children}
    </div>
  );
}

/** Section heading inside a screen body. */
export function Heading({ children, right }: { children: ReactNode; right?: ReactNode }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: gap.row,
        margin: `${gap.section}px 0 10px`,
      }}
    >
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: type.group,
          letterSpacing: ".16em",
          textTransform: "uppercase",
          color: colors.textLabel,
        }}
      >
        {children}
      </div>
      {right}
    </div>
  );
}

/** Status chip at tablet size — colour, glyph and label, always all three. */
export function TabletChip({ kind, label }: { kind: StatusKind; label: string }) {
  const m = statusMeta(kind);
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 7,
        padding: "5px 12px",
        borderRadius: 7,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        color: m.t,
        fontFamily: fonts.semiCondensed,
        fontSize: 14,
        fontWeight: 600,
        letterSpacing: ".06em",
        textTransform: "uppercase",
        whiteSpace: "nowrap",
      }}
    >
      <span aria-hidden>{m.g}</span>
      {label}
    </span>
  );
}

/** Neutral empty state, styled like the rest of the surface rather than as an error. */
export function EmptyNote({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        ...chipStyle(colors.inputBg, colors.borderSubtle, colors.textDim),
        fontSize: type.label,
        padding: "16px 18px",
      }}
    >
      {children}
    </div>
  );
}

/** Label above a value inside a card. */
export function FieldLine({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div style={{ minWidth: 0 }}>
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
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: type.label,
          color: colors.textPrimary,
          marginTop: 3,
        }}
      >
        {value}
      </div>
    </div>
  );
}
