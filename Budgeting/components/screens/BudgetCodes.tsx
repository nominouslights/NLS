"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { DetailRow, Panel, SectionLabel } from "@/components/ui/Panel";
import { budgetCodes } from "@/lib/data";
import { EmptyNote, MockTag, Screen } from "@/components/screens/shared";

// Master/detail, following Dispatcher's Clients and Trips screens: a left column of rows and a
// right detail pane on the tinted detailBg, split by a CSS grid with a top border.
//
// The ZBB point this screen exists to carry: a code's justification is re-confirmed every
// period, so it is the detail pane's headline, not a footnote.

export default function BudgetCodes({
  selId,
  onSelect,
}: {
  selId: string | null;
  onSelect: (id: string | null) => void;
}) {
  const selected = budgetCodes.find((c) => c.id === selId) ?? budgetCodes[0] ?? null;

  return (
    <Screen eyebrow="Planning" title="Budget Codes" right={<MockTag />}>
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "38% 1fr",
          borderTop: `1px solid ${colors.border}`,
          height: "100%",
          minHeight: 0,
        }}
      >
        <div
          style={{
            overflowY: "auto",
            padding: "16px 18px 16px 0",
            borderRight: `1px solid ${colors.border}`,
            display: "flex",
            flexDirection: "column",
            gap: 8,
          }}
        >
          {budgetCodes.map((c) => {
            const active = selected?.id === c.id;
            return (
              <div
                key={c.id}
                onClick={() => onSelect(c.id)}
                style={{ ...rowSurface(active), padding: "11px 13px" }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 4 }}>
                  <MonoTag>{c.code}</MonoTag>
                  <StatusChip
                    kind={c.category === "Revenue" ? "ontime" : "info"}
                    label={c.category}
                  />
                  {!c.active && <StatusChip kind="off" label="Inactive" />}
                </div>
                <div
                  style={{
                    fontFamily: fonts.body,
                    fontWeight: 600,
                    fontSize: 12.5,
                    color: colors.textPrimary,
                  }}
                >
                  {c.name}
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ overflowY: "auto", padding: "22px 0 22px 26px", background: colors.detailBg }}>
          {selected ? (
            <>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 14 }}>
                <MonoTag>{selected.code}</MonoTag>
                <div
                  style={{
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 22,
                    color: colors.headingBright,
                  }}
                >
                  {selected.name}
                </div>
              </div>

              <Panel style={{ marginBottom: 12 }}>
                <SectionLabel>Zero-based justification</SectionLabel>
                <div
                  style={{
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    color: colors.textSecondary,
                    lineHeight: 1.65,
                  }}
                >
                  {selected.justification}
                </div>
              </Panel>

              <Panel>
                <SectionLabel>Details</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="Category" value={selected.category} />
                  <DetailRow label="Owner" value={selected.owner} />
                  <DetailRow
                    label="Status"
                    value={
                      <StatusChip
                        kind={selected.active ? "ontime" : "off"}
                        label={selected.active ? "Active" : "Inactive"}
                      />
                    }
                  />
                </div>
              </Panel>
            </>
          ) : (
            <EmptyNote>No budget codes defined for this tenant yet.</EmptyNote>
          )}
        </div>
      </div>
    </Screen>
  );
}
