"use client";

import type { ReactNode } from "react";
import { chipStyle, colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { periods } from "@/lib/data";

// The page-shell convention every screen in both consoles follows: a fixed header block at
// 20px 26px 14px, then a scrolling body at 0 26px 26px that owns the overflow. The
// `detailfade` class is the shared 0.15s entrance from app/globals.css (it respects
// prefers-reduced-motion). Dispatcher repeats this markup per screen; here it is one component,
// producing the same DOM.

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
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 14px" }}>
        <PageHeader eyebrow={eyebrow} title={title} right={right} />
      </div>
      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "0 26px 26px" }}>
        {children}
      </div>
    </div>
  );
}

/**
 * Period switcher for the header's right slot. A row of pills rather than ui/PeriodNav's
 * ‹ / › stepper, because budget periods are a short fixed list to pick from, not an unbounded
 * calendar to step through. Reuses PeriodNav's pill treatment so it still reads as the same
 * control family.
 */
export function PeriodPicker({
  periodId,
  onSelect,
}: {
  periodId: string;
  onSelect: (id: string) => void;
}) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
      {periods.map((p) => {
        const active = p.id === periodId;
        return (
          <button
            key={p.id}
            onClick={() => onSelect(p.id)}
            style={{
              padding: "5px 11px",
              borderRadius: 7,
              border: `1px solid ${active ? colors.borderActive : colors.borderStrong}`,
              background: active ? colors.cardBgActive : colors.cardBg,
              color: active ? colors.headingBright : colors.textMuted,
              fontFamily: fonts.semiCondensed,
              fontSize: 11.5,
              letterSpacing: ".06em",
              textTransform: "uppercase",
              cursor: "pointer",
            }}
          >
            {p.label}
          </button>
        );
      })}
    </div>
  );
}

/** A period's display label, for headers and empty states. */
export function periodLabel(periodId: string): string {
  return periods.find((p) => p.id === periodId)?.label ?? periodId;
}

/**
 * A numeric cell. Tabular figures so columns of money line up and a changing seconds-style
 * digit can't jitter the layout — the same reasoning as HeaderClock's clock.
 */
export function Num({
  children,
  color,
  size = 12.5,
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

/**
 * Status chip plus a signed number. The number carries its own explicit + or −, so the sign
 * survives grayscale and any colour-vision deficiency — the chip's tint is never the only thing
 * saying "this is over".
 */
export function SignedStatus({
  kind,
  label,
  value,
}: {
  kind: StatusKind;
  label: string;
  value: string;
}) {
  const m = statusMeta(kind);
  return (
    <span style={{ display: "inline-flex", alignItems: "center", gap: 9 }}>
      <StatusChip kind={kind} label={label} />
      <Num color={m.t} weight={500}>
        {value}
      </Num>
    </span>
  );
}

/** Simple table header row — shared so column rhythm matches across screens. */
export function TableHead({ columns }: { columns: { label: string; align?: "right" }[] }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 12,
        padding: "0 14px 8px",
        fontFamily: fonts.semiCondensed,
        fontSize: 9.5,
        letterSpacing: ".14em",
        textTransform: "uppercase",
        color: colors.textLabel,
      }}
    >
      {columns.map((c, i) => (
        <div
          key={c.label}
          style={{
            flex: i === 0 ? "1 1 auto" : "none",
            width: i === 0 ? undefined : 150,
            textAlign: c.align ?? "left",
          }}
        >
          {c.label}
        </div>
      ))}
    </div>
  );
}

/** Neutral empty state, styled like the rest of the surface rather than as an error. */
export function EmptyNote({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        ...chipStyle(colors.inputBg, colors.borderSubtle, colors.textDim),
        fontSize: 12,
        padding: "10px 13px",
      }}
    >
      {children}
    </div>
  );
}

/** The "this is mock data" tag, so nothing on screen is mistaken for a real figure. */
export function MockTag() {
  return (
    <span
      style={{
        fontFamily: fonts.mono,
        fontSize: 9.5,
        padding: "2px 6px",
        border: `1px solid ${colors.borderStrong}`,
        borderRadius: 4,
        color: colors.textDim,
        letterSpacing: ".04em",
      }}
      title="Mock data — the Budgeting API lands in Stage 6.1"
    >
      MOCK
    </span>
  );
}
