// COPIED FROM Dispatcher/components/ui/MonthGrid.tsx — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
"use client";

import type { CSSProperties, ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";

// Generic month-grid layout primitive — no date library in this app, so this
// is plain Date math (following the rest of the codebase's minimal-dependency
// convention). Styled from lib/theme.ts tokens to match Panel.tsx. Caller owns
// all day-cell content via renderDay; this component only handles the grid.

const MONTH_NAMES = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];
const WEEKDAY_LABELS = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function pad(n: number): string {
  return String(n).padStart(2, "0");
}

function isoOf(year: number, month: number, day: number): string {
  const d = new Date(year, month, day);
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

export function MonthGrid({
  year,
  month, // 0-based, matches Date's getMonth()
  onPrevMonth,
  onNextMonth,
  renderDay,
}: {
  year: number;
  month: number;
  onPrevMonth: () => void;
  onNextMonth: () => void;
  renderDay: (dateIso: string, inMonth: boolean) => ReactNode;
}) {
  const firstWeekday = new Date(year, month, 1).getDay(); // 0=Sun..6=Sat
  const leading = (firstWeekday + 6) % 7; // Mon-first offset
  const daysInMonth = new Date(year, month + 1, 0).getDate();
  const trailing = (7 - ((leading + daysInMonth) % 7)) % 7;

  const cells: { dateIso: string; inMonth: boolean }[] = [];
  for (let i = leading; i > 0; i--) {
    cells.push({ dateIso: isoOf(year, month, 1 - i), inMonth: false });
  }
  for (let d = 1; d <= daysInMonth; d++) {
    cells.push({ dateIso: isoOf(year, month, d), inMonth: true });
  }
  for (let d = 1; d <= trailing; d++) {
    cells.push({ dateIso: isoOf(year, month + 1, d), inMonth: false });
  }

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 12 }}>
        <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, color: colors.headingBright }}>
          {MONTH_NAMES[month]} {year}
        </div>
        <div style={{ display: "flex", gap: 6 }}>
          <span onClick={onPrevMonth} style={navBtnStyle}>‹</span>
          <span onClick={onNextMonth} style={navBtnStyle}>›</span>
        </div>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(7,1fr)", gap: 6, marginBottom: 6 }}>
        {WEEKDAY_LABELS.map((w) => (
          <div
            key={w}
            style={{
              textAlign: "center",
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".1em",
              textTransform: "uppercase",
              color: colors.textFaint,
            }}
          >
            {w}
          </div>
        ))}
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(7,1fr)", gap: 6 }}>
        {cells.map((c) => (
          <div key={c.dateIso}>{renderDay(c.dateIso, c.inMonth)}</div>
        ))}
      </div>
    </div>
  );
}

const navBtnStyle: CSSProperties = {
  width: 26,
  height: 26,
  borderRadius: 7,
  border: `1px solid ${colors.borderStrong}`,
  background: colors.cardBg,
  color: colors.textMuted,
  cursor: "pointer",
  fontFamily: fonts.body,
  fontSize: 14,
  display: "flex",
  alignItems: "center",
  justifyContent: "center",
  userSelect: "none",
};
