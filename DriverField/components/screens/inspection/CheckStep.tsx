"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, radius, type, wizard } from "@/lib/tablet";
import { AnswerButton } from "@/components/ui-tablet/AnswerButton";
import type { InspectionArea } from "@/lib/inspectionForm";
import type { CheckState } from "@/lib/types";

// APP-LOCAL. One NL-PTI-01 row, alone on the screen: where the driver is, the row itself, what
// the form says to check for, three answer tiles and the row's Notes column.
//
// WHY THERE IS MORE ON THIS SCREEN THAN THERE USED TO BE. The old 22-item list was all
// self-explanatory labels ("Engine oil level"). NL-PTI-01 is 67-80 rows of the real form, and
// "Ground beneath the vehicle" means nothing without its Check For column ("No fresh oil,
// coolant, fuel, brake fluid or transmission fluid on the ground"). The area and sub-group line
// is the other half of that: a walk-around that long needs the driver to be able to see where
// they are standing, not just which question they are on. Both are the mitigation for the
// length, and dropping either to "keep the screen clean" re-creates the problem.
//
// NO DEFAULT SELECTION. "Not answered" must never be able to look like "passed"; that rule is
// the reason this feature exists, and it is also why there is no bulk "all pass" affordance
// anywhere in the flow (explicitly rejected — a one-question-per-screen flow whose first
// affordance skips 67 questions is self-defeating, and more so at 80).

const AREA_LABEL: Record<InspectionArea, string> = {
  A: "Area A",
  B: "Area B",
  C: "Area C",
};

const OPTIONS: {
  value: CheckState;
  label: string;
  sublabel: string;
  kind: "ontime" | "over" | "off";
}[] = [
  { value: "pass", label: "Pass", sublabel: "Checked and serviceable", kind: "ontime" },
  { value: "defect", label: "Defect", sublabel: "Asks how bad and what", kind: "over" },
  { value: "na", label: "N/A", sublabel: "Not fitted to this vehicle", kind: "off" },
];

export function CheckStep({
  area,
  group,
  label,
  checkFor,
  value,
  note,
  onAnswer,
  onNote,
}: {
  area: InspectionArea;
  group: string;
  label: string;
  /** The form's "Check For" column, verbatim. */
  checkFor: string;
  value: CheckState | null;
  note: string;
  onAnswer: (next: CheckState) => void;
  onNote: (next: string) => void;
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
        {AREA_LABEL[area]} · {group}
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
        {label}
      </div>

      {/* The form's own words, not a paraphrase. It is the answer to "what does this row
          actually mean?", which at 67-80 rows a driver will have for several of them. */}
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: type.value,
          lineHeight: 1.45,
          color: colors.textSecondary,
        }}
      >
        {checkFor}
      </div>

      <div style={{ display: "flex", gap: gap.row }} role="group" aria-label={label}>
        {OPTIONS.map((o) => (
          <AnswerButton
            key={o.value}
            kind={o.kind}
            label={o.label}
            sublabel={o.sublabel}
            selected={value === o.value}
            onClick={() => onAnswer(o.value)}
          />
        ))}
      </div>

      {/* NL-PTI-01's Notes column, which is on EVERY row — an N/A or a Pass can carry a remark
          ("spare fitted", "not fitted to this unit") without being a defect. It saves on every
          keystroke, so the auto-advance that answering triggers cannot lose it; a driver who
          answers first reaches it again through Back or the review step's Change. */}
      <textarea
        value={note}
        onChange={(e) => onNote(e.target.value)}
        placeholder="Notes (optional) — carried on this row of the submitted form"
        rows={2}
        aria-label={`Note about ${label}`}
        style={{
          width: "100%",
          minHeight: 72,
          padding: "12px 16px",
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
