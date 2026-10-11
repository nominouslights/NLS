"use client";

import { dutyMeta, fonts } from "@/lib/theme";
import { radius, touch } from "@/lib/tablet";
import type { DutyState } from "@/lib/types";

// APP-LOCAL. A duty-status chip: colour + glyph + the word, from the copied lib/theme.ts's
// dutyMeta() — the FOUR states, Personal Conveyance included, which shares Off Duty's grey and is
// told apart by its own glyph (◇) and the label, never by colour. TabletChip cannot render this:
// it keys on StatusKind, and On Duty's blue is a duty palette, not a status band.
//
// Two sizes: the top bar's (clears the 44px touch floor so it sits level with the pill and the
// clock) and the row size for the Hours entries.

export function DutyChip({ state, size = "row" }: { state: DutyState; size?: "bar" | "row" }) {
  const m = dutyMeta(state);
  const bar = size === "bar";
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 8,
        minHeight: bar ? touch.min : undefined,
        padding: bar ? "0 14px" : "5px 12px",
        borderRadius: bar ? radius.control : 7,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        color: m.text,
        fontFamily: fonts.semiCondensed,
        fontSize: 14,
        fontWeight: 600,
        letterSpacing: bar ? ".08em" : ".06em",
        textTransform: "uppercase",
        whiteSpace: "nowrap",
      }}
    >
      <span aria-hidden>{m.glyph}</span>
      {state}
    </span>
  );
}
