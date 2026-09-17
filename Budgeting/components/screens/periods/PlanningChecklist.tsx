"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import type { ChecklistStep } from "@/lib/api/budgeting";

// "Where am I in the planning?" — every non-lifecycle step planningProgress returns: the two
// line rows, then the zero-based balance row. Transitions are not gated on any of them
// (finalizing an unbalanced or empty plan is allowed server-side, by decision), so this is what
// makes an unfinished plan visible instead of forbidden.
//
// A pure render: the chip (glyph + Done/Pending), the step's name, and the sentence
// planningProgress already wrote. Nothing is re-derived or re-phrased here, so adding a fourth
// row later is a change to one function, not to two files that must agree.

export default function PlanningChecklist({ steps }: { steps: ChecklistStep[] }) {
  return (
    <Panel>
      <SectionLabel>Planning checklist</SectionLabel>
      <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
        {steps.map((s) => (
          <div
            key={s.id}
            style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}
          >
            <StatusChip kind={s.done ? "ontime" : "info"} label={s.done ? "Done" : "Pending"} />
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
