// COPIED FROM Dispatcher/components/ui/MetricTile.tsx — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
import { colors, fonts } from "@/lib/theme";

export function MetricTile({
  icon,
  iconBg,
  iconColor,
  label,
  value,
  valueColor,
  borderColor,
  onClick,
}: {
  icon: string;
  iconBg: string;
  iconColor: string;
  label: string;
  value: number | string;
  valueColor: string;
  borderColor?: string;
  onClick?: () => void;
}) {
  return (
    <div
      onClick={onClick}
      style={{
        padding: "14px 15px",
        borderRadius: 11,
        background: colors.cardBg,
        border: `1px solid ${borderColor ?? colors.borderSubtle}`,
        boxShadow: colors.shadowCard,
        cursor: "pointer",
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 7, marginBottom: 9 }}>
        <span
          style={{
            width: 16,
            height: 16,
            borderRadius: 4,
            background: iconBg,
            color: iconColor,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontSize: 10,
            fontWeight: 800,
          }}
        >
          {icon}
        </span>
        <span
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 10,
            letterSpacing: ".1em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          {label}
        </span>
      </div>
      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 32,
          lineHeight: 1,
          color: valueColor,
          fontVariantNumeric: "tabular-nums",
        }}
      >
        {value}
      </div>
    </div>
  );
}
