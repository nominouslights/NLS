// COPIED FROM Dispatcher/lib/theme.ts — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
import type { CSSProperties } from "react";

export const colors = {
  pageBg: "#EEF2F6",
  topbarBg: "#FFFFFF",
  railBg: "#F7F9FC",
  mainBg: "#EEF2F6",
  detailBg: "#F7F9FC",
  cardBg: "#FFFFFF",
  cardBgActive: "#EAF2FB",
  inputBg: "#F7F9FC",

  borderStrong: "#B8C7D9",
  border: "#D5DEE9",
  borderSubtle: "#E4EAF1",
  borderActive: "#7FA9D4",

  textPrimary: "#1C2B3A",
  headingBright: "#102A43",
  textSecondary: "#334E68",
  textMuted: "#486581",
  textDim: "#56718C",
  textFaint: "#7B93AC", // decorative only — use textDim where the text carries meaning
  textLabel: "#527191",

  navy: "#102A43",
  blue: "#1F6FB2",
  skyBlue: "#17779E",
  amber: "#E8A020", // fills/borders/icons only — pair with navy text; use amberText for amber-as-text
  amberText: "#9C6500",

  shadowCard: "0 1px 2px rgba(16,42,67,.05), 0 2px 8px rgba(16,42,67,.06)",
  shadowPop: "0 8px 28px rgba(16,42,67,.16)",
  scrim: "rgba(16,42,67,.45)",
  focusRing: "0 0 0 3px rgba(31,111,178,.25)",
} as const;

export type StatusKind = "ontime" | "soon" | "over" | "off" | "info";
export type ServiceType =
  | "alamos"
  | "community"
  | "nihb"
  | "charter"
  | "cargo"
  | "grocery";

export interface StatusMeta {
  c: string; // solid colour (badge bg) — protected status hexes, never change
  t: string; // text colour (AA on white surfaces)
  bt: string; // text colour on the solid badge bg
  g: string; // glyph
  bg: string; // chip bg
  bd: string; // chip border
}

const STATUS_META: Record<StatusKind, StatusMeta> = {
  ontime: {
    c: "#009E73",
    t: "#007A59",
    bt: "#FFFFFF",
    g: "✓",
    bg: "rgba(0,158,115,.10)",
    bd: "rgba(0,158,115,.38)",
  },
  soon: {
    c: "#E1B000",
    t: "#8A6D00",
    bt: "#102A43",
    g: "◐",
    bg: "rgba(225,176,0,.14)",
    bd: "rgba(225,176,0,.48)",
  },
  over: {
    c: "#D55E00",
    t: "#AD4C00",
    bt: "#FFFFFF",
    g: "▲",
    bg: "rgba(213,94,0,.10)",
    bd: "rgba(213,94,0,.42)",
  },
  off: {
    c: "#7A8899",
    t: "#5A6B7D",
    bt: "#FFFFFF",
    g: "—",
    bg: "rgba(122,136,153,.10)",
    bd: "rgba(122,136,153,.38)",
  },
  info: {
    c: "#1F6FB2",
    t: "#1F6FB2",
    bt: "#FFFFFF",
    g: "●",
    bg: "rgba(31,111,178,.08)",
    bd: "rgba(31,111,178,.32)",
  },
};

export function statusMeta(kind: StatusKind): StatusMeta {
  return STATUS_META[kind] ?? STATUS_META.info;
}

export interface ServiceMeta {
  label: string;
  accent: string;
  glyph: string;
  chipBg: string;
  chipBd: string;
  chipTx: string;
}

const SERVICE_META: Record<ServiceType, ServiceMeta> = {
  alamos: {
    label: "Alamos / Corporate",
    accent: "#E8A020",
    glyph: "▲",
    chipBg: "rgba(232,160,32,.13)",
    chipBd: "rgba(232,160,32,.5)",
    chipTx: "#9C6500",
  },
  community: {
    label: "Community",
    accent: "#1F6FB2",
    glyph: "●",
    chipBg: "rgba(31,111,178,.08)",
    chipBd: "rgba(31,111,178,.35)",
    chipTx: "#1F6FB2",
  },
  nihb: {
    label: "NIHB Medical",
    accent: "#17779E",
    glyph: "✚",
    chipBg: "rgba(23,119,158,.09)",
    chipBd: "rgba(23,119,158,.35)",
    chipTx: "#17779E",
  },
  charter: {
    label: "Charter",
    accent: "#486581",
    glyph: "★",
    chipBg: "rgba(72,101,129,.08)",
    chipBd: "rgba(72,101,129,.3)",
    chipTx: "#40566C",
  },
  cargo: {
    label: "Cargo / Parcel",
    accent: "#56718C",
    glyph: "▪",
    chipBg: "rgba(86,113,140,.08)",
    chipBd: "rgba(86,113,140,.3)",
    chipTx: "#4C6378",
  },
  grocery: {
    label: "Grocery",
    accent: "#1F6FB2",
    glyph: "◗",
    chipBg: "rgba(31,111,178,.07)",
    chipBd: "rgba(31,111,178,.3)",
    chipTx: "#1F6FB2",
  },
};

export const SERVICE_SHORT: Record<ServiceType, string> = {
  alamos: "Alamos",
  community: "Community",
  nihb: "NIHB",
  charter: "Charter",
  cargo: "Cargo",
  grocery: "Grocery",
};

export function svcMeta(svc: ServiceType): ServiceMeta {
  return SERVICE_META[svc] ?? SERVICE_META.community;
}

export type DutyStatus = "Off Duty" | "On Duty" | "Driving";

export interface DutyMeta {
  glyph: string;
  color: string; // solid accent (badge bg)
  text: string; // AA text colour on white surfaces
  bg: string; // chip bg
  bd: string; // chip border
}

// Duty status is color + icon + label (accessible-status-colors rule): the
// colour never stands alone. Reuses the protected status hexes — Off Duty grey,
// On Duty blue, Driving teal (an active/"go" semantic).
const DUTY_META: Record<DutyStatus, DutyMeta> = {
  "Off Duty": {
    glyph: "—",
    color: "#7A8899",
    text: "#5A6B7D",
    bg: "rgba(122,136,153,.10)",
    bd: "rgba(122,136,153,.38)",
  },
  "On Duty": {
    glyph: "●",
    color: "#1F6FB2",
    text: "#1F6FB2",
    bg: "rgba(31,111,178,.08)",
    bd: "rgba(31,111,178,.32)",
  },
  Driving: {
    glyph: "▸",
    color: "#009E73",
    text: "#007A59",
    bg: "rgba(0,158,115,.10)",
    bd: "rgba(0,158,115,.38)",
  },
};

export function dutyMeta(status: DutyStatus): DutyMeta {
  return DUTY_META[status] ?? DUTY_META["Off Duty"];
}

export const fonts = {
  condensed: "'Barlow Condensed', sans-serif",
  semiCondensed: "'Barlow Semi Condensed', sans-serif",
  body: "'Barlow', sans-serif",
  mono: "'JetBrains Mono', monospace",
} as const;

export function chipStyle(bg: string, bd: string, tx: string): CSSProperties {
  return {
    display: "inline-flex",
    alignItems: "center",
    gap: 6,
    padding: "3px 9px",
    borderRadius: 7,
    fontFamily: fonts.body,
    fontWeight: 600,
    fontSize: 11.5,
    background: bg,
    border: `1px solid ${bd}`,
    color: tx,
    whiteSpace: "nowrap",
  };
}

export function rowSurface(active: boolean, accent: string = colors.blue): CSSProperties {
  return {
    cursor: "pointer",
    borderRadius: 9,
    border: `1px solid ${active ? colors.borderActive : colors.borderSubtle}`,
    background: active ? colors.cardBgActive : colors.cardBg,
    boxShadow: active ? `inset 3px 0 0 ${accent}, ${colors.shadowCard}` : colors.shadowCard,
  };
}

export function badgeStyle(bg: string, size = 16, text = "#FFFFFF"): CSSProperties {
  return {
    width: size,
    height: size,
    flex: "none",
    borderRadius: 4,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    fontSize: size <= 15 ? 9 : 10,
    fontWeight: 800,
    background: bg,
    color: text,
  };
}
