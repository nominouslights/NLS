"use client";

import { colors, fonts, type StatusKind } from "@/lib/theme";
import { gap, type } from "@/lib/tablet";
import { StatusButton, TouchButton } from "@/components/ui-tablet/TouchButton";
import { TabletChip } from "../shared";
import type { SectionShortcut } from "@/lib/inspectionSteps";
import type { InspectionArea } from "@/lib/inspectionForm";
import type { CheckState } from "@/lib/types";

// APP-LOCAL. The confirm panel for the per-section "All OK" shortcut (NL-PTI-01 rev 3). It
// replaces the check step's body — inside the same WizardFrame — until the driver confirms or
// cancels.
//
// THE DRIVER MUST SEE WHAT THEY ARE CERTIFYING. So every row of the sub-group is listed by its
// label with what will happen to it: a blank row "Will be OK", an answered one keeping its
// answer ("Defect — kept"). Each of those is colour + glyph + word (TabletChip), never colour
// alone. Only then is there one confirming tap.
//
// HEIGHT BUDGET — WizardFrame has no scroll container, so this must fit ~524px or it is
// silently clipped. Label-only, two columns, read down then across: the reasoning and the
// numbers are on `wizard.sectionMaxRows` in lib/tablet.ts, and the step model refuses the
// shortcut for a sub-group larger than that rather than letting this list overflow. Check For
// is left out on purpose — at 12 rows it cannot fit — and Cancel returns to the row-by-row walk
// that shows it.

const AREA_LABEL: Record<InspectionArea, string> = {
  A: "Area A",
  B: "Area B",
  C: "Area C",
};

const KEPT: Record<CheckState, { kind: StatusKind; label: string }> = {
  pass: { kind: "ontime", label: "Pass — kept" },
  defect: { kind: "over", label: "Defect — kept" },
  na: { kind: "off", label: "N/A — kept" },
};

export function SectionConfirm({
  section,
  onConfirm,
  onCancel,
}: {
  section: SectionShortcut;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const perColumn = Math.ceil(section.rows.length / 2);

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
        {AREA_LABEL[section.area]} · {section.title}
      </div>

      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: type.metric,
          lineHeight: 1.1,
          color: colors.headingBright,
        }}
      >
        Mark {section.unanswered} unanswered checks OK?
      </div>

      <div
        style={{
          fontFamily: fonts.body,
          fontSize: type.label,
          lineHeight: 1.35,
          color: colors.textSecondary,
        }}
      >
        Confirm only if you have inspected every row below. Each unanswered row is recorded as Pass,
        exactly as if you had tapped it. Rows you already answered are not changed, and you can
        change any row afterwards.
      </div>

      <ul
        aria-label={`Checks in ${section.title}`}
        style={{
          listStyle: "none",
          margin: 0,
          padding: 0,
          display: "grid",
          gridTemplateColumns: "1fr 1fr",
          gridTemplateRows: `repeat(${perColumn}, auto)`,
          gridAutoFlow: "column",
          columnGap: gap.row,
          rowGap: gap.tight,
        }}
      >
        {section.rows.map((row) => {
          const meta = row.state === null ? { kind: "ontime" as const, label: "Will be OK" } : KEPT[row.state];
          return (
            <li
              key={row.itemId}
              style={{
                display: "flex",
                alignItems: "center",
                gap: gap.tight,
                minWidth: 0,
                padding: "2px 0",
                borderBottom: `1px solid ${colors.borderSubtle}`,
              }}
            >
              <span
                style={{
                  flex: 1,
                  minWidth: 0,
                  fontFamily: fonts.body,
                  fontSize: type.label,
                  lineHeight: 1.25,
                  color: row.state === null ? colors.textPrimary : colors.textDim,
                }}
              >
                {row.label}
              </span>
              <span style={{ flex: "none" }}>
                <TabletChip kind={meta.kind} label={meta.label} />
              </span>
            </li>
          );
        })}
      </ul>

      <div style={{ display: "flex", gap: gap.row }}>
        <StatusButton kind="ontime" active label="Confirm all OK" onClick={onConfirm} />
        <TouchButton variant="secondary" onClick={onCancel}>
          Cancel — check one by one
        </TouchButton>
      </div>
    </div>
  );
}
