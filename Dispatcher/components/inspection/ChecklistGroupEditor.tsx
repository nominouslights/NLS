"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { ChecklistItemStateWire, DefectSeverityWire } from "@/lib/api/maintenance";
import type { InspectionItem, InspectionSubGroup } from "@/lib/inspectionForm";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// One NL-PTI-01 sub-group. Each row is the paper form's three boxes — OK /
// Defect / N-A — and starts UNANSWERED: with 28–64 rows, defaulting them to OK
// would let a dispatcher save a form nobody actually filled in. The one exception is
// a new TRIP inspection, whose rows start at OK by the owner's decision (see
// `TRIP_ROW_START` in checklistRows.ts) — this editor just renders whatever state it
// is given. The one shortcut is
// per sub-group: "Mark unanswered OK" answers only that section's still-blank rows,
// for transcribing a paper section the driver ticked through. Defect expands a
// severity select; the note applies on any state (an N-A wants a reason as much
// as a defect does). Rows carry the backend wire keys (group + item); everything
// else on screen comes from the catalogue entry, so the form text and the wire
// value can never drift apart.

export interface ChecklistRow {
  /** Backend wire group string (InspectionSubGroup.key). */
  groupKey: string;
  /** Backend wire item string (InspectionItem.key) — half of a defect's address. */
  itemKey: string;
  /** Long display label, as printed on the form. */
  label: string;
  /** NL-PTI-01's tri-state answer. `null` is UNANSWERED, not a state the wire has. */
  state: ChecklistItemStateWire | null;
  severity: DefectSeverityWire;
  note: string;
  /** Set only on a re-reported row — the inspection whose resolution of this same
   *  item this new report supersedes. */
  recurrenceOfInspectionId?: string | null;
}

/** NL-PTI-01 classifies a defect Minor or Major and nothing else. `OutOfService`
 *  stays in the backend enum so legacy rows still read back, but this form never
 *  offers it. */
const SEVERITY_OPTIONS: { value: DefectSeverityWire; label: string }[] = [
  { value: "Minor", label: "Minor" },
  { value: "Major", label: "Major" },
];

/** Verbatim — the marker legend printed under each area of the form. */
export const SCOPE_LEGEND =
  "NL = Northern Link operational addition beyond Manitoba Reg 95/2008 Schedule B. " +
  "All unmarked rows are the NSC Standard 13 requirement.";

/** A row counts as answered once it has any of the three states. */
export function isAnswered(row: ChecklistRow): boolean {
  return row.state != null;
}

export function unansweredCount(rows: ChecklistRow[]): number {
  return rows.filter((r) => !isAnswered(r)).length;
}

/**
 * The rows "Mark unanswered OK" may touch: ONLY those still unanswered. A Defect or
 * an N-A is somebody's deliberate answer and is never overwritten. Returned as item
 * keys so the caller patches each one through its ordinary `onPatch`.
 */
export function unansweredItemKeys(rows: ChecklistRow[]): string[] {
  return rows.filter((r) => !isAnswered(r)).map((r) => r.itemKey);
}

/** Result for a set of rows. A NotApplicable row is NOT a failure — it is an
 *  answer, matching the backend's own rule that `passed == state != Defect`. */
export function groupResult(rows: ChecklistRow[]): "Pass" | "Pass with defects" | "Fail" {
  const defects = rows.filter((r) => r.state === "Defect");
  if (defects.some((d) => d.severity === "Major" || d.severity === "OutOfService")) return "Fail";
  if (defects.length > 0) return "Pass with defects";
  return "Pass";
}

export default function ChecklistGroupEditor({
  group,
  rows,
  onPatch,
  legend = false,
  readOnly = false,
}: {
  group: InspectionSubGroup;
  rows: ChecklistRow[];
  onPatch: (itemKey: string, patch: Partial<ChecklistRow>) => void;
  /** Print the marker legend under this sub-group — set on the last sub-group of
   *  each area so the legend appears once per area rather than thirteen times. */
  legend?: boolean;
  /** Disables every answer control, including "Mark unanswered OK". */
  readOnly?: boolean;
}) {
  const result = groupResult(rows);
  const rm = statusMeta(result === "Pass" ? "ontime" : result === "Fail" ? "over" : "soon");
  const open = unansweredCount(rows);
  const byKey = new Map(rows.map((r) => [r.itemKey, r]));
  // Only rows this sub-group actually renders — a row with no catalogue item here
  // is not on screen, so a bulk action must not answer it unseen.
  const shown = group.items.flatMap((item) => byKey.get(item.key) ?? []);
  const toMark = unansweredItemKeys(shown);
  const canMark = !readOnly && toMark.length > 0;

  // Per sub-group ON PURPOSE — there is no whole-form "all OK". Transcribing a paper
  // form section by section, each section the driver ticked through is one
  // deliberate act; a single form-wide button would let a blank form be saved as a
  // clean pass. Defect and N-A answers are never touched.
  function markUnansweredOk() {
    if (!canMark) return;
    for (const key of toMark) onPatch(key, { state: "Ok" });
  }

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
        <ActionButton
          onClick={markUnansweredOk}
          disabled={!canMark}
          style={{ fontSize: 11, padding: "3px 9px", alignSelf: "center" }}
        >
          {canMark ? `MARK ${toMark.length} UNANSWERED OK` : "MARK UNANSWERED OK"}
        </ActionButton>
        <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          {open > 0 ? `${open} unanswered · ` : ""}Result:{" "}
          <span style={{ color: rm.t, fontWeight: 700 }}>
            {rm.g} {result}
          </span>
        </span>
      </div>

      <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
        {group.items.map((item) => {
          const r = byKey.get(item.key);
          if (!r) return null;
          return <ItemRow key={item.key} item={item} row={r} onPatch={onPatch} readOnly={readOnly} />;
        })}
      </div>

      {legend && (
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 11,
            color: colors.textDim,
            lineHeight: 1.5,
            marginTop: 8,
            paddingTop: 7,
            borderTop: `1px solid ${colors.borderSubtle}`,
          }}
        >
          {SCOPE_LEGEND}
        </div>
      )}
    </div>
  );
}

function ItemRow({
  item,
  row,
  onPatch,
  readOnly,
}: {
  item: InspectionItem;
  row: ChecklistRow;
  onPatch: (itemKey: string, patch: Partial<ChecklistRow>) => void;
  readOnly: boolean;
}) {
  const isDefect = row.state === "Defect";
  const isNa = row.state === "NotApplicable";
  // Only the two noteworthy answers get the accent bar. With 28–64 rows, marking
  // every answered row would leave the highlight carrying no signal; "unanswered"
  // is called out by its own chip instead.
  const accent = isDefect ? statusMeta("over").c : isNa ? statusMeta("off").c : colors.blue;

  return (
    <div style={{ padding: "9px 11px", ...rowSurface(isDefect || isNa, accent), cursor: "default" }}>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 210px", gap: 10, alignItems: "start" }}>
        <div>
          <div style={{ display: "flex", alignItems: "baseline", gap: 6, flexWrap: "wrap" }}>
            <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
              {item.label}
            </span>
            {/* Scope markers — NL-02 for a unit-specific row, NL for a company
                addition beyond NSC 13. Both where both apply. */}
            {item.scope === "NL02Only" && <MonoTag>NL-02</MonoTag>}
            {item.basis === "NorthernLink" && <MonoTag>NL</MonoTag>}
            {row.state == null && <StatusChip kind="soon" label="Unanswered" />}
          </div>
          {/* The form's "Check For" column: with up to 64 rows a dispatcher needs to know
              what "Ground beneath the vehicle" actually means. */}
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: colors.textDim,
              lineHeight: 1.45,
              marginTop: 2,
            }}
          >
            {item.checkFor}
          </div>
          {item.categoryNote && (
            <div
              style={{
                fontFamily: fonts.body,
                fontSize: 11,
                fontWeight: 600,
                color: statusMeta("soon").t,
                lineHeight: 1.45,
                marginTop: 2,
              }}
            >
              {statusMeta("soon").g} {item.categoryNote}
            </div>
          )}
        </div>
        <div style={{ display: "flex", gap: 6, justifyContent: "flex-end", flexWrap: "wrap" }}>
          <StateToggle
            active={row.state === "Ok"}
            label="OK"
            kind="ontime"
            disabled={readOnly}
            onClick={() => onPatch(row.itemKey, { state: "Ok" })}
          />
          <StateToggle
            active={isDefect}
            label="DEFECT"
            kind="over"
            disabled={readOnly}
            onClick={() => onPatch(row.itemKey, { state: "Defect" })}
          />
          <StateToggle
            active={isNa}
            label="N-A"
            kind="off"
            disabled={readOnly}
            onClick={() => onPatch(row.itemKey, { state: "NotApplicable" })}
          />
        </div>
      </div>

      {/* The note applies on ANY state — required only for a defect, but an N-A on
          a compliance form is worth a reason too. */}
      <div style={{ display: "grid", gridTemplateColumns: isDefect ? "180px 1fr" : "1fr", gap: 10, marginTop: 9 }}>
        {isDefect && (
          <SelectField
            label="Severity"
            value={row.severity}
            onChange={(v) => onPatch(row.itemKey, { severity: v as DefectSeverityWire })}
            options={SEVERITY_OPTIONS}
            disabled={readOnly}
          />
        )}
        <TextField
          label={isDefect ? "Note (required for a defect)" : "Note (optional)"}
          value={row.note}
          onChange={(v) => onPatch(row.itemKey, { note: v })}
          disabled={readOnly}
          placeholder={
            isDefect
              ? "Describe the defect"
              : row.state === "NotApplicable"
                ? "Why this row does not apply"
                : "Anything worth recording"
          }
        />
      </div>
    </div>
  );
}

function StateToggle({
  active,
  label,
  kind,
  disabled,
  onClick,
}: {
  active: boolean;
  label: string;
  kind: "ontime" | "over" | "off";
  disabled: boolean;
  onClick: () => void;
}) {
  const m = statusMeta(kind);
  return (
    <span
      onClick={disabled ? undefined : onClick}
      aria-disabled={disabled || undefined}
      style={{
        fontFamily: fonts.body,
        fontWeight: 600,
        fontSize: 11.5,
        padding: "4px 9px",
        borderRadius: 7,
        cursor: disabled ? "not-allowed" : "pointer",
        opacity: disabled && !active ? 0.6 : 1,
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
