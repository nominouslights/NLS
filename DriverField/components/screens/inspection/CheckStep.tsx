"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { gap, radius, type, wizard } from "@/lib/tablet";
import { AnswerButton } from "@/components/ui-tablet/AnswerButton";
import { StatusButton } from "@/components/ui-tablet/TouchButton";
import { SectionConfirm } from "./SectionConfirm";
import type { InspectionArea } from "@/lib/inspectionForm";
import type { SectionShortcut } from "@/lib/inspectionSteps";
import type { CheckState } from "@/lib/types";

// APP-LOCAL. One NL-PTI-01 row, alone on the screen: where the driver is, the row itself, what
// the form says to check for, three answer tiles and the row's Notes column.
//
// WHY THERE IS MORE ON THIS SCREEN THAN THERE USED TO BE. The old 22-item list was all
// self-explanatory labels ("Engine oil level"). NL-PTI-01 is up to 64 rows of the real form, and
// "Ground beneath the vehicle" means nothing without its Check For column ("No fresh oil,
// coolant, fuel, brake fluid or transmission fluid on the ground"). The area and sub-group line
// is the other half of that: a walk-around that long needs the driver to be able to see where
// they are standing, not just which question they are on. Both are the mitigation for the
// length, and dropping either to "keep the screen clean" re-creates the problem.
//
// NO DEFAULT SELECTION. "Not answered" must never be able to look like "passed"; that rule is
// the reason this feature exists, and it is also why there is no WHOLE-FORM "all pass"
// affordance anywhere in the flow (explicitly rejected — a one-question-per-screen flow whose
// first affordance skips every question is self-defeating).
//
// THE PER-SECTION SHORTCUT IS THE BOUNDED EXCEPTION (NL-PTI-01 rev 3, at the owner's request).
// On the first unanswered row of a sub-group, a separate "All OK — <section> (n checks)" button
// sits on the area line, well away from the three answer tiles. It answers nothing by itself:
// it swaps this body for SectionConfirm, which lists every row of the sub-group, and only that
// panel's "Confirm all OK" writes — blanks only, one sub-group only (markSectionOk in
// lib/inspectionStore.ts). The rules are written out above sectionShortcut() in
// lib/inspectionSteps.ts.
//
// Height: the button makes the area line touch.primary tall (+37px); the budget is on
// lib/tablet.ts's `wizard`. The area text may ellipsize to make room — the button repeats the
// sub-group title, so nothing is lost.

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
  section,
  onSectionOk,
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
  /** The per-section shortcut, when sectionShortcut() offers it on this row; null otherwise. */
  section?: SectionShortcut | null;
  /** Applies the shortcut. Called only from SectionConfirm's "Confirm all OK". */
  onSectionOk?: () => void;
}) {
  // Local, and reset for free: WizardFrame re-keys its body on the step id, so leaving this
  // step by any route drops an open confirm panel.
  const [confirming, setConfirming] = useState(false);

  if (confirming && section && onSectionOk) {
    return (
      <SectionConfirm
        section={section}
        onConfirm={onSectionOk}
        onCancel={() => setConfirming(false)}
      />
    );
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: gap.row, minHeight: 0 }}>
      <div style={{ display: "flex", alignItems: "center", gap: gap.row, minWidth: 0 }}>
        <div
          style={{
            flex: 1,
            minWidth: 0,
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
            fontFamily: fonts.semiCondensed,
            fontSize: 16,
            letterSpacing: ".16em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          {AREA_LABEL[area]} · {group}
        </div>
        {section && onSectionOk ? (
          <div style={{ flex: "none" }}>
            <StatusButton
              kind="ontime"
              label={`All OK — ${section.title} (${section.unanswered} checks)`}
              onClick={() => setConfirming(true)}
            />
          </div>
        ) : null}
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
          actually mean?", which at up to 64 rows a driver will have for several of them. */}
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
