"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { formatCadPrecise } from "@/lib/money";
import {
  itemCount,
  PRIORITY_GLYPHS,
  PRIORITY_KINDS,
  type PriorityBucket,
} from "@/lib/api/budgeting";
import { Num } from "@/components/screens/shared";

// "What would I cut first?" — the period's expense items totalled by priority, Must have at the
// top and Nice to have at the bottom (the order a zero-based planner cuts from). Every priority is
// listed even at $0, so an empty bucket reads as a fact rather than as a missing row.
//
// The buckets are summed client-side from the loaded items by priorityBreakdown. They are a
// breakdown only: the dashboard's headline tiles are the server's own period totals and are never
// re-derived from these. Each row is chip (per-priority glyph + written label) + count + total —
// never colour alone.

export default function PriorityBreakdown({
  title,
  buckets,
}: {
  title: string;
  buckets: PriorityBucket[];
}) {
  return (
    <Panel>
      <SectionLabel>{title}</SectionLabel>
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {buckets.map((b) => (
          <div key={b.priority} style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <StatusChip
              kind={PRIORITY_KINDS[b.priority]}
              glyph={PRIORITY_GLYPHS[b.priority]}
              label={b.label}
            />
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              {itemCount(b.count)}
            </span>
            <span style={{ marginLeft: "auto" }}>
              <Num size={13} weight={600} color={colors.textPrimary}>
                {formatCadPrecise(b.totalCad)}
              </Num>
            </span>
          </div>
        ))}
      </div>
    </Panel>
  );
}
