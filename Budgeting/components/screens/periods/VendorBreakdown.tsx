"use client";

import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { formatCadPrecise } from "@/lib/money";
import { itemCount, type VendorRollup, type VendorRollupRow } from "@/lib/api/budgeting";
import { PENDING_VENDOR_NAME } from "@/lib/vendorShares";
import { EmptyNote, Num } from "@/components/screens/shared";

// "Who gets paid?" — the period's planned EXPENSE by vendor, beside the by-priority and
// by-cost-centre panels. The server's own rollup (GET periods/{id}/rollups/vendors,
// VendorPlannedRollup), not a client-side sum: a vendor's figure is the sum of its dollar shares
// across the period's expense items, and an item split across three vendors counts once for each.
//
// Only vendors with a share this period are listed — the register grows with every supplier ever
// paid, and a $0 row per idle one would bury the ones that matter. Retired vendors still carrying
// shares are listed with a chip (glyph + written label, never colour alone); a vendor whose read
// row is a poll behind is named as loading rather than dropped. "No vendor" is always shown, even
// at $0, and the total is the server's totalPlannedExpenseCad — the rows plus "No vendor" add up
// to it, and it equals the period's planned-expense tile.
//
// Planned only: there is no actual field until the actuals slice lands, so no actual column.

export default function VendorBreakdown({
  rollup,
  error,
}: {
  /** null while loading. */
  rollup: VendorRollup | null;
  error: string | null;
}) {
  return (
    <Panel>
      <SectionLabel>Expense by vendor</SectionLabel>
      {error ? (
        <EmptyNote>{error}</EmptyNote>
      ) : rollup === null ? (
        <EmptyNote>Loading the vendor rollup…</EmptyNote>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
          {rollup.vendors.map((row) => (
            <VendorLine key={row.vendorId} row={row} />
          ))}

          <div data-testid="vendor-none" style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>
              No vendor
            </span>
            <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              {itemCount(rollup.noVendor.itemCount)}
            </span>
            <span style={{ marginLeft: "auto" }}>
              <Num size={13} weight={600} color={colors.textPrimary}>
                {formatCadPrecise(rollup.noVendor.plannedCad)}
              </Num>
            </span>
          </div>

          <div
            data-testid="vendor-total"
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
            Planned only — a split item counts toward each of its vendors. Actual spend by vendor
            arrives with actuals.
          </div>
        </div>
      )}
    </Panel>
  );
}

function VendorLine({ row }: { row: VendorRollupRow }) {
  const pending = row.name === null;
  return (
    <div data-testid="vendor-row" style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
      <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>
        {row.name ?? PENDING_VENDOR_NAME}
      </span>
      {/* isActive is false for an unresolved row too — that is lag, not retirement. */}
      {!pending && !row.isActive && <StatusChip kind="off" label="Retired" />}
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
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
