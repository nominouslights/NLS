"use client";

import { colors, fonts } from "@/lib/theme";
import { useToday } from "@/lib/useToday";

/**
 * The header's date + running clock. Kept as its own component so the
 * once-a-second tick re-renders only this pill, not the whole TopBar.
 */
export default function HeaderClock() {
  const today = useToday();

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 7,
        flex: "none",
        padding: "5px 12px",
        border: `1px solid ${colors.borderStrong}`,
        borderRadius: 8,
        cursor: "pointer",
        color: colors.textSecondary,
        fontFamily: fonts.semiCondensed,
        fontSize: 12,
        letterSpacing: ".06em",
      }}
    >
      <span style={{ width: 6, height: 6, borderRadius: "50%", background: colors.blue }} />
      TODAY · {today.label}
      <span style={{ width: 1, height: 13, background: colors.borderStrong, flex: "none" }} />
      <span
        style={{
          fontFamily: fonts.mono,
          fontSize: 12,
          letterSpacing: "0",
          color: colors.textPrimary,
          // Tabular figures keep the seconds from jittering the layout.
          fontVariantNumeric: "tabular-nums",
        }}
      >
        {today.time}
      </span>
      <span style={{ fontSize: 10, letterSpacing: ".06em", color: colors.textDim }}>{today.zone}</span>
    </div>
  );
}
