"use client";

import { colors, fonts } from "@/lib/theme";
import type { ChecklistItemStateWire, VehicleInspection } from "@/lib/api/maintenance";
import { DEFECT_SEVERITY_LABEL, DEFECT_SEVERITY_META } from "@/lib/workOrderDisplay";
import { StatusChip } from "@/components/ui/Chip";

// Read-only view of an inspection recorded against a form revision NL-PTI-01
// replaced. It renders THAT RECORD'S OWN items — never the current catalogue —
// because the two share no item strings and any mapping between them would be a
// guess. Nothing here is editable and nothing is dropped: what the driver
// answered on the day is what shows.

const STATE_CHIP: Record<ChecklistItemStateWire, { kind: "ontime" | "over" | "off"; label: string }> = {
  Ok: { kind: "ontime", label: "OK" },
  Defect: { kind: "over", label: "Defect" },
  NotApplicable: { kind: "off", label: "N/A" },
};

export default function RetiredFormChecklist({ inspection }: { inspection: VehicleInspection }) {
  const defectByItem = new Map(inspection.defects.map((d) => [d.item, d]));

  // Group in the order the record carries, with un-grouped rows under one heading.
  const order: string[] = [];
  const byGroup = new Map<string, typeof inspection.checklist>();
  for (const row of inspection.checklist) {
    const key = row.group ?? "";
    if (!byGroup.has(key)) {
      byGroup.set(key, []);
      order.push(key);
    }
    byGroup.get(key)!.push(row);
  }

  return (
    <div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: 10 }}>
        {inspection.checklist.length} recorded {inspection.checklist.length === 1 ? "check" : "checks"}, as answered on
        the day.
      </div>

      {order.map((groupKey) => (
        <div key={groupKey} style={{ marginBottom: 14 }}>
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 10.5,
              letterSpacing: ".12em",
              textTransform: "uppercase",
              color: colors.textLabel,
              marginBottom: 8,
            }}
          >
            {groupKey || "Checklist"}
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {byGroup.get(groupKey)!.map((row) => {
              const state: ChecklistItemStateWire = row.state ?? (row.passed ? "Ok" : "Defect");
              const chip = STATE_CHIP[state];
              const defect = defectByItem.get(row.item);
              const note = row.note ?? defect?.note ?? null;
              return (
                <div
                  key={row.item}
                  style={{
                    padding: "8px 11px",
                    borderRadius: 9,
                    border: `1px solid ${colors.borderSubtle}`,
                    background: colors.cardBg,
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                      {row.item}
                    </span>
                    <span style={{ marginLeft: "auto", display: "flex", gap: 6, flexWrap: "wrap" }}>
                      <StatusChip kind={chip.kind} label={chip.label} />
                      {defect && (
                        <StatusChip
                          kind={DEFECT_SEVERITY_META[defect.severity].kind}
                          glyph={DEFECT_SEVERITY_META[defect.severity].glyph}
                          label={DEFECT_SEVERITY_LABEL[defect.severity]}
                        />
                      )}
                    </span>
                  </div>
                  {note && (
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 11.5,
                        color: colors.textDim,
                        lineHeight: 1.45,
                        marginTop: 3,
                      }}
                    >
                      {note}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
