"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import type { DefectRepairOutcomeWire, WorkOrderDefectLineWire } from "@/lib/api/maintenance";
import {
  DEFECT_OUTCOME_HELP,
  DEFECT_OUTCOME_LABEL,
  DEFECT_OUTCOME_META,
  DEFECT_SEVERITY_LABEL,
  DEFECT_SEVERITY_META,
} from "@/lib/workOrderDisplay";
import { defectKeyOf, outcomesFor, type DefectOutcomeDraft } from "@/lib/workOrderCompletion";
import { SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { SelectField, TextField } from "@/components/ui/Field";

// "Defect outcomes" on the close-work-order form: one row per defect line, each
// needing an outcome the dispatcher picks — there is NO default, so a defect is
// never cleared by someone who did not look at it. Deferred is not offered for
// an out-of-service defect, and needs a note.

export default function DefectOutcomesSection({
  lines,
  drafts,
  onChange,
  issues,
  disabled = false,
}: {
  lines: WorkOrderDefectLineWire[];
  drafts: DefectOutcomeDraft[];
  onChange: (next: DefectOutcomeDraft[]) => void;
  /** Per-row messages from validateDefectOutcomes, keyed by defectKeyOf. */
  issues: Map<string, string>;
  disabled?: boolean;
}) {
  function update(key: string, patch: Partial<DefectOutcomeDraft>) {
    onChange(drafts.map((d) => (defectKeyOf(d) === key ? { ...d, ...patch } : d)));
  }

  return (
    <div style={{ marginBottom: 18 }}>
      <SectionLabel>Defect outcomes · {lines.length}</SectionLabel>
      <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.5, marginBottom: 10 }}>
        Record what the mechanic found for each defect. <b>Repaired</b> and <b>No fault found</b> clear the
        defect permanently. <b>Deferred</b> leaves it open so a later work order can take it.
      </div>
      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
        {lines.map((line) => {
          const key = defectKeyOf(line);
          const draft = drafts.find((d) => defectKeyOf(d) === key);
          const sev = DEFECT_SEVERITY_META[line.severity] ?? DEFECT_SEVERITY_META.Major;
          const chosen = draft?.outcome ?? null;
          const out = chosen ? DEFECT_OUTCOME_META[chosen] : null;
          const issue = issues.get(key);
          return (
            <div
              key={key}
              data-testid={`defect-outcome-${line.item}`}
              style={{
                padding: "10px 12px",
                borderRadius: 9,
                border: `1px solid ${issue ? statusMeta("over").bd : colors.borderSubtle}`,
                background: colors.cardBg,
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", marginBottom: 8 }}>
                <StatusChip kind={sev.kind} glyph={sev.glyph} label={DEFECT_SEVERITY_LABEL[line.severity]} />
                <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.headingBright }}>
                  {line.item}
                </span>
                {chosen && out && (
                  <span style={{ marginLeft: "auto" }}>
                    <StatusChip kind={out.kind} glyph={out.glyph} label={DEFECT_OUTCOME_LABEL[chosen]} />
                  </span>
                )}
              </div>
              {line.note && (
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: 8 }}>
                  {line.note}
                </div>
              )}
              <div style={{ display: "grid", gridTemplateColumns: "200px 1fr", gap: 12, alignItems: "start" }}>
                <SelectField
                  label="Outcome"
                  value={chosen ?? ""}
                  disabled={disabled}
                  onChange={(v) => update(key, { outcome: (v || null) as DefectRepairOutcomeWire | null })}
                  options={[
                    { value: "", label: "— choose —" },
                    ...outcomesFor(line.severity).map((o) => ({ value: o, label: DEFECT_OUTCOME_LABEL[o] })),
                  ]}
                  hint={<span style={{ color: colors.textFaint }}>· required</span>}
                />
                <TextField
                  label={chosen === "Deferred" ? "Why deferred" : "Note (optional)"}
                  value={draft?.note ?? ""}
                  disabled={disabled}
                  onChange={(v) => update(key, { note: v })}
                  placeholder={chosen === "Deferred" ? "Parts on order — book for next shop visit" : "What was found"}
                  hint={chosen === "Deferred" ? <span style={{ color: colors.textFaint }}>· required</span> : undefined}
                />
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 6 }}>
                {chosen
                  ? DEFECT_OUTCOME_HELP[chosen]
                  : line.severity === "OutOfService"
                    ? "Out-of-service defects cannot be deferred."
                    : " "}
              </div>
              {issue && (
                <div style={{ fontFamily: fonts.body, fontSize: 12, fontWeight: 600, color: statusMeta("over").t, marginTop: 4 }}>
                  {statusMeta("over").g} {issue}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
