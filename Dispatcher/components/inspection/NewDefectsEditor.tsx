"use client";

import { colors, fonts } from "@/lib/theme";
import type { DefectSeverityWire } from "@/lib/api/maintenance";
import { ActionButton } from "@/components/ui/Button";
import { SelectField, TextField } from "@/components/ui/Field";
import { OptChip } from "@/components/manifest/manifestRows";
import type { NewDefect, NewDefectOption } from "./checklistRows";

// The post-trip's "New defects since the pre-trip" section (NL-PTI-01 rev 4). It
// replaces the old 21-row re-check and the single "Defects noticed while driving"
// row: the dispatcher answers No, or lists each new defect against the pre-trip row
// it concerns, with Minor/Major and a note. An explicit No/Yes rather than an empty
// list, so "no new defects" is a recorded answer, not an omission (Man. Reg. 95/2008
// s.12(1) — a report with nothing wrong says so).

const SEVERITY_OPTIONS: { value: DefectSeverityWire; label: string }[] = [
  { value: "Minor", label: "Minor" },
  { value: "Major", label: "Major" },
];

const BLANK: NewDefect = { itemKey: "", severity: "Minor", note: "" };

export default function NewDefectsEditor({
  found,
  onFoundChange,
  defects,
  onChange,
  options,
  preTripReported,
}: {
  /** The No/Yes answer. */
  found: boolean;
  onFoundChange: (found: boolean) => void;
  defects: NewDefect[];
  onChange: (defects: NewDefect[]) => void;
  /** Items a new defect may be filed against (see `newDefectOptions`). */
  options: NewDefectOption[];
  /** How many pre-trip defects were left out of `options` — shown so the
   *  dispatcher knows why those items are missing. */
  preTripReported: number;
}) {
  function patch(i: number, p: Partial<NewDefect>) {
    onChange(defects.map((d, x) => (x === i ? { ...d, ...p } : d)));
  }

  function answer(yes: boolean) {
    onFoundChange(yes);
    if (yes && defects.length === 0) onChange([BLANK]);
  }

  return (
    <div style={{ marginBottom: 16 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8 }}>
        <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>
          Any defect found after the pre-trip?
        </span>
        <OptChip active={!found} label="No" onClick={() => answer(false)} />
        <OptChip active={found} label="Yes" onClick={() => answer(true)} />
      </div>
      {preTripReported > 0 && (
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: 8 }}>
          {preTripReported} {preTripReported === 1 ? "item" : "items"} already reported on this trip&apos;s
          pre-trip {preTripReported === 1 ? "is" : "are"} not listed — those are not new.
        </div>
      )}

      {found && (
        <>
          {defects.map((d, i) => {
            // Each item once per inspection — the backend rejects a second defect on
            // the same item (DuplicateDefectItem).
            const taken = new Set(defects.filter((_, x) => x !== i).map((o) => o.itemKey));
            const itemOptions = options.filter((o) => !taken.has(o.key));
            return (
              <div
                key={i}
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 110px 1fr auto",
                  gap: 10,
                  alignItems: "end",
                  marginBottom: 8,
                }}
              >
                <SelectField
                  label={`Defect ${i + 1} — item`}
                  value={d.itemKey}
                  onChange={(v) => patch(i, { itemKey: v })}
                  options={[
                    ...(d.itemKey ? [] : [{ value: "", label: "— select the item —" }]),
                    ...itemOptions.map((o) => ({ value: o.key, label: o.label })),
                  ]}
                />
                <SelectField
                  label="Severity"
                  value={d.severity}
                  onChange={(v) => patch(i, { severity: v as DefectSeverityWire })}
                  options={SEVERITY_OPTIONS}
                />
                <TextField
                  label="Note (required)"
                  value={d.note}
                  onChange={(v) => patch(i, { note: v })}
                  placeholder="What was found, and where"
                />
                <div style={{ paddingBottom: 6 }}>
                  <span
                    onClick={() => onChange(defects.filter((_, x) => x !== i))}
                    style={{
                      width: 30,
                      height: 30,
                      borderRadius: 7,
                      border: `1px solid ${colors.borderStrong}`,
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      color: colors.textMuted,
                      cursor: "pointer",
                      fontSize: 14,
                    }}
                  >
                    ✕
                  </span>
                </div>
              </div>
            );
          })}
          <ActionButton onClick={() => onChange([...defects, BLANK])}>+ ADD DEFECT</ActionButton>
        </>
      )}
    </div>
  );
}
