"use client";

import type { CSSProperties, ReactNode } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { radius, touch, type } from "@/lib/tablet";

// APP-LOCAL. Not a copy of anything, and never copied anywhere else.
//
// components/ui/Button.tsx is a protected copy of Dispatcher's and sits at roughly 34px tall —
// fine for a mouse, unusable for a gloved thumb in a moving vehicle. This is the tablet
// counterpart, not a fork: it composes theme.ts colours with tablet.ts sizes and shares none of
// the copy's markup. Where a copied primitive DOES work at size (Chip, Panel, ModalShell,
// MetricTile) use the copy — only add here when the desktop one is geometrically unusable.

export type TouchButtonVariant = "primary" | "secondary" | "danger";

export function TouchButton({
  children,
  onClick,
  variant = "primary",
  disabled,
  disabledReason,
  full,
  style,
}: {
  children: ReactNode;
  onClick?: () => void;
  variant?: TouchButtonVariant;
  disabled?: boolean;
  /**
   * Why this is disabled. Rendered as the title, and REQUIRED in spirit for anything disabled
   * by a rule rather than by transient state — a driver staring at a dead button with no reason
   * given will call dispatch, which is the outcome this app exists to avoid.
   */
  disabledReason?: string;
  full?: boolean;
  style?: CSSProperties;
}) {
  const palette = variantPalette(variant);

  return (
    <button
      onClick={onClick}
      disabled={disabled}
      title={disabled ? disabledReason : undefined}
      style={{
        minHeight: touch.primary,
        minWidth: touch.primary,
        width: full ? "100%" : undefined,
        padding: "0 22px",
        borderRadius: radius.control,
        border: `1px solid ${palette.border}`,
        background: palette.bg,
        color: palette.text,
        fontFamily: fonts.semiCondensed,
        fontSize: 17,
        fontWeight: 600,
        letterSpacing: ".08em",
        textTransform: "uppercase",
        cursor: disabled ? "not-allowed" : "pointer",
        opacity: disabled ? 0.45 : 1,
        ...style,
      }}
    >
      {children}
    </button>
  );
}

function variantPalette(variant: TouchButtonVariant) {
  if (variant === "primary") {
    return { bg: colors.navy, border: colors.navy, text: "#FFFFFF" };
  }
  if (variant === "danger") {
    const over = statusMeta("over");
    return { bg: over.c, border: over.c, text: over.bt };
  }
  return { bg: colors.cardBg, border: colors.borderStrong, text: colors.textSecondary };
}

/**
 * A status-tinted action — Board, No-show, Claim. The StatusKind carries its glyph onto the
 * button, so the meaning survives grayscale exactly as it does on a chip.
 */
export function StatusButton({
  kind,
  label,
  onClick,
  active,
  disabled,
  disabledReason,
}: {
  kind: StatusKind;
  label: string;
  onClick?: () => void;
  active?: boolean;
  disabled?: boolean;
  disabledReason?: string;
}) {
  const m = statusMeta(kind);

  return (
    <button
      onClick={onClick}
      disabled={disabled}
      title={disabled ? disabledReason : undefined}
      aria-pressed={active}
      style={{
        minHeight: touch.primary,
        padding: "0 18px",
        borderRadius: radius.control,
        border: `1px solid ${active ? m.c : m.bd}`,
        background: active ? m.c : m.bg,
        color: active ? m.bt : m.t,
        fontFamily: fonts.semiCondensed,
        fontSize: type.label,
        fontWeight: 600,
        letterSpacing: ".06em",
        textTransform: "uppercase",
        display: "inline-flex",
        alignItems: "center",
        gap: 8,
        cursor: disabled ? "not-allowed" : "pointer",
        opacity: disabled ? 0.45 : 1,
      }}
    >
      <span aria-hidden>{m.g}</span>
      {label}
    </button>
  );
}
