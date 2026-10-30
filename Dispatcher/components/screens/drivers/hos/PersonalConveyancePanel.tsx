"use client";

import { colors, fonts } from "@/lib/theme";
import type { DutyDaySummary } from "@/lib/api/hos";
import { formatDateOnly, PC_CAUTION_KM, PC_DAILY_LIMIT_KM, pcDayVerdict } from "@/lib/hos/display";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";

// Personal conveyance per day over the ledger window (GET …/hos/days → personalConveyanceKm):
// each day's distance with the verdict chip — ✓ under 60 km, ◐ with the km left up to 74.9,
// ▲ at 75 and beyond (the engine reclassifies the remainder on-duty). Only days with records
// are listed; a window with no PC at all says so rather than showing fourteen zeros.

export function PersonalConveyancePanel({ days }: { days: DutyDaySummary[] | null }) {
  const rows = days === null ? null : [...days].filter((d) => d.hasRecords && d.personalConveyanceKm > 0).sort((a, b) => b.date.localeCompare(a.date));
  const worst = rows?.reduce((max, d) => Math.max(max, d.personalConveyanceKm), 0) ?? 0;

  return (
    <Panel style={{ marginBottom: 12 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
        <SectionLabel>Personal conveyance · last 14 days</SectionLabel>
        <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          {PC_DAILY_LIMIT_KM} km per day (s.1) · caution from {PC_CAUTION_KM} km
        </span>
      </div>
      {rows === null ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading…</div>
      ) : rows.length === 0 ? (
        <StatusChip kind="ontime" label="No personal conveyance recorded in the last 14 days" />
      ) : (
        <div>
          {worst >= PC_DAILY_LIMIT_KM && (
            <div style={{ marginBottom: 8 }}>
              <StatusChip kind="over" glyph="▲" label={`${PC_DAILY_LIMIT_KM} km reached on at least one day — the remainder was counted as on-duty`} />
            </div>
          )}
          {rows.map((d) => {
            const verdict = pcDayVerdict(d.personalConveyanceKm);
            return (
              <div key={d.date} data-testid="pc-day-row" style={{ display: "flex", alignItems: "center", gap: 12, borderTop: `1px solid ${colors.borderSubtle}`, padding: "8px 0" }}>
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary, minWidth: 92 }}>{formatDateOnly(d.date)}</span>
                <StatusChip kind={verdict.kind} glyph={verdict.glyph} label={verdict.label} />
              </div>
            );
          })}
        </div>
      )}
    </Panel>
  );
}
