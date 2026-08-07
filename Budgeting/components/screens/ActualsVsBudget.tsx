"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import { MonoTag } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { formatCad } from "@/lib/api/format";
import { actuals, budgetCodes, formatDeltaCad, varianceKind } from "@/lib/data";
import {
  EmptyNote,
  MockTag,
  Num,
  PeriodPicker,
  Screen,
  TableHead,
  periodLabel,
} from "@/components/screens/shared";
import { StatusBadge } from "@/components/ui/Chip";

// Plan against reality, line by line. The delta column pairs a status badge with a signed
// number — the sign is written out (+ / −) rather than implied by colour, so the direction
// survives a grayscale print and any colour-vision deficiency.
//
// Actuals will come from QuickBooks reconciliation once Stage 6.1 lands; today they are mock.

export default function ActualsVsBudget({
  periodId,
  onSelectPeriod,
}: {
  periodId: string;
  onSelectPeriod: (id: string) => void;
}) {
  const rows = actuals.filter((a) => a.periodId === periodId);

  const planned = rows.reduce((sum, r) => sum + r.planned, 0);
  const actual = rows.reduce((sum, r) => sum + r.actual, 0);
  const delta = actual - planned;
  const deltaPct = planned === 0 ? null : (delta / planned) * 100;
  const totalKind = varianceKind(deltaPct);

  return (
    <Screen
      eyebrow={`Performance · ${periodLabel(periodId)}`}
      title="Actuals vs Budget"
      right={<PeriodPicker periodId={periodId} onSelect={onSelectPeriod} />}
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
          icon="●"
          iconBg="rgba(31,111,178,.10)"
          iconColor={colors.blue}
          label="Actual"
          value={formatCad(actual)}
          valueColor={colors.headingBright}
        />
        <MetricTile
          icon={totalKind === "ontime" ? "✓" : totalKind === "soon" ? "◐" : "▲"}
          iconBg={
            totalKind === "ontime"
              ? "rgba(0,158,115,.10)"
              : totalKind === "soon"
                ? "rgba(225,176,0,.14)"
                : "rgba(213,94,0,.10)"
          }
          iconColor={
            totalKind === "ontime" ? "#007A59" : totalKind === "soon" ? colors.amberText : "#AD4C00"
          }
          label="Net delta"
          value={formatDeltaCad(delta)}
          valueColor={
            totalKind === "ontime" ? "#007A59" : totalKind === "soon" ? colors.amberText : "#AD4C00"
          }
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
          By budget code
        </div>
        <MockTag />
      </div>

      {rows.length === 0 ? (
        <EmptyNote>No actuals recorded for {periodLabel(periodId)}.</EmptyNote>
      ) : (
        <>
          <TableHead
            columns={[
              { label: "Code" },
              { label: "Planned", align: "right" },
              { label: "Actual", align: "right" },
              { label: "Delta", align: "right" },
            ]}
          />
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {rows.map((r) => {
              const rowDelta = r.actual - r.planned;
              const rowPct = r.planned === 0 ? null : (rowDelta / r.planned) * 100;
              const kind = varianceKind(rowPct);
              const name = budgetCodes.find((c) => c.code === r.code)?.name ?? r.code;

              return (
                <div
                  key={r.id}
                  style={{
                    ...rowSurface(false),
                    padding: "11px 14px",
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                    cursor: "default",
                  }}
                >
                  <div style={{ flex: "1 1 auto", minWidth: 0 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
                      <MonoTag>{r.code}</MonoTag>
                      <span
                        style={{
                          fontFamily: fonts.body,
                          fontWeight: 600,
                          fontSize: 12.5,
                          color: colors.textPrimary,
                        }}
                      >
                        {name}
                      </span>
                    </div>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num>{formatCad(r.planned)}</Num>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num>{formatCad(r.actual)}</Num>
                  </div>
                  <div
                    style={{
                      width: 150,
                      flex: "none",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "flex-end",
                      gap: 8,
                    }}
                  >
                    <StatusBadge kind={kind} />
                    <Num>{formatDeltaCad(rowDelta)}</Num>
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
