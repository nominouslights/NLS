"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import type { ChecklistStep } from "@/lib/api/budgeting";

// "Where am I in the zero-based plan?" — the four checklist rows from planningProgress, which
// maps them onto the steps of zero-based budgeting (see that function's comment; step 4, track
// all month, deliberately has no row because actuals are still mock).
//
// Transitions are not gated on any of these — finalizing an empty plan is allowed server-side —
// so this is what makes an incomplete plan visible instead. Every row is chip (glyph + written
// status) + step name + detail sentence, and both the kind and the status word come from
// planningProgress, so this component picks no colour and takes no branch.

export default function PlanningChecklist({ steps }: { steps: ChecklistStep[] }) {
  return (
    <Panel>
      <SectionLabel>Zero-based checklist</SectionLabel>
      <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
        {steps.map((s) => (
          <div
            key={s.id}
            style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}
          >
            <StatusChip kind={s.kind} label={s.status} />
            <span
              style={{
                fontFamily: fonts.body,
                fontWeight: 600,
                fontSize: 12.5,
                color: colors.textPrimary,
              }}
            >
              {s.label}
            </span>
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              {s.detail}
            </span>
          </div>
        ))}
      </div>
    </Panel>
  );
}
