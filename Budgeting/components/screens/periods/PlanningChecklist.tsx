"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import type { LineStep } from "@/lib/api/budgeting";

// "Where am I in the planning?" — the two line steps from planningProgress. Transitions are not
// gated on these (finalizing an empty plan is allowed server-side), so this is what makes an
// empty plan visible instead. Each row is chip (glyph + Done/Pending) + step name + the coverage
// detail, so the answer survives grayscale.

export default function PlanningChecklist({ steps }: { steps: LineStep[] }) {
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
              {s.count} {s.count === 1 ? "line" : "lines"} · {s.detail}
            </span>
          </div>
        ))}
      </div>
    </Panel>
  );
}
