"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, wizard } from "@/lib/tablet";
import { AnswerButton } from "@/components/ui-tablet/AnswerButton";
import type { CheckState } from "@/lib/types";

// APP-LOCAL. One checklist item, alone on the screen: the group as an eyebrow, the item as the
// question, three answer tiles. Nothing else — anything more is something for a driver to read
// instead of looking at the vehicle.
//
// NO DEFAULT SELECTION. "Not answered" must never be able to look like "passed"; that rule is
// the reason this feature exists, and it is also why there is no bulk "all pass" affordance
// anywhere in the flow (explicitly rejected — a one-question-per-screen flow whose first
// affordance skips all 22 questions is self-defeating).

const OPTIONS: {
  value: CheckState;
  label: string;
  sublabel: string;
  kind: "ontime" | "over" | "off";
}[] = [
  { value: "pass", label: "Pass", sublabel: "Checked and serviceable", kind: "ontime" },
  { value: "defect", label: "Defect", sublabel: "Asks what and how bad", kind: "over" },
  { value: "na", label: "N/A", sublabel: "Not fitted to this vehicle", kind: "off" },
];

export function CheckStep({
  group,
  label,
  value,
  onAnswer,
}: {
  group: string;
  label: string;
  value: CheckState | null;
  onAnswer: (next: CheckState) => void;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: gap.section, minHeight: 0 }}>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 16,
          letterSpacing: ".16em",
          textTransform: "uppercase",
          color: colors.textLabel,
        }}
      >
        {group}
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
    </div>
  );
}
