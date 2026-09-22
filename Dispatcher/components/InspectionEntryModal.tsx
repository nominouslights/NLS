"use client";

import { useEffect, useMemo, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createInspection,
  type DefectSeverityWire,
  type InspectionInput,
  type InspectionResultWire,
  type InspectionType,
} from "@/lib/api/maintenance";
import { itemsFor } from "@/lib/inspectionForm";
import { DEFECT_SEVERITY_LABEL, INSPECTION_RESULT_META } from "@/lib/workOrderDisplay";
import { listDrivers } from "@/lib/api/drivers";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import ChecklistGroupEditor, {
  unansweredCount,
  type ChecklistRow,
} from "@/components/inspection/ChecklistGroupEditor";
import {
  areaLegendKeys,
  checklistWire,
  defectsWire,
  rowsFor,
} from "@/components/inspection/checklistRows";

/** NL-PTI-01 classifies a defect Minor or Major; OutOfService stays in the wire
 *  enum for legacy rows but this form never offers it. */
const SEVERITIES: DefectSeverityWire[] = ["Minor", "Major"];

/** Re-report: a defect cleared on an earlier DVIR is back, and the dispatcher
 *  does not want to wait for the next one. Resolution is final, so this records
 *  a NEW defect pointed at the inspection it supersedes. */
export interface InspectionPrefill {
  item: string;
  severity: DefectSeverityWire;
  note?: string | null;
  recurrenceOfInspectionId: string;
}

/** A re-reported item that is NOT on the current form. Defect items are free text
 *  and some predate NL-PTI-01, so it is carried alongside the catalogue rows
 *  rather than forced into one — and it is a defect by definition, so it has no
 *  OK / N-A choice. */
interface ExtraDefect {
  item: string;
  severity: DefectSeverityWire;
  note: string;
  recurrenceOfInspectionId: string;
}

// Dispatcher data-entry for a pre/post-trip DVIR — the office fallback for when
// the Driver Field App fails in the field and the driver uses a paper backup form.
// The rows are the one fleet-wide catalogue (Form NL-PTI-01, narrowed by unit and
// mode); it used to keep its own short list, which was a fourth fork of the form.
// Saves to the live Fleet API (source "Dispatcher"), which also advances the
// vehicle odometer.

export default function InspectionEntryModal({
  vehicleId,
  unit,
  type,
  odometerKm,
  prefill,
  onClose,
  onSaved,
}: {
  vehicleId: string;
  unit: string;
  type: InspectionType;
  odometerKm: number;
  prefill?: InspectionPrefill;
  onClose: () => void;
  onSaved: (inspectionId: string) => void;
}) {
  const typeLabel = type === "PreTrip" ? "Pre-Trip" : "Post-Trip";

  const groups = useMemo(() => itemsFor(unit, type), [unit, type]);
  const legendKeys = useMemo(() => areaLegendKeys(groups), [groups]);

  // Driver roster from the real Drivers API (Active drivers only).
  const [driverNames, setDriverNames] = useState<string[]>([]);
  const [driver, setDriver] = useState("");

  useEffect(() => {
    let active = true;
    listDrivers().then(
      (rows) => {
        if (active) {
          const names = rows.filter((d) => d.status === "Active").map((d) => d.name);
          setDriverNames(names);
          setDriver((cur) => cur || names[0] || "");
        }
      },
      () => {
        // Roster unavailable — the driver select stays empty and submit's
        // "select the driver" validation surfaces the problem.
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const [odo, setOdo] = useState(String(odometerKm));

  const [rows, setRows] = useState<ChecklistRow[]>(() => {
    const base = rowsFor(unit, type);
    if (!prefill) return base;
    const i = base.findIndex((r) => r.itemKey.trim().toLowerCase() === prefill.item.trim().toLowerCase());
    if (i < 0) return base;
    return base.map((r, idx) =>
      idx === i
        ? {
            ...r,
            state: "Defect" as const,
            severity: prefill.severity,
            note: prefill.note ?? "",
            recurrenceOfInspectionId: prefill.recurrenceOfInspectionId,
          }
        : r,
    );
  });

  const [extra, setExtra] = useState<ExtraDefect | null>(() => {
    if (!prefill) return null;
    const onForm = rowsFor(unit, type).some(
      (r) => r.itemKey.trim().toLowerCase() === prefill.item.trim().toLowerCase(),
    );
    if (onForm) return null;
    return {
      item: prefill.item,
      severity: prefill.severity,
      note: prefill.note ?? "",
      recurrenceOfInspectionId: prefill.recurrenceOfInspectionId,
    };
  });

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function patchRow(itemKey: string, patch: Partial<ChecklistRow>) {
    setRows((prev) => prev.map((r) => (r.itemKey === itemKey ? { ...r, ...patch } : r)));
  }

  const defects: InspectionInput["defects"] = [
    ...defectsWire(rows),
    ...(extra
      ? [
          {
            item: extra.item,
            severity: extra.severity,
            note: extra.note.trim() || null,
            recurrenceOfInspectionId: extra.recurrenceOfInspectionId,
          },
        ]
      : []),
  ];
  const result: InspectionResultWire = defects.some(
    (d) => d.severity === "OutOfService" || d.severity === "Major",
  )
    ? "Fail"
    : defects.length > 0
      ? "PassWithDefects"
      : "Pass";
  const unanswered = unansweredCount(rows);

  function validate(): string | null {
    if (!driver) return "Select the driver who performed the inspection.";
    const defectNoNote = rows.find((r) => r.state === "Defect" && !r.note.trim());
    if (defectNoNote) return `Defect rows need a note — add one for "${defectNoNote.label}".`;
    if (extra && !extra.note.trim()) return `Defect rows need a note — add one for "${extra.item}".`;
    if (unanswered > 0) {
      return `${unanswered} of ${rows.length} checks ${unanswered === 1 ? "is" : "are"} unanswered — every row needs OK, Defect or N-A.`;
    }
    return null;
  }

  async function submit() {
    if (busy) return;
    const problem = validate();
    if (problem) {
      setError(problem);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const id = await createInspection({
        type,
        source: "Dispatcher",
        vehicleId,
        unit,
        driverName: driver,
        enteredBy: "Dispatch",
        odometerKm: parseInt(odo, 10) || odometerKm,
        checklist: [
          ...checklistWire(rows),
          ...(extra
            ? [
                {
                  group: null,
                  item: extra.item,
                  passed: false,
                  state: "Defect" as const,
                  note: extra.note.trim() || null,
                },
              ]
            : []),
        ],
        defects,
      });
      onSaved(id);
      onClose();
    } catch (e) {
      setBusy(false);
      setError(e instanceof ApiError ? e.message : "Failed to save the inspection — please try again.");
    }
  }

  const resultMeta = statusMeta(INSPECTION_RESULT_META[result].kind);

  return (
    <ModalShell
      eyebrow={`Fleet & Maintenance · ${unit} · DVIR`}
      title={`Enter ${typeLabel} Inspection`}
      onClose={onClose}
      error={error}
      maxWidth={880}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {unanswered > 0 ? `${unanswered} unanswered · ` : ""}Result:{" "}
            <span style={{ color: resultMeta.t, fontWeight: 700 }}>
              {resultMeta.g} {INSPECTION_RESULT_META[result].label}
            </span>
          </span>
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "SAVE INSPECTION"}
          </ActionButton>
        </>
      }
    >
      {/* paper-backup context banner */}
      <div
        style={{
          padding: "11px 14px",
          background: "rgba(31,111,178,.07)",
          border: `1px solid ${colors.borderActive}`,
          borderRadius: 10,
          marginBottom: 16,
          fontFamily: fonts.body,
          fontSize: 12,
          color: colors.textSecondary,
          lineHeight: 1.5,
        }}
      >
        Dispatcher paper-backup entry — use when the Driver App failed in the field and the driver
        recorded this DVIR on the backup paper form. Recorded as
        <span style={{ fontWeight: 600 }}> Dispatcher</span> entry.
      </div>

      {prefill && (
        <div
          style={{
            padding: "11px 14px",
            background: statusMeta("soon").bg,
            border: `1px solid ${statusMeta("soon").bd}`,
            borderRadius: 10,
            marginBottom: 16,
            fontFamily: fonts.body,
            fontSize: 12,
            fontWeight: 600,
            color: statusMeta("soon").t,
            lineHeight: 1.5,
          }}
        >
          {statusMeta("soon").g} Re-reporting &ldquo;{prefill.item}&rdquo; — it was cleared on an earlier
          DVIR. Resolution is final, so this records a new defect citing that one.
        </div>
      )}

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 16 }}>
        <SelectField
          label="Driver"
          value={driver}
          onChange={setDriver}
          options={driverNames.map((n) => ({ value: n, label: n }))}
        />
        <NumberField label="Odometer (km)" value={odo} onChange={setOdo} min={0} step={1} />
      </div>

      {extra && (
        <div style={{ marginBottom: 16 }}>
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 10.5,
              letterSpacing: ".12em",
              textTransform: "uppercase",
              color: colors.textLabel,
              marginBottom: 8,
            }}
          >
            Re-reported item (not on Form NL-PTI-01)
          </div>
          <div
            style={{
              padding: "9px 11px",
              borderRadius: 9,
              border: `1px solid ${statusMeta("over").bd}`,
              background: statusMeta("over").bg,
            }}
          >
            <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
              {statusMeta("over").g} {extra.item} — Defect
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "180px 1fr", gap: 10, marginTop: 9 }}>
              <SelectField
                label="Severity"
                value={extra.severity}
                onChange={(v) => setExtra((cur) => (cur ? { ...cur, severity: v as DefectSeverityWire } : cur))}
                options={SEVERITIES.map((s) => ({ value: s, label: DEFECT_SEVERITY_LABEL[s] }))}
              />
              <TextField
                label="Note (required for a defect)"
                value={extra.note}
                onChange={(v) => setExtra((cur) => (cur ? { ...cur, note: v } : cur))}
                placeholder="Describe the defect"
              />
            </div>
          </div>
        </div>
      )}

      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 10.5,
          letterSpacing: ".12em",
          textTransform: "uppercase",
          color: colors.textLabel,
          marginBottom: 8,
        }}
      >
        {typeLabel} checklist · Form NL-PTI-01 · {rows.length} checks
      </div>
      {groups.map((g) => (
        <ChecklistGroupEditor
          key={g.key}
          group={g}
          rows={rows.filter((r) => r.groupKey === g.key)}
          onPatch={patchRow}
          legend={legendKeys.has(g.key)}
        />
      ))}

      {defects.some((d) => d.severity === "OutOfService" || d.severity === "Major") && (
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: statusMeta("over").t, marginTop: 12, fontWeight: 600 }}>
          {statusMeta("over").g} Major defect present — you&apos;ll be prompted to generate a work order after saving.
        </div>
      )}
    </ModalShell>
  );
}
