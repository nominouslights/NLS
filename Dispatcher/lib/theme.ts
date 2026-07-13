import type { CSSProperties } from "react";

export const colors = {
  pageBg: "#070E19",
  topbarBg: "#0F1E33",
  railBg: "#0A1729",
  mainBg: "#0B1626",
  detailBg: "#0C1A2C",
  cardBg: "#0F1E33",
  cardBgActive: "#16283F",
  inputBg: "#0A1729",

  borderStrong: "#24405f",
  border: "#1E3350",
  borderSubtle: "#152941",
  borderActive: "#2f557d",

  textPrimary: "#E8EEF5",
  headingBright: "#F2F6FB",
  textSecondary: "#c2d0e0",
  textMuted: "#9fb2c8",
  textDim: "#6B8099",
  textFaint: "#4d688a",
  textLabel: "#8fa6c0",

  navy: "#08192D",
  blue: "#3B8DD4",
  skyBlue: "#7EC8F0",
  amber: "#E8A020",
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
  c: string; // solid colour (badge bg)
  t: string; // text colour
  g: string; // glyph
  bg: string; // chip bg
  bd: string; // chip border
}

const STATUS_META: Record<StatusKind, StatusMeta> = {
  ontime: {
    c: "#009E73",
    t: "#38d3a6",
    g: "✓",
    bg: "rgba(0,158,115,.13)",
    bd: "rgba(0,158,115,.35)",
  },
  soon: {
    c: "#E1B000",
    t: "#ecc94b",
    g: "◐",
    bg: "rgba(225,176,0,.13)",
    bd: "rgba(225,176,0,.35)",
  },
  over: {
    c: "#D55E00",
    t: "#f0803f",
    g: "▲",
    bg: "rgba(213,94,0,.14)",
    bd: "rgba(213,94,0,.4)",
  },
  off: {
    c: "#7A8899",
    t: "#9fb2c8",
    g: "—",
    bg: "rgba(122,136,153,.12)",
    bd: "rgba(122,136,153,.35)",
  },
  info: {
    c: "#3B8DD4",
    t: "#7EC8F0",
    g: "●",
    bg: "rgba(59,141,212,.13)",
    bd: "rgba(59,141,212,.35)",
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
    chipBg: "rgba(232,160,32,.12)",
    chipBd: "rgba(232,160,32,.4)",
    chipTx: "#E8A020",
  },
  community: {
    label: "Community",
    accent: "#3B8DD4",
    glyph: "●",
    chipBg: "rgba(59,141,212,.12)",
    chipBd: "rgba(59,141,212,.4)",
    chipTx: "#7EC8F0",
  },
  nihb: {
    label: "NIHB Medical",
    accent: "#7EC8F0",
    glyph: "✚",
    chipBg: "rgba(126,200,240,.12)",
    chipBd: "rgba(126,200,240,.4)",
    chipTx: "#7EC8F0",
  },
  charter: {
    label: "Charter",
    accent: "#c2d0e0",
    glyph: "★",
    chipBg: "rgba(194,208,224,.1)",
    chipBd: "rgba(194,208,224,.3)",
    chipTx: "#c2d0e0",
  },
  cargo: {
    label: "Cargo / Parcel",
    accent: "#8ba4c2",
    glyph: "▪",
    chipBg: "rgba(139,164,194,.1)",
    chipBd: "rgba(139,164,194,.3)",
    chipTx: "#aebfd2",
  },
  grocery: {
    label: "Grocery",
    accent: "#3B8DD4",
    glyph: "◗",
    chipBg: "rgba(59,141,212,.1)",
    chipBd: "rgba(59,141,212,.35)",
    chipTx: "#7EC8F0",
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
    borderRadius: 6,
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
    boxShadow: active ? `inset 3px 0 0 ${accent}` : undefined,
  };
}

export function badgeStyle(bg: string, size = 16): CSSProperties {
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
    color: "#04121f",
  };
}
