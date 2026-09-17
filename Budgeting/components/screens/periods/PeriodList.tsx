"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { StatusChip } from "@/components/ui/Chip";
import { formatCad, formatUtcDate } from "@/lib/api/format";
import { formatDeltaCad } from "@/lib/money";
import {
  hasPlannedAmounts,
  zbbBalance,
  zbbBalanceDisplay,
  PERIOD_STATE_LABELS,
} from "@/lib/api/budgeting";
import { Num } from "@/components/screens/shared";

// List mode of the Budget Periods screen: every period at full width, one row each, and enough
// room per row to answer "where does this one stand?" without opening it — label, dates, the
// state chip, the two planned totals the wire carries, and the zero-based verdict on them.
//
// The row is the way into a period: clicking it takes the whole content area over (the screen's
// period mode), so the row has to be worth reading on its own rather than being a tab stop in a
// master column.
//
// Nothing here picks a colour. The state chip's kind is carried on the row
// (BudgetPeriod.pk, derived in lib/api/budgeting.ts) and the balance chip's comes from
// zbbBalanceDisplay, which also writes the words — so both survive grayscale, and the signed
// figure beside the balance writes its own + or − as text.

export default function PeriodList({
  periods,
  onOpen,
}: {
  periods: BudgetPeriod[];
  onOpen: (id: string) => void;
}) {
  return (
    <div
      style={{
        borderTop: `1px solid ${colors.border}`,
        paddingTop: 14,
        display: "flex",
        flexDirection: "column",
        gap: 9,
      }}
    >
      {periods.map((p) => {
        const m = statusMeta(p.pk);
        const balance = zbbBalance(p);
        const display = zbbBalanceDisplay(balance, hasPlannedAmounts(p));
        return (
          <div
            key={p.id}
            onClick={() => onOpen(p.id)}
            style={{ ...rowSurface(false, m.c), padding: "14px 16px" }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 14,
                flexWrap: "wrap",
              }}
            >
              <div style={{ flex: "1 1 220px", minWidth: 0 }}>
                <div
                  style={{
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 17,
                    color: colors.headingBright,
                    lineHeight: 1.2,
                  }}
                >
                  {p.label}
                </div>
                <div
                  style={{
                    fontFamily: fonts.body,
                    fontSize: 11.5,
                    color: colors.textDim,
                    marginTop: 3,
                  }}
                >
                  {formatUtcDate(p.startsOn)} — {formatUtcDate(p.endsOn)}
                </div>
              </div>
              <StatusChip kind={p.pk} label={PERIOD_STATE_LABELS[p.state]} />
              <Figure label="Planned revenue" value={formatCad(p.plannedRevenue)} />
              <Figure label="Planned expense" value={formatCad(p.plannedExpense)} />
              <div style={{ flex: "none", display: "flex", alignItems: "center", gap: 8 }}>
                <StatusChip kind={display.kind} label={display.headline} />
                <Num size={12} color={statusMeta(display.kind).t}>
                  {formatDeltaCad(balance.amount)}
                </Num>
              </div>
            </div>
          </div>
        );
      })}
    </div>
  );
}

/** One labelled figure in a row: the caption above, the tabular number below. */
function Figure({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ flex: "none", minWidth: 106 }}>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
          marginBottom: 2,
        }}
      >
        {label}
      </div>
      <Num size={13}>{value}</Num>
    </div>
  );
}
