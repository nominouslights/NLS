"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { formatCadPrecise } from "@/lib/money";
import { itemCount, userDisplay, type CostCentreRollup, type CostCentreRollupRow } from "@/lib/api/budgeting";
import { groupRollup } from "@/lib/costCentres";
import { EmptyNote, Num } from "@/components/screens/shared";

// "Where does the money land?" — the period's planned EXPENSE by cost centre, beside the
// by-priority breakdown. Unlike that panel this one is the server's own rollup
// (GET periods/{id}/rollups/cost-centres, CostCentrePlannedRollup), not a client-side sum: the
// attribution joins each code's cost-centre string to the register, which only the server holds
// in full.
//
// Planned only. The rollup carries no actual field until the actuals slice lands, so there is no
// actual column here either — a column of zeroes would read as "nothing spent".
//
// Rows nest one level under their parent when the parent is in the rollup (groupRollup); a row
// whose parent is not (a retired parent with nothing planned) stands alone and names it. "No
// cost centre" is always shown, even at $0, and the total is the server's totalPlannedExpenseCad,
// which equals the period's planned-expense tile. Retired or unregistered rows carry a chip
// (glyph + label) — never colour alone.

export default function CostCentreBreakdown({
  rollup,
  error,
}: {
  /** null while loading. */
  rollup: CostCentreRollup | null;
  error: string | null;
}) {
  return (
    <Panel>
      <SectionLabel>Expense by cost centre</SectionLabel>
      {error ? (
        <EmptyNote>{error}</EmptyNote>
      ) : rollup === null ? (
        <EmptyNote>Loading the cost-centre rollup…</EmptyNote>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {groupRollup(rollup.costCentres).map((group) => (
            <div key={group.row.costCentreId ?? `unregistered:${group.row.code}`} data-testid="cc-group">
              <RollupLine row={group.row} />
              {group.children.map((child) => (
                <RollupLine key={child.costCentreId ?? child.code} row={child} nested />
              ))}
              {group.children.length > 0 && (
                <div
                  data-testid="cc-subtotal"
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: 10,
                    padding: "2px 0 0 22px",
                    fontFamily: fonts.body,
                    fontSize: 11,
                    color: colors.textDim,
                  }}
                >
                  <span>
                    {group.row.code} with what rolls up into it
                  </span>
                  <span style={{ marginLeft: "auto" }}>
                    <Num size={12}>{formatCadPrecise(group.subtotalCad)}</Num>
                  </span>
                </div>
              )}
            </div>
          ))}

          <div data-testid="cc-none" style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>
              No cost centre
            </span>
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              {itemCount(rollup.noCostCentre.itemCount)}
            </span>
            <span style={{ marginLeft: "auto" }}>
              <Num size={13} weight={600} color={colors.textPrimary}>
                {formatCadPrecise(rollup.noCostCentre.plannedCad)}
              </Num>
            </span>
          </div>

          <div
            data-testid="cc-total"
            style={{
              display: "flex",
              alignItems: "center",
              gap: 10,
              borderTop: `1px solid ${colors.border}`,
              paddingTop: 8,
            }}
          >
            <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>
              Total planned expense
            </span>
            <span style={{ marginLeft: "auto" }}>
              <Num size={13} weight={700} color={colors.headingBright}>
                {formatCadPrecise(rollup.totalPlannedExpenseCad)}
              </Num>
            </span>
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
            Planned only — actual spend by cost centre arrives with actuals.
          </div>
        </div>
      )}
    </Panel>
  );
}

function RollupLine({ row, nested = false }: { row: CostCentreRollupRow; nested?: boolean }) {
  const owner = userDisplay(row.ownerName, row.ownerEmail, "");
  return (
    <div
      data-testid={nested ? "cc-child" : "cc-row"}
      style={{ display: "flex", alignItems: "center", gap: 10, padding: nested ? "4px 0 0 22px" : 0, flexWrap: "wrap" }}
    >
      {nested && <span style={{ color: colors.textDim, fontSize: 11 }}>↳</span>}
      <MonoTag>{row.code}</MonoTag>
      <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>
        {row.name}
      </span>
      {row.costCentreId === null ? (
        <StatusChip kind="off" label="Not in register" />
      ) : (
        !row.isActive && <StatusChip kind="off" label="Retired" />
      )}
      {!nested && row.parentCode && (
        <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>↳ {row.parentCode}</span>
      )}
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        {owner ? `${owner} · ` : ""}
        {itemCount(row.itemCount)}
      </span>
      <span style={{ marginLeft: "auto" }}>
        <Num size={13} weight={600} color={colors.textPrimary}>
          {formatCadPrecise(row.plannedCad)}
        </Num>
      </span>
    </div>
  );
}
