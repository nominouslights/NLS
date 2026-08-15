"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import { MonoTag } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { formatCad } from "@/lib/api/format";
import type { BudgetPeriod } from "@/lib/types";
import { allocations, budgetCodes } from "@/lib/data";
import {
  EmptyNote,
  MockTag,
  Num,
  PeriodPicker,
  Screen,
  TableHead,
  periodLabel,
} from "@/components/screens/shared";

// What the selected period's money is committed to, code by code, with a KPI row on top.
// Unallocated is the number that matters in zero-based budgeting — every dollar is supposed to
// be justified into a code, so a non-zero remainder is work outstanding, not slack.

export default function Allocations({
  periods,
  periodId,
  onSelectPeriod,
  onOpenCode,
}: {
  periods: BudgetPeriod[];
  periodId: string;
  onSelectPeriod: (id: string) => void;
  onOpenCode: (id: string) => void;
}) {
  const period = periods.find((p) => p.id === periodId);
  const rows = allocations.filter((a) => a.periodId === periodId);

  const planned = period?.planned ?? 0;
  const allocated = rows.reduce((sum, a) => sum + a.amount, 0);
  const unallocated = planned - allocated;

  return (
    <Screen
      eyebrow={`Planning · ${periodLabel(periods, periodId)}`}
      title="Allocations"
      right={<PeriodPicker periods={periods} periodId={periodId} onSelect={onSelectPeriod} />}
    >
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(190px, 1fr))",
          gap: 12,
          marginBottom: 18,
        }}
      >
        <MetricTile
          icon="◧"
          iconBg="rgba(31,111,178,.10)"
          iconColor={colors.blue}
          label="Planned"
          value={formatCad(planned)}
          valueColor={colors.headingBright}
        />
        <MetricTile
          icon="✓"
          iconBg="rgba(0,158,115,.10)"
          iconColor="#007A59"
          label="Allocated"
          value={formatCad(allocated)}
          valueColor="#007A59"
        />
        <MetricTile
          icon={unallocated === 0 ? "✓" : "◐"}
          iconBg={unallocated === 0 ? "rgba(0,158,115,.10)" : "rgba(225,176,0,.14)"}
          iconColor={unallocated === 0 ? "#007A59" : colors.amberText}
          label="Unallocated"
          value={formatCad(unallocated)}
          valueColor={unallocated === 0 ? "#007A59" : colors.amberText}
        />
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".14em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          Allocations
        </div>
        <MockTag />
      </div>

      {rows.length === 0 ? (
        <EmptyNote>
          Nothing allocated in {periodLabel(periods, periodId)} yet — this period is still a draft.
        </EmptyNote>
      ) : (
        <>
          <TableHead
            columns={[{ label: "Code" }, { label: "Amount", align: "right" }]}
          />
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {rows.map((a) => {
              const code = budgetCodes.find((c) => c.code === a.code);
              return (
                <div
                  key={a.id}
                  onClick={() => code && onOpenCode(code.id)}
                  style={{
                    ...rowSurface(false),
                    padding: "11px 14px",
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                  }}
                >
                  <div style={{ flex: "1 1 auto", minWidth: 0 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
                      <MonoTag>{a.code}</MonoTag>
                      <span
                        style={{
                          fontFamily: fonts.body,
                          fontWeight: 600,
                          fontSize: 12.5,
                          color: colors.textPrimary,
                        }}
                      >
                        {code?.name ?? a.code}
                      </span>
                    </div>
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 11.5,
                        color: colors.textDim,
                        marginTop: 3,
                      }}
                    >
                      {a.note}
                    </div>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num size={13.5}>{formatCad(a.amount)}</Num>
                  </div>
                </div>
              );
            })}
          </div>
        </>
      )}
    </Screen>
  );
}
