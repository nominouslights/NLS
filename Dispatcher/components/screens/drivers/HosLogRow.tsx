"use client";

import { chipStyle, colors, dutyMeta, fonts, statusMeta } from "@/lib/theme";
import type { HosEntryRecord } from "@/lib/api/drivers";
import type { DutyStatus } from "@/lib/types";

// One duty-log row on the Hours of Service tab (+ its source chip) — extracted
// verbatim from Drivers.tsx.

export function SourceChip({ entry }: { entry: HosEntryRecord }) {
  const manual = entry.source === "Manual (paper backup)";
  const style = manual
    ? chipStyle("rgba(232,160,32,.13)", "rgba(232,160,32,.5)", colors.amberText)
    : chipStyle(statusMeta("info").bg, statusMeta("info").bd, statusMeta("info").t);
  return (
    <span style={{ ...style, fontSize: 10.5, padding: "2px 8px" }}>
      <span style={{ fontSize: 9, lineHeight: 1 }}>{manual ? "✎" : "◈"}</span>
      {manual ? "Paper backup" : "Driver App"}
    </span>
  );
}

export function HosLogRow({ entry }: { entry: HosEntryRecord }) {
  const m = dutyMeta(entry.duty as DutyStatus);
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "78px 92px 1fr auto",
        gap: 10,
        alignItems: "center",
        padding: "10px 0",
        borderTop: `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>{entry.date}</div>
      <div style={{ display: "flex", alignItems: "center", gap: 5, fontFamily: fonts.body, fontSize: 12, fontWeight: 600, color: m.text }}>
        <span style={{ fontSize: 9, color: m.color }}>{m.glyph}</span>
        {entry.duty}
      </div>
      <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>
        On {entry.onDutyH}h · Drv {entry.drivingH}h · Off {entry.offDutyH}h
      </div>
      <div style={{ textAlign: "right" }}>
        <SourceChip entry={entry} />
        {entry.enteredBy && (
          <div style={{ fontFamily: fonts.body, fontSize: 10, color: colors.textDim, marginTop: 2 }}>{entry.enteredBy}</div>
        )}
      </div>
    </div>
  );
}
