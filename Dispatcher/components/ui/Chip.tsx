import type { CSSProperties } from "react";
import { chipStyle, colors, fonts, statusMeta, svcMeta, type ServiceType, type StatusKind } from "@/lib/theme";

export function ServiceChip({
  svc,
  label,
  compact = false,
}: {
  svc: ServiceType;
  label?: string;
  compact?: boolean;
}) {
  const m = svcMeta(svc);
  return (
    <span style={chipStyle(m.chipBg, m.chipBd, m.chipTx)}>
      <span style={{ fontSize: 10, lineHeight: 1, color: m.accent }}>{m.glyph}</span>
      {label ?? (compact ? undefined : m.label)}
    </span>
  );
}

export function StatusChip({ kind, label }: { kind: StatusKind; label: string }) {
  const m = statusMeta(kind);
  return (
    <span style={chipStyle(m.bg, m.bd, m.t)}>
      <span
        style={{
          width: 16,
          height: 16,
          flex: "none",
          borderRadius: 4,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 10,
          fontWeight: 800,
          background: m.c,
          color: m.bt,
        }}
      >
        {m.g}
      </span>
      {label}
    </span>
  );
}

export function StatusBadge({
  kind,
  size = 16,
}: {
  kind: StatusKind;
  size?: number;
}) {
  const m = statusMeta(kind);
  const style: CSSProperties = {
    width: size,
    height: size,
    flex: "none",
    borderRadius: 4,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    fontSize: size <= 15 ? 9 : 10,
    fontWeight: 800,
    background: m.c,
    color: m.bt,
  };
  return <span style={style}>{m.g}</span>;
}

export function MonoTag({ children, color }: { children: React.ReactNode; color?: string }) {
  return (
    <span
      style={{
        fontFamily: fonts.mono,
        fontSize: 10,
        padding: "2px 6px",
        border: `1px solid ${colors.borderStrong}`,
        borderRadius: 4,
        color: color ?? colors.textDim,
      }}
    >
      {children}
    </span>
  );
}
