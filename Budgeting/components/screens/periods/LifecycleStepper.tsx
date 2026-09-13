"use client";

import { colors, fonts } from "@/lib/theme";
import { StatusBadge, StatusChip } from "@/components/ui/Chip";
import { periodKind, type LifecycleStep } from "@/lib/api/budgeting";

// The five lifecycle states in order (planningProgress → PERIOD_STATE_ORDER), read left to
// right. Three renderings, every one of them glyph + label so nothing rests on colour: a done
// step is the teal ✓ badge beside its name, the current step is the period's own StatusChip
// (the same kind + label the list row shows), a pending step is a numbered outline beside its
// name. Forward only — there is no way back, so nothing here is clickable.

export default function LifecycleStepper({ steps }: { steps: LifecycleStep[] }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
      {steps.map((s, i) => (
        <div key={s.id} style={{ display: "flex", alignItems: "center", gap: 8 }}>
          {i > 0 && (
            <span
              aria-hidden
              style={{ width: 18, height: 1, background: colors.borderStrong, flex: "none" }}
            />
          )}
          {s.status === "current" ? (
            <StatusChip kind={periodKind(s.id)} label={s.label} />
          ) : (
            <span
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 6,
                fontFamily: fonts.body,
                fontWeight: 600,
                fontSize: 11.5,
                color: colors.textDim,
                whiteSpace: "nowrap",
              }}
            >
              {s.status === "done" ? <StatusBadge kind="ontime" /> : <PendingMark n={i + 1} />}
              {s.label}
            </span>
          )}
        </div>
      ))}
    </div>
  );
}

/** A numbered outline for a state not yet reached — the number is the glyph. */
function PendingMark({ n }: { n: number }) {
  return (
    <span
      style={{
        width: 16,
        height: 16,
        flex: "none",
        borderRadius: 4,
        border: `1px solid ${colors.borderStrong}`,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        fontFamily: fonts.mono,
        fontSize: 9,
        fontWeight: 700,
        color: colors.textDim,
      }}
    >
      {n}
    </span>
  );
}
