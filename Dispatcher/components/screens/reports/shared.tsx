// Pieces shared by the two reports on the Reports screen — the tab bar that
// switches between them, and the pseudo-table styling they both build rows
// with. Sibling of screens/clients/shared.tsx.

import type { CSSProperties } from "react";
import { colors, fonts } from "@/lib/theme";

// ---------------------------------------------------------------------------
// Tabs
// ---------------------------------------------------------------------------

export const REPORT_TABS = [
  { id: "accruals", label: "Client Accruals" },
  { id: "terminus", label: "Terminus Summary" },
] as const;

export type ReportTabId = (typeof REPORT_TABS)[number]["id"];

/**
 * The screen-level underline tab bar, matching Grocery's (there is no shared
 * Tabs component in this app — this treatment is the idiom).
 *
 * Tabs are matched by NAME rather than by index, the same defence
 * fleet/vehicle-detail/VehicleDetail.tsx takes: inserting a third report must
 * not silently shift which one a tab renders, and the id also travels up to
 * Console as persisted state where a bare number would be unreadable.
 */
export function ReportTabs({
  tab,
  setTab,
}: {
  tab: ReportTabId;
  setTab: (t: ReportTabId) => void;
}) {
  return (
    <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginTop: 12 }}>
      {REPORT_TABS.map((t) => (
        <span
          key={t.id}
          onClick={() => setTab(t.id)}
          style={{
            fontFamily: fonts.body,
            fontWeight: tab === t.id ? 600 : 500,
            fontSize: 13,
            padding: "9px 16px",
            color: tab === t.id ? colors.headingBright : colors.textDim,
            borderBottom: tab === t.id ? `2px solid ${colors.blue}` : undefined,
            marginBottom: -1,
            cursor: "pointer",
          }}
        >
          {t.label}
        </span>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Pseudo-table styling
// ---------------------------------------------------------------------------

/** One cell in a report's pseudo-table: monospace, single line, ellipsised. */
export const cellStyle = {
  fontFamily: fonts.mono,
  fontSize: 11.5,
  color: colors.textSecondary,
  overflow: "hidden",
  textOverflow: "ellipsis",
  whiteSpace: "nowrap",
} as const satisfies CSSProperties;

/** The header row above those cells. Takes the grid template so each report
 *  can carry its own column set while the treatment stays identical. */
export function headerRowStyle(cols: string): CSSProperties {
  return {
    display: "grid",
    gridTemplateColumns: cols,
    gap: 11,
    padding: "7px 13px",
    fontFamily: fonts.semiCondensed,
    fontSize: 9.5,
    letterSpacing: ".12em",
    textTransform: "uppercase",
    color: colors.textFaint,
    borderBottom: `1px solid ${colors.borderSubtle}`,
  };
}
