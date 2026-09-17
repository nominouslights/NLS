"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, radius, type, wizard } from "@/lib/tablet";
import { AnswerButton } from "@/components/ui-tablet/AnswerButton";
import { severityGlyph, severityKind } from "@/lib/inspectionGate";
import type { DefectSeverity } from "@/lib/types";
import type { DraftDefect } from "@/lib/inspectionStore";

// APP-LOCAL. The follow-up injected right after a check answered "Defect": how bad, and what.
//
// SEVERITY COLOURS. Minor is gold; Major and Out of Service are BOTH vermillion, deliberately —
// lib/theme.ts is a protected copy, so a fifth StatusKind or a new hex is not an option. They
// are told apart by their GLYPH instead: Out of Service supplies ✕ through AnswerButton's
// `glyph` override (the same prop the copied Chip.tsx's StatusChip carries, for the same
// reason). Colour + glyph + label, all three, and the three tiles stay distinguishable in
// grayscale. See severityGlyph() in lib/inspectionGate.ts.
//
// The note stays OPTIONAL: DefectInput.Note is `string?`. The severity does not — a defect
// without one cannot be graded, and DeriveResult reads nothing else.

const SEVERITIES: { value: DefectSeverity; sublabel: string }[] = [
  { value: "Minor", sublabel: "Safe to operate; log it" },
  { value: "Major", sublabel: "Fails the inspection" },
  { value: "Out of Service", sublabel: "Do not operate the vehicle" },
];

export function DefectStep({
  group,
  label,
  defect,
  onChange,
}: {
  group: string;
  label: string;
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
        {group} · defect
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
