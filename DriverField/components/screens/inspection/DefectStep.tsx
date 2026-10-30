"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, radius, type, wizard } from "@/lib/tablet";
import { AnswerButton } from "@/components/ui-tablet/AnswerButton";
import { severityGlyph, severityKind } from "@/lib/inspectionGate";
import type { InspectionArea, ItemCategory } from "@/lib/inspectionForm";
import type { DefectSeverity } from "@/lib/types";
import type { DraftDefect } from "@/lib/inspectionStore";

// APP-LOCAL. The follow-up injected right after a check answered "Defect": how bad, and what.
//
// MINOR AND MAJOR ONLY. NL-PTI-01 classifies every row `Minor` or `Major` and offers no third
// box — under NSC 13 a Major IS the out-of-service condition, so a separate "Out of Service"
// answer was a grade the form does not have. `"Out of Service"` stays in `DefectSeverity` and in
// `severityToWire` because InspectionDefectSeverity still carries `OutOfService` and historical
// rows are graded with it; it is simply never OFFERED here. Do not re-add the third tile to
// "match the enum" — the enum is the wire's vocabulary, this is the form's.
//
// SEVERITY COLOURS. Minor is gold, Major vermillion — two kinds, two glyphs, two labels, so the
// pair stays distinguishable in grayscale without any override. The `glyph` prop is still passed
// through severityGlyph() rather than dropped: it is the single place that decides a severity's
// icon, and hardcoding the default here would be the drift it exists to prevent.
//
// The note stays OPTIONAL: DefectInput.Note is `string?`. The severity does not — a defect
// without one cannot be graded, and DeriveResult reads nothing else. This note describes the
// FAULT; the row's own Notes-column remark lives on the check step, and a defect can carry both.

const AREA_LABEL: Record<InspectionArea, string> = {
  A: "Area A",
  B: "Area B",
  C: "Area C",
};

const SEVERITIES: { value: DefectSeverity; sublabel: string }[] = [
  { value: "Minor", sublabel: "Safe to operate; log it" },
  { value: "Major", sublabel: "Fails the inspection — do not operate" },
];

export function DefectStep({
  area,
  group,
  label,
  category,
  categoryNote,
  defect,
  onChange,
}: {
  area: InspectionArea;
  group: string;
  label: string;
  /** The form's default classification for this row. DISPLAY TEXT — nothing computes it. */
  category: ItemCategory;
  /** The form's own qualifier, verbatim ("Major if leaking"), where the row has one. */
  categoryNote?: string;
  defect: DraftDefect;
  onChange: (next: DraftDefect) => void;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: gap.row, minHeight: 0 }}>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 16,
          letterSpacing: ".16em",
          textTransform: "uppercase",
          color: colors.textLabel,
        }}
      >
        {AREA_LABEL[area]} · {group} · defect
      </div>

      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: wizard.question,
          lineHeight: 1.1,
          color: colors.headingBright,
        }}
      >
        How bad is the {label.toLowerCase()} defect?
      </div>

      {/* The form's own guidance for this row, reproduced verbatim. The DRIVER grades the
          defect — nothing here evaluates a season, a date or a route, and this line is not a
          default selection. */}
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: type.value,
          lineHeight: 1.45,
          color: colors.textSecondary,
        }}
      >
        The form classifies this row as <strong>{category}</strong>
        {categoryNote ? ` — “${categoryNote}”` : ""}. You decide.
      </div>

      <div style={{ display: "flex", gap: gap.row }} role="group" aria-label="Defect severity">
        {SEVERITIES.map((s) => (
          <AnswerButton
            key={s.value}
            kind={severityKind(s.value)}
            glyph={severityGlyph(s.value)}
            label={s.value}
            sublabel={s.sublabel}
            selected={defect.severity === s.value}
            onClick={() => onChange({ ...defect, severity: s.value })}
          />
        ))}
      </div>

      <textarea
        value={defect.note}
        onChange={(e) => onChange({ ...defect, note: e.target.value })}
        placeholder="What exactly is wrong? (optional)"
        rows={2}
        aria-label={`Note about the ${label} defect`}
        style={{
          width: "100%",
          minHeight: 84,
          padding: "14px 16px",
          borderRadius: radius.control,
          border: `1px solid ${colors.borderStrong}`,
          background: colors.inputBg,
          color: colors.textPrimary,
          fontFamily: fonts.body,
          fontSize: type.label,
          resize: "none",
        }}
      />
    </div>
  );
}
