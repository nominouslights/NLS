"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { StatusChip } from "@/components/ui/Chip";
import { formatCad, formatUtcDate } from "@/lib/api/format";
import { PERIOD_STATE_LABELS } from "@/lib/api/budgeting";
import { Num } from "@/components/screens/shared";

// The master column of the Budget Periods screen: one row per period — label, date range, the
// state chip, and the two planned totals the wire now carries. Each row's kind is carried on the
// row (BudgetPeriod.pk, derived in lib/api/budgeting.ts), so this list never picks a colour
// itself; the accent stripe on the selected row reuses that kind's colour beside the chip that
// already carries its glyph and label.

export default function PeriodList({
  periods,
  selectedId,
  onSelect,
}: {
  periods: BudgetPeriod[];
  selectedId: string;
  onSelect: (id: string) => void;
}) {
  return (
    <div
      style={{
        overflowY: "auto",
        padding: "16px 18px 16px 0",
        borderRight: `1px solid ${colors.border}`,
        display: "flex",
        flexDirection: "column",
        gap: 8,
      }}
    >
      {periods.map((p) => {
        const active = p.id === selectedId;
        const m = statusMeta(p.pk);
        return (
          <div
            key={p.id}
            onClick={() => onSelect(p.id)}
            style={{ ...rowSurface(active, m.c), padding: "11px 13px" }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 9,
                marginBottom: 4,
                flexWrap: "wrap",
              }}
            >
              <div
                style={{
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 16,
                  color: colors.headingBright,
                  lineHeight: 1.2,
                  flex: "1 1 auto",
                  minWidth: 0,
                }}
              >
                {p.label}
              </div>
              <StatusChip kind={p.pk} label={PERIOD_STATE_LABELS[p.state]} />
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              {formatUtcDate(p.startsOn)} — {formatUtcDate(p.endsOn)}
            </div>
            <div style={{ marginTop: 5, display: "flex", alignItems: "center", gap: 6 }}>
              <Num size={11.5}>Rev {formatCad(p.plannedRevenue)}</Num>
              <span style={{ color: colors.textDim, fontSize: 11.5 }}>·</span>
              <Num size={11.5}>Exp {formatCad(p.plannedExpense)}</Num>
            </div>
          </div>
        );
      })}
    </div>
  );
}
