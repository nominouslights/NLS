"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { formatCad } from "@/lib/api/format";
import { actuals, budgetCodes, varianceKind } from "@/lib/data";
import { MockTag, Num, Screen, periodLabel } from "@/components/screens/shared";

// Placeholder report cards. The first one is real enough to be worth building now: revenue mix
// against the Rider Express service-mix benchmark in the architecture reference (Section 5.3),
// which is the comparison the owner actually asks for. The rest are named so Stage 6.1 knows
// what to fill in rather than inventing a report surface from scratch.

/** Rider Express service-mix benchmark — architecture.md Section 5.3. */
const MIX_BENCHMARK: { code: string; low: number; high: number }[] = [
  { code: "ZBB-CREW-01", low: 70, high: 75 },
  { code: "ZBB-NIHB-01", low: 15, high: 20 },
  { code: "ZBB-CHTR-02", low: 5, high: 10 },
  { code: "ZBB-COMM-01", low: 2, high: 5 },
];

export default function Reports({ periodId }: { periodId: string }) {
  const revenueCodes = new Set(
    budgetCodes.filter((c) => c.category === "Revenue").map((c) => c.code),
  );
  const revenueLines = actuals.filter((a) => a.periodId === periodId && revenueCodes.has(a.code));
  const totalRevenue = revenueLines.reduce((sum, r) => sum + r.actual, 0);

  return (
    <Screen eyebrow={`Performance · ${periodLabel(periodId)}`} title="Reports" right={<MockTag />}>
      <Panel style={{ marginBottom: 12 }}>
        <SectionLabel>Revenue mix vs. benchmark</SectionLabel>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 12,
            color: colors.textMuted,
            lineHeight: 1.6,
            marginBottom: 13,
          }}
        >
          Share of {formatCad(totalRevenue)} actual revenue by service, against the Rider Express
          benchmark bands. A share inside its band is on benchmark; outside is worth explaining.
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          {MIX_BENCHMARK.map((b) => {
            const line = revenueLines.find((r) => r.code === b.code);
            const share = totalRevenue === 0 ? 0 : ((line?.actual ?? 0) / totalRevenue) * 100;
            const inBand = share >= b.low && share <= b.high;
            // Distance outside the band, expressed as a percentage of the nearest edge, so it
            // maps onto the same variance thresholds the rest of the app uses.
            const edge = share < b.low ? b.low : b.high;
            const kind = inBand ? "ontime" : varianceKind(((share - edge) / edge) * 100);
            const name = budgetCodes.find((c) => c.code === b.code)?.name ?? b.code;

            return (
              <div
                key={b.code}
                style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}
              >
                <div
                  style={{
                    flex: "1 1 200px",
                    minWidth: 0,
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    color: colors.textPrimary,
                  }}
                >
                  {name}
                </div>
                <Num>{share.toFixed(1)}%</Num>
                <span
                  style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, flex: "none" }}
                >
                  benchmark {b.low}–{b.high}%
                </span>
                <StatusChip kind={kind} label={inBand ? "On benchmark" : "Off benchmark"} />
              </div>
            );
          })}
        </div>
      </Panel>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))",
          gap: 12,
        }}
      >
        <PlaceholderCard
          title="Period close pack"
          body="Planned, allocated, actual and variance for every code, as a printable summary for the period close."
        />
        <PlaceholderCard
          title="Budget code history"
          body="One code's planned-versus-actual across periods, to show whether its zero-based justification keeps holding up."
        />
        <PlaceholderCard
          title="Cost per kilometre"
          body="Expense codes against corridor kilometres driven, once trip distances are joined in."
        />
      </div>
    </Screen>
  );
}

function PlaceholderCard({ title, body }: { title: string; body: string }) {
  return (
    <Panel>
      <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 7 }}>
        <div
          style={{
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 16,
            color: colors.headingBright,
          }}
        >
          {title}
        </div>
        <StatusChip kind="off" label="Stage 6.1" />
      </div>
      <div
        style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6 }}
      >
        {body}
      </div>
    </Panel>
  );
}
