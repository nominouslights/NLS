"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { DefectSeverityWire } from "@/lib/api/maintenance";
import type { PreTripGroup } from "@/lib/tripManifestChecklist";
import { SelectField, TextField } from "@/components/ui/Field";

// One pre-trip inspection group. Each item is an OK/FAIL toggle (default OK);
// FAIL expands a severity select and a required note. Rows carry the backend
// wire keys (group + item); labels are display-only. Salvaged from the retired
// Manual Trip Entry form and now feeds the trip-context Fleet inspection modal.

export interface ChecklistRow {
  /** Backend wire group string (PreTripGroup.key). */
  groupKey: string;
  /** Backend wire item string (PreTripItem.key). */
  itemKey: string;
  /** Long display label. */
  label: string;
  fail: boolean;
  severity: DefectSeverityWire;
  note: string;
}

const SEVERITY_OPTIONS: { value: DefectSeverityWire; label: string }[] = [
  { value: "Minor", label: "Minor" },
  { value: "Major", label: "Major" },
  { value: "OutOfService", label: "Out of service" },
];

export function groupResult(rows: ChecklistRow[]): "Pass" | "Pass with defects" | "Fail" {
  const fails = rows.filter((r) => r.fail);
  if (fails.some((f) => f.severity === "Major" || f.severity === "OutOfService")) return "Fail";
  if (fails.length > 0) return "Pass with defects";
  return "Pass";
}

export default function ChecklistGroupEditor({
  group,
  rows,
  onPatch,
}: {
  group: PreTripGroup;
  rows: ChecklistRow[];
  onPatch: (itemKey: string, patch: Partial<ChecklistRow>) => void;
}) {
  const result = groupResult(rows);
  const rm = statusMeta(result === "Pass" ? "ontime" : result === "Fail" ? "over" : "soon");

  return (
    <div style={{ marginBottom: 14 }}>
      <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginBottom: 8 }}>
        <span
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 10.5,
            letterSpacing: ".12em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          {group.title}
        </span>
        <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          Result:{" "}
          <span style={{ color: rm.t, fontWeight: 700 }}>
            {rm.g} {result}
          </span>
        </span>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {rows.map((r) => (
          <div
            key={r.itemKey}
            style={{ padding: "9px 11px", ...rowSurface(r.fail, r.fail ? statusMeta("over").c : colors.blue), cursor: "default" }}
          >
            <div style={{ display: "grid", gridTemplateColumns: "1fr 140px", gap: 10, alignItems: "center" }}>
              <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                {r.label}
              </span>
              <div style={{ display: "flex", gap: 6, justifyContent: "flex-end" }}>
                <StatusToggle active={!r.fail} label="OK" kind="ontime" onClick={() => onPatch(r.itemKey, { fail: false })} />
                <StatusToggle active={r.fail} label="FAIL" kind="over" onClick={() => onPatch(r.itemKey, { fail: true })} />
              </div>
            </div>
            {r.fail && (
              <div style={{ display: "grid", gridTemplateColumns: "180px 1fr", gap: 10, marginTop: 9 }}>
                <SelectField
                  label="Severity"
                  value={r.severity}
                  onChange={(v) => onPatch(r.itemKey, { severity: v as DefectSeverityWire })}
                  options={SEVERITY_OPTIONS}
                />
                <TextField
                  label="Note (required for FAIL)"
                  value={r.note}
                  onChange={(v) => onPatch(r.itemKey, { note: v })}
                  placeholder="Describe the failure"
                />
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

function StatusToggle({
  active,
  label,
  kind,
  onClick,
}: {
  active: boolean;
  label: string;
  kind: "ontime" | "over";
  onClick: () => void;
}) {
  const m = statusMeta(kind);
  return (
    <span
      onClick={onClick}
      style={{
        fontFamily: fonts.body,
        fontWeight: 600,
        fontSize: 11.5,
        padding: "4px 11px",
        borderRadius: 7,
        cursor: "pointer",
        border: `1px solid ${active ? m.bd : colors.borderSubtle}`,
        background: active ? m.bg : "transparent",
        color: active ? m.t : colors.textDim,
        whiteSpace: "nowrap",
      }}
    >
      {active ? `${m.g} ` : ""}
      {label}
    </span>
  );
}
