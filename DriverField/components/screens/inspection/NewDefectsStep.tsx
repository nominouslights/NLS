"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { gap, radius, touch, type, wizard } from "@/lib/tablet";
import { AnswerButton } from "@/components/ui-tablet/AnswerButton";
import { StatusButton, TouchButton } from "@/components/ui-tablet/TouchButton";
import { TabletChip } from "../shared";
import { severityGlyph, severityKind } from "@/lib/inspectionGate";
import { optionsForRow, type NewDefectOption } from "@/lib/newDefects";
import type { FormSeverity, NewDefectDraft } from "@/lib/types";

// APP-LOCAL. The post-trip's "New defects since the pre-trip" step (NL-PTI-01 rev 4).
//
// It replaces the old 21-row re-check and the single "Defects noticed while driving" row. The
// driver answers one question — "Any defect found after the pre-trip?" — and, on Yes, lists each
// NEW defect against the pre-trip row it concerns, with Minor/Major and a required note. The
// Dispatch Console's counterpart is Dispatcher/components/inspection/NewDefectsEditor.tsx; this
// is not a copy of it (that one is a desktop grid of copied Field/Select primitives at ~34px).
//
// AN EXPLICIT NO/YES, NEVER A DEFAULT. "No new defects" must be a recorded answer, not an
// omission (Man. Reg. 95/2008 s.12(1) — a report with nothing wrong says so). Both tiles start
// unselected, exactly like a check's three tiles, and Certify is blocked until one is chosen.
//
// TWO BODIES, ONE STEP, like CheckStep and SectionConfirm. The list body asks the question and
// lists what has been entered; the editor body fills in ONE defect. Local state, reset for free
// because WizardFrame re-keys its body on the step id. Every edit writes straight to the draft
// (through the callbacks), so nothing typed here can be lost to navigation or a reload.
//
// THE ITEM PICKER IS A NATIVE <select>, deliberately: on the Android tablet Chrome opens it as a
// full-screen OS list with large rows, which is the right control for choosing one of up to 64
// form rows with a gloved thumb. Grouped by sub-group (<optgroup>) in form order. It lists only
// what newDefectOptions() allows — this unit's pre-trip rows, minus the items today's pre-trip
// already reported — and optionsForRow() also hides items picked on another row of this report
// (the backend rejects two defects on one item: DuplicateDefectItem).
//
// HEIGHT. WizardFrame has no scroll container, so both bodies are budgeted against the ~524px a
// step gets (lib/tablet.ts's `wizard`). Editor, worst case: area line 19 + heading 44 + picker
// label 20 + picker 56 + the form's guidance (two lines at 16) 48 + severity row 56 + note 72 +
// action row 56 + seven gap.row 98 ≈ 469. List body with Yes: area line 19 + question 44 + two
// lines of explanation 48 + the No/Yes row 56 + Add button 56 + five gap.row 70 ≈ 293, leaving
// ~230 for the list — three rows. A fourth and later row scrolls INSIDE the list's own bounded
// region: the question, the answer and the Add button never leave the screen, and a list of
// entries is reference material being re-read, the same reason ReviewStep owns an overflow.
//
// SEVERITY is two StatusButtons, Minor (gold) and Major (vermillion), each with its kind's glyph
// and its label — NL-PTI-01 has no third box (see DefectStep for why "Out of Service" is never
// offered). The 168px AnswerButton tiles are kept for the No/Yes question; at that size two
// severity tiles would not fit the editor's budget beside the picker and the note.

const AREA_LINE_STYLE = {
  fontFamily: fonts.semiCondensed,
  fontSize: 16,
  letterSpacing: ".16em",
  textTransform: "uppercase" as const,
  color: colors.textLabel,
};

const QUESTION_STYLE = {
  fontFamily: fonts.condensed,
  fontWeight: 700,
  fontSize: wizard.question,
  lineHeight: 1.1,
  color: colors.headingBright,
};

const SEVERITIES: FormSeverity[] = ["Minor", "Major"];

export function NewDefectsStep({
  found,
  defects,
  options,
  preTripReported,
  onAnswer,
  onAdd,
  onUpdate,
  onRemove,
}: {
  /** The No/Yes answer; null until the driver answers. */
  found: boolean | null;
  defects: NewDefectDraft[];
  /** newDefectOptions(unit, alreadyReported) — what any row may be filed against. */
  options: NewDefectOption[];
  /** How many items today's pre-trip reported, and so are left out of `options`. */
  preTripReported: number;
  onAnswer: (found: boolean) => void;
  onAdd: () => void;
  onUpdate: (index: number, patch: Partial<NewDefectDraft>) => void;
  onRemove: (index: number) => void;
}) {
  const [editing, setEditing] = useState<number | null>(null);

  if (found === true && editing !== null && editing < defects.length) {
    return (
      <DefectEditor
        index={editing}
        total={defects.length}
        defect={defects[editing]}
        options={optionsForRow(options, defects, editing)}
        onUpdate={(patch) => onUpdate(editing, patch)}
        onDone={() => setEditing(null)}
        onRemove={() => {
          onRemove(editing);
          setEditing(null);
        }}
      />
    );
  }

  const labelOf = new Map(options.map((o) => [o.key, o.label]));

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: gap.row, minHeight: 0, flex: 1 }}>
      <div style={AREA_LINE_STYLE}>Post-trip · since the pre-trip</div>

      <div style={QUESTION_STYLE}>Any defect found after the pre-trip?</div>

      <div
        style={{
          fontFamily: fonts.body,
          fontSize: type.label,
          lineHeight: 1.5,
          color: colors.textSecondary,
        }}
      >
        Anything that broke, wore or started leaking on the run. Each one is filed against the
        pre-trip item it concerns.
        {preTripReported > 0
          ? ` ${preTripReported} ${preTripReported === 1 ? "item" : "items"} already reported on today’s pre-trip ${preTripReported === 1 ? "is" : "are"} not listed — ${preTripReported === 1 ? "that is" : "those are"} not new.`
          : ""}
      </div>

      {found === true ? (
        <>
          {/* Compact once answered Yes, so the list has room. Still colour + glyph + label. */}
          <div style={{ display: "flex", gap: gap.row }} role="group" aria-label="New defects answer">
            <StatusButton kind="ontime" label="No new defects" onClick={() => onAnswer(false)} />
            <StatusButton kind="over" label="Yes — new defects" active />
          </div>

          <ul
            aria-label="New defects"
            style={{
              listStyle: "none",
              margin: 0,
              padding: 0,
              flex: 1,
              minHeight: 0,
              overflowY: "auto",
              display: "flex",
              flexDirection: "column",
              gap: gap.tight,
            }}
          >
            {defects.length === 0 ? (
              <li style={{ fontFamily: fonts.body, fontSize: type.label, color: colors.textDim }}>
                No defect listed yet — add one, or answer No.
              </li>
            ) : null}
            {defects.map((d, i) => {
              const need = rowGap(d);
              return (
              <li
                key={i}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: gap.row,
                  minHeight: touch.primary,
                  padding: "4px 4px 4px 14px",
                  borderRadius: radius.control,
                  border: `1px solid ${colors.border}`,
                  background: colors.cardBg,
                }}
              >
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontWeight: 600,
                      fontSize: type.label,
                      color: colors.headingBright,
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {i + 1}. {d.itemKey === "" ? "Item not picked" : (labelOf.get(d.itemKey) ?? d.itemKey)}
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 14,
                      color: colors.textDim,
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                      whiteSpace: "nowrap",
                    }}
                  >
                    {d.note.trim() || "No note yet"}
                  </div>
                </div>
                <div style={{ flex: "none", display: "flex", alignItems: "center", gap: 10 }}>
                  {d.severity ? (
                    <TabletChip
                      kind={severityKind(d.severity)}
                      glyph={severityGlyph(d.severity)}
                      label={d.severity}
                    />
                  ) : (
                    <TabletChip kind="off" label="Not graded" />
                  )}
                  {need ? <TabletChip kind="soon" label={need} /> : null}
                  <TouchButton
                    variant="secondary"
                    onClick={() => setEditing(i)}
                    style={{ minHeight: touch.min }}
                  >
                    Edit
                  </TouchButton>
                </div>
              </li>
              );
            })}
          </ul>

          <div style={{ flex: "none" }}>
            <TouchButton
              variant="secondary"
              onClick={() => {
                // The new row's index is the current length; open it straight away.
                const next = defects.length;
                onAdd();
                setEditing(next);
              }}
            >
              + Add a defect
            </TouchButton>
          </div>
        </>
      ) : (
        <div style={{ display: "flex", gap: gap.row }} role="group" aria-label="New defects answer">
          <AnswerButton
            kind="ontime"
            label="No"
            sublabel="Nothing new since the pre-trip"
            selected={found === false}
            onClick={() => onAnswer(false)}
          />
          <AnswerButton
            kind="over"
            label="Yes"
            sublabel="List each new defect"
            selected={false}
            onClick={() => {
              // Answering Yes to an empty list seeds one blank row (the store does that) —
              // open it, so the next thing on screen is the defect to fill in.
              const wasEmpty = defects.length === 0;
              onAnswer(true);
              if (wasEmpty) setEditing(0);
            }}
          />
        </div>
      )}
    </div>
  );
}

/** What a listed row still needs, shortest first — shown as a gold chip on the row. */
function rowGap(d: NewDefectDraft): string | null {
  if (d.itemKey === "") return "Needs item";
  if (d.note.trim() === "") return "Needs note";
  return null;
}

function DefectEditor({
  index,
  total,
  defect,
  options,
  onUpdate,
  onDone,
  onRemove,
}: {
  index: number;
  total: number;
  defect: NewDefectDraft;
  /** Already narrowed for THIS row (optionsForRow). */
  options: NewDefectOption[];
  onUpdate: (patch: Partial<NewDefectDraft>) => void;
  onDone: () => void;
  onRemove: () => void;
}) {
  const selectId = `new-defect-item-${index}`;
  const picked = options.find((o) => o.key === defect.itemKey) ?? null;
  // A stored pick that is no longer allowed (a reassignment, or the pre-trip reported it since)
  // is SHOWN as such rather than letting the native select silently display another row.
  const stale = defect.itemKey !== "" && picked === null;

  const groups: { title: string; items: NewDefectOption[] }[] = [];
  for (const o of options) {
    const last = groups[groups.length - 1];
    if (last && last.title === o.group) last.items.push(o);
    else groups.push({ title: o.group, items: [o] });
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: gap.row, minHeight: 0 }}>
      <div style={AREA_LINE_STYLE}>
        Post-trip · new defect {index + 1} of {total}
      </div>

      <div style={QUESTION_STYLE}>What did you find?</div>

      <div style={{ display: "flex", flexDirection: "column", gap: gap.tight }}>
        <label
          htmlFor={selectId}
          style={{
            fontFamily: fonts.semiCondensed,
            fontWeight: 600,
            fontSize: type.label,
            color: colors.headingBright,
          }}
        >
          Item — the pre-trip row it concerns
        </label>
        <select
          id={selectId}
          value={defect.itemKey}
          onChange={(e) => onUpdate({ itemKey: e.target.value })}
          aria-invalid={defect.itemKey === "" || stale}
          style={{
            width: "100%",
            maxWidth: 720,
            minHeight: touch.primary,
            padding: "0 16px",
            borderRadius: radius.control,
            border: `1px solid ${colors.borderStrong}`,
            background: colors.inputBg,
            color: colors.textPrimary,
            fontFamily: fonts.body,
            fontSize: type.value,
          }}
        >
          {defect.itemKey === "" ? <option value="">— Pick the item —</option> : null}
          {stale ? (
            <option value={defect.itemKey}>{defect.itemKey} — not available for this vehicle</option>
          ) : null}
          {groups.map((g) => (
            <optgroup key={g.title} label={g.title}>
              {g.items.map((o) => (
                <option key={o.key} value={o.key}>
                  {o.label}
                </option>
              ))}
            </optgroup>
          ))}
        </select>
      </div>

      {/* The form's own guidance for the picked row, verbatim — as DefectStep shows it. The
          DRIVER grades the defect; this line is not a default selection. */}
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: type.label,
          lineHeight: 1.5,
          color: colors.textSecondary,
          minHeight: 24,
        }}
      >
        {picked ? (
          <>
            The form classifies this row as <strong>{picked.category}</strong>
            {picked.categoryNote ? ` — “${picked.categoryNote}”` : ""}. You decide.
          </>
        ) : stale ? (
          "That item cannot take a new defect on this report — pick another, or remove this one."
        ) : (
          "Pick the item first; the form's guidance for it appears here."
        )}
      </div>

      <div style={{ display: "flex", gap: gap.row }} role="group" aria-label="New defect severity">
        {/* StatusButton draws its kind's default glyph. For Minor and Major that IS
            severityGlyph() — the override exists for "Out of Service", which is never offered. */}
        {SEVERITIES.map((s) => (
          <StatusButton
            key={s}
            kind={severityKind(s)}
            label={s}
            active={defect.severity === s}
            onClick={() => onUpdate({ severity: s })}
          />
        ))}
      </div>

      <textarea
        value={defect.note}
        onChange={(e) => onUpdate({ note: e.target.value })}
        placeholder="What was found, and where (required)"
        rows={2}
        aria-label={`Note about new defect ${index + 1}`}
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

      <div style={{ display: "flex", gap: gap.row }}>
        <TouchButton onClick={onDone}>Done</TouchButton>
        <TouchButton variant="secondary" onClick={onRemove}>
          Remove this defect
        </TouchButton>
      </div>
    </div>
  );
}
