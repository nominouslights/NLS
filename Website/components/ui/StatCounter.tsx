import type { Stat } from "@/lib/types";
import { colors, fonts } from "@/lib/theme";

export default function StatCounter({ stat }: { stat: Stat }) {
  return (
    <div
      style={{
        textAlign: "center",
        padding: "20px 12px",
        borderRadius: 12,
        background: colors.tealTint,
        border: `1px solid ${colors.border}`,
      }}
    >
      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 42,
          lineHeight: 1,
          color: colors.tealDark,
        }}
      >
        {stat.value}
      </div>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontWeight: 600,
          fontSize: 14.5,
          color: colors.text,
          marginTop: 8,
        }}
      >
        {stat.label}
      </div>
    </div>
  );
}
