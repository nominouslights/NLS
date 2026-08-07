"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { MonoTag } from "@/components/ui/Chip";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { formatCad } from "@/lib/api/format";
import {
  budgetCodes,
  formatDeltaCad,
  formatDeltaPct,
  variance,
  varianceLabel,
} from "@/lib/data";
import {
  EmptyNote,
  MockTag,
  Num,
  PeriodPicker,
  Screen,
  SignedStatus,
  TableHead,
  periodLabel,
} from "@/components/screens/shared";

// The screen where the status system earns its keep, and the one most at risk of falling into
// colour-alone. Every variance is rendered three ways at once: the band's glyph, its text label
// ("On plan" / "Watch" / "Over threshold"), and an explicitly signed percentage. Strip the
// colour entirely and the meaning is still fully there.
//
// Bands live in lib/data.ts (varianceKind), not here, so any future report agrees with this
// screen by construction: within 5% on plan, 5–15% watch, beyond 15% over threshold, and no
// baseline where planned is zero.

export default function Variance({
  periodId,
  onSelectPeriod,
  onOpenCode,
}: {
  periodId: string;
  onSelectPeriod: (id: string) => void;
  onOpenCode: (id: string) => void;
}) {
  const rows = variance
    .filter((v) => v.periodId === periodId)
    .slice()
    .sort((a, b) => Math.abs(b.deltaPct ?? 0) - Math.abs(a.deltaPct ?? 0));

  const breaches = rows.filter((v) => v.vk === "over");

  return (
    <Screen
      eyebrow={`Performance · ${periodLabel(periodId)}`}
      title="Variance"
      right={<PeriodPicker periodId={periodId} onSelect={onSelectPeriod} />}
    >
      <Panel style={{ marginBottom: 16 }}>
        <SectionLabel>Thresholds</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
          <DetailRow label="On plan" value="Within ±5% of the planned figure" />
          <DetailRow label="Watch" value="±5% to ±15% — review before period close" />
          <DetailRow label="Over threshold" value="Beyond ±15% — needs a decision" />
        </div>
      </Panel>

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
          {breaches.length} over threshold · {rows.length} codes
        </div>
        <MockTag />
      </div>

      {rows.length === 0 ? (
        <EmptyNote>No variance to report for {periodLabel(periodId)}.</EmptyNote>
      ) : (
        <>
          <TableHead
            columns={[
              { label: "Code" },
              { label: "Planned", align: "right" },
              { label: "Actual", align: "right" },
              { label: "Variance", align: "right" },
            ]}
          />
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {rows.map((v) => {
              const code = budgetCodes.find((c) => c.code === v.code);
              const m = statusMeta(v.vk);

              return (
                <div
                  key={v.id}
                  onClick={() => code && onOpenCode(code.id)}
                  style={{
                    ...rowSurface(false, m.c),
                    padding: "11px 14px",
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                    flexWrap: "wrap",
                  }}
                >
                  <div style={{ flex: "1 1 240px", minWidth: 0 }}>
                    <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
                      <MonoTag>{v.code}</MonoTag>
                      <span
                        style={{
                          fontFamily: fonts.body,
                          fontWeight: 600,
                          fontSize: 12.5,
                          color: colors.textPrimary,
                        }}
                      >
                        {v.name}
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
                      {formatDeltaCad(v.delta)} against plan
                    </div>
                  </div>

                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num>{formatCad(v.planned)}</Num>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num>{formatCad(v.actual)}</Num>
                  </div>
                  <div
                    style={{
                      flex: "none",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "flex-end",
                    }}
                  >
                    <SignedStatus
                      kind={v.vk}
                      label={varianceLabel(v.deltaPct)}
                      value={formatDeltaPct(v.deltaPct)}
                    />
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
