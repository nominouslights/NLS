import type { CSSProperties, ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";

export function Panel({
  children,
  style,
  borderColor,
}: {
  children: ReactNode;
  style?: CSSProperties;
  borderColor?: string;
}) {
  return (
    <div
      style={{
        padding: "15px 16px",
        background: colors.cardBg,
        border: `1px solid ${borderColor ?? colors.borderSubtle}`,
        borderRadius: 12,
        boxShadow: colors.shadowCard,
        ...style,
      }}
    >
      {children}
    </div>
  );
}

export function SectionLabel({ children }: { children: ReactNode }) {
  return (
    <div
      style={{
        fontFamily: fonts.semiCondensed,
        fontSize: 9.5,
        letterSpacing: ".14em",
        textTransform: "uppercase",
        color: colors.textLabel,
        marginBottom: 11,
      }}
    >
      {children}
    </div>
  );
}

export function DetailRow({ label, value, valueStyle }: { label: string; value: ReactNode; valueStyle?: CSSProperties }) {
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "space-between",
        fontFamily: fonts.body,
        fontSize: 12.5,
        gap: 12,
      }}
    >
      <span style={{ color: colors.textDim }}>{label}</span>
      <span style={{ color: colors.textSecondary, fontWeight: 500, textAlign: "right", ...valueStyle }}>{value}</span>
    </div>
  );
}

export function PageHeader({
  eyebrow,
  title,
  right,
}: {
  eyebrow: string;
  title: string;
  right?: ReactNode;
}) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "flex-end",
        justifyContent: "space-between",
        gap: 12,
      }}
    >
      <div>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 10.5,
            letterSpacing: ".16em",
            textTransform: "uppercase",
            color: colors.textFaint,
            marginBottom: 3,
          }}
        >
          {eyebrow}
        </div>
        <h1
          style={{
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 30,
            lineHeight: 1,
            color: colors.headingBright,
            margin: 0,
          }}
        >
          {title}
        </h1>
      </div>
      {right}
    </div>
  );
}
