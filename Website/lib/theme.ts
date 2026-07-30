import type { CSSProperties } from "react";

// Public marketing site palette — a light theme built on the Northern Link brand
// colors. Mirrors the shape of Dispatcher/lib/theme.ts (tokens + style helpers),
// but deliberately NOT the admin app's dark navy: this is the public face.
// Platform rule: status/error colors never stand alone — always icon + text label.
export const colors = {
  // Brand
  teal: "#009E73",
  tealDark: "#007A59",
  gold: "#E1B000",
  vermillion: "#D55E00", // form errors ONLY — always paired with ⚠ + text label

  // Ink
  ink: "#122B24", // deep spruce — headings
  text: "#33443F",
  textMuted: "#5E716B",

  // Surfaces
  pageBg: "#FFFFFF",
  sectionAlt: "#F2F8F5",
  cardBg: "#FFFFFF",
  footerBg: "#0E2A22",
  border: "#DCE7E2",

  // Derived tints (kept here so components never invent ad hoc colors)
  tealTint: "rgba(0,158,115,.10)",
  tealTintStrong: "rgba(0,158,115,.16)",
  goldTint: "rgba(225,176,0,.14)",
  vermillionTint: "rgba(213,94,0,.08)",
  footerText: "#CFE0DA",
  footerTextDim: "#8FA69E",
} as const;

export const fonts = {
  condensed: "'Barlow Condensed', sans-serif",
  semiCondensed: "'Barlow Semi Condensed', sans-serif",
  body: "'Barlow', sans-serif",
} as const;

export type ButtonVariant = "primary" | "secondary" | "ghost" | "onDark";

export function buttonStyle(
  variant: ButtonVariant = "primary",
  size: "md" | "lg" = "md",
): CSSProperties {
  const base: CSSProperties = {
    display: "inline-flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    padding: size === "lg" ? "14px 28px" : "11px 20px",
    borderRadius: 8,
    fontFamily: fonts.semiCondensed,
    fontWeight: 600,
    fontSize: size === "lg" ? 17 : 15.5,
    lineHeight: 1.2,
    letterSpacing: "0.02em",
    cursor: "pointer",
    textDecoration: "none",
    whiteSpace: "nowrap",
    transition: "background .15s ease, color .15s ease, border-color .15s ease",
  };
  switch (variant) {
    case "primary":
      return { ...base, background: colors.teal, color: "#FFFFFF", border: `1px solid ${colors.tealDark}` };
    case "secondary":
      return { ...base, background: colors.cardBg, color: colors.tealDark, border: `1px solid ${colors.teal}` };
    case "onDark":
      return { ...base, background: "#FFFFFF", color: colors.tealDark, border: "1px solid #FFFFFF" };
    case "ghost":
    default:
      return { ...base, background: "transparent", color: colors.tealDark, border: `1px solid ${colors.border}` };
  }
}

export function sectionStyle(alt = false): CSSProperties {
  return {
    padding: "72px 0",
    background: alt ? colors.sectionAlt : colors.pageBg,
  };
}

export function containerStyle(maxWidth = 1120): CSSProperties {
  return {
    maxWidth,
    margin: "0 auto",
    padding: "0 24px",
  };
}

export function cardStyle(padding = 24): CSSProperties {
  return {
    background: colors.cardBg,
    border: `1px solid ${colors.border}`,
    borderRadius: 12,
    padding,
    boxShadow: "0 1px 3px rgba(18,43,36,.06)",
  };
}

export function chipStyle(bg: string, bd: string, tx: string): CSSProperties {
  return {
    display: "inline-flex",
    alignItems: "center",
    gap: 6,
    padding: "3px 10px",
    borderRadius: 999,
    fontFamily: fonts.semiCondensed,
    fontWeight: 600,
    fontSize: 12.5,
    background: bg,
    border: `1px solid ${bd}`,
    color: tx,
    whiteSpace: "nowrap",
  };
}

export function headingStyle(size: number, color: string = colors.ink): CSSProperties {
  return {
    fontFamily: fonts.condensed,
    fontWeight: 700,
    fontSize: size,
    lineHeight: 1.1,
    letterSpacing: "0.01em",
    color,
    margin: 0,
    textTransform: "uppercase" as const,
  };
}

export function bodyStyle(size = 16, color: string = colors.text): CSSProperties {
  return {
    fontFamily: fonts.body,
    fontSize: size,
    lineHeight: 1.65,
    color,
    margin: 0,
  };
}

// ── Form tokens ────────────────────────────────────────────────────────────
export function labelStyle(): CSSProperties {
  return {
    fontFamily: fonts.semiCondensed,
    fontWeight: 600,
    fontSize: 14,
    color: colors.ink,
    display: "block",
    marginBottom: 6,
  };
}

export function inputStyle(hasError: boolean): CSSProperties {
  return {
    width: "100%",
    padding: "11px 12px",
    borderRadius: 8,
    border: `1.5px solid ${hasError ? colors.vermillion : colors.border}`,
    background: hasError ? colors.vermillionTint : "#FFFFFF",
    fontFamily: fonts.body,
    fontSize: 15.5,
    color: colors.ink,
    outlineColor: colors.teal,
  };
}

export function errorTextStyle(): CSSProperties {
  return {
    display: "inline-flex",
    alignItems: "center",
    gap: 6,
    marginTop: 6,
    fontFamily: fonts.body,
    fontWeight: 600,
    fontSize: 13.5,
    color: colors.vermillion,
  };
}
