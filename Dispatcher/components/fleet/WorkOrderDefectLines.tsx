"use client";

import { colors, fonts } from "@/lib/theme";
import type { DefectRepairOutcomeWire, DefectSeverityWire } from "@/lib/api/maintenance";
import {
  DEFECT_OUTCOME_LABEL,
  DEFECT_OUTCOME_META,
  DEFECT_SEVERITY_LABEL,
  DEFECT_SEVERITY_META,
} from "@/lib/workOrderDisplay";
import { StatusChip } from "@/components/ui/Chip";

// Read-only list of the defects a work order is raised against: severity chip,
// item, the driver's note and — once the work order is completed — the
// mechanic's outcome chip and note. Used by the New Work Order modal (what is
// about to be attached) and by each work-order card (what is attached).

export interface DefectLineView {
  inspectionId: string;
  item: string;
  severity: DefectSeverityWire;
  note: string | null;
  outcome?: DefectRepairOutcomeWire | null;
  outcomeNote?: string | null;
}

export default function WorkOrderDefectLines({ lines }: { lines: DefectLineView[] }) {
  if (lines.length === 0) return null;
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      {lines.map((d) => {
        const sev = DEFECT_SEVERITY_META[d.severity] ?? DEFECT_SEVERITY_META.Major;
        const out = d.outcome ? DEFECT_OUTCOME_META[d.outcome] : null;
        return (
          <div
            key={`${d.inspectionId}:${d.item}`}
            style={{
              padding: "7px 10px",
              borderRadius: 8,
              border: `1px solid ${colors.borderSubtle}`,
              background: colors.cardBg,
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
              <StatusChip kind={sev.kind} glyph={sev.glyph} label={DEFECT_SEVERITY_LABEL[d.severity] ?? d.severity} />
              <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 700, color: colors.headingBright }}>
                {d.item}
              </span>
              {d.outcome && out && (
                <span style={{ marginLeft: "auto" }}>
                  <StatusChip kind={out.kind} glyph={out.glyph} label={DEFECT_OUTCOME_LABEL[d.outcome] ?? d.outcome} />
                </span>
              )}
            </div>
            {d.note && <div style={noteStyle}>{d.note}</div>}
            {d.outcomeNote && (
              <div style={{ ...noteStyle, color: colors.textSecondary }}>
                <span style={{ fontWeight: 600 }}>Mechanic:</span> {d.outcomeNote}
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

const noteStyle = {
  fontFamily: fonts.body,
  fontSize: 11.5,
  color: colors.textDim,
  marginTop: 4,
  lineHeight: 1.45,
} as const;
