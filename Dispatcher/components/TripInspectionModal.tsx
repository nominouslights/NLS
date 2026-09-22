"use client";

import { useEffect, useMemo, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import {
  ApiError,
  createInspection,
  updateInspection,
  type InspectionFuelLevel,
  type InspectionInput,
  type InspectionRoadCondition,
  type InspectionType,
  type InspectionVisibility,
  type InspectionWeather,
  type VehicleInspection,
} from "@/lib/api";
import type { TripRecord } from "@/lib/api/trips";
import { listDrivers } from "@/lib/api/drivers";
import { NL_PTI_01_CERTIFICATION, itemsFor, type InspectionFormMode } from "@/lib/inspectionForm";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { SectionLabel } from "@/components/ui/Panel";
import { FieldLabel, NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { OptChip } from "@/components/manifest/manifestRows";
import ChecklistGroupEditor, {
  groupResult,
  unansweredCount,
  type ChecklistRow,
} from "@/components/inspection/ChecklistGroupEditor";
import {
  areaLegendKeys,
  checklistWire,
  defectsWire,
  isRetiredFormRecord,
  itemsOutsideForm,
  rowsFor,
  rowsFromRecord,
} from "@/components/inspection/checklistRows";
import RetiredFormChecklist from "@/components/inspection/RetiredFormChecklist";

// Trip-context Fleet inspection entry. Posts a real Fleet VehicleInspection
// (POST /api/fleet/inspections) tagged with the trip's tripNumber + vehicleId,
// source "Dispatcher". Both halves of the form are now the SAME thing — the
// NL-PTI-01 catalogue narrowed by `itemsFor(unit, mode)`, where the post-trip
// mode simply adds the Close-Out group. Pre-trip additionally carries
// weather/road/fuel; post-trip carries issues, fuel-added and the §10
// certification.

const WEATHER_OPTIONS: { value: InspectionWeather; label: string }[] = [
  { value: "Clear", label: "Clear" },
  { value: "Cloudy", label: "Cloudy" },
  { value: "Rain", label: "Rain" },
  { value: "Snow", label: "Snow" },
  { value: "Fog", label: "Fog" },
  { value: "ExtremeCold", label: "Extreme cold" },
];

const ROAD_OPTIONS: { value: InspectionRoadCondition; label: string }[] = [
  { value: "Dry", label: "Dry" },
  { value: "Wet", label: "Wet" },
  { value: "Icy", label: "Icy" },
  { value: "SnowCovered", label: "Snow-covered" },
  { value: "Muddy", label: "Muddy" },
];

const VISIBILITY_OPTIONS: InspectionVisibility[] = ["Good", "Reduced", "Poor"];

const FUEL_LEVELS: { value: InspectionFuelLevel; label: string }[] = [
  { value: "Full", label: "Full" },
  { value: "ThreeQuarters", label: "3/4" },
  { value: "Half", label: "1/2" },
  { value: "Quarter", label: "1/4" },
];

function nowLocal(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

/** ISO → "YYYY-MM-DD HH:MM" local (the format the certifiedAt TextField expects). */
function isoToLocal(iso: string | null): string {
  if (!iso) return nowLocal();
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return nowLocal();
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

export default function TripInspectionModal({
  trip,
  type,
  existing,
  enteredBy,
  onClose,
  onSaved,
}: {
  trip: TripRecord;
  type: InspectionType;
  /** When set, the modal edits this inspection (PUT) instead of creating one.
   *  Its type is fixed and immutable — the `type` prop is ignored in that case. */
  existing?: VehicleInspection;
  enteredBy: string;
  onClose: () => void;
  onSaved: () => Promise<void>;
}) {
  // In edit mode the inspection type comes from the record itself (immutable).
  const insType: InspectionType = existing?.type ?? type;
  const isPre = insType === "PreTrip";
  const mode: InspectionFormMode = isPre ? "PreTrip" : "PostTrip";
  const defaultDriver = existing?.driverName ?? trip.driverName ?? "";

  // The unit narrows the form. A trip with no vehicle assigned genuinely has no
  // unit, and `itemsFor` answers that with the full superset on purpose — never
  // the narrower NL-01 form (see its docblock).
  //
  // In edit mode the unit comes from the RECORD, not the trip: reassigning the
  // trip's vehicle afterwards must not change which rows the saved record is
  // rebuilt against, or rows it really answered would render unanswered.
  const unit = existing ? existing.unit || null : trip.vehicleUnit;
  const groups = useMemo(() => itemsFor(unit, mode), [unit, mode]);
  const legendKeys = useMemo(() => areaLegendKeys(groups), [groups]);

  // A record written against the retired NL-TM-01 checklist cannot be rebuilt
  // into current-catalogue rows without inventing a correspondence, so it opens
  // read-only. See `isRetiredFormRecord`.
  const retired = existing != null && isRetiredFormRecord(existing);
  // Rows rebuilt from the record, and anything it answered that they do not cover.
  const rebuilt = useMemo(
    () => (existing && !retired ? rowsFromRecord(existing, unit, mode) : []),
    [existing, retired, unit, mode],
  );
  const outsideForm = existing && !retired ? itemsOutsideForm(existing, rebuilt) : [];
  /** Either data-loss shape disables editing — nothing is remapped or dropped. */
  const readOnly = retired || outsideForm.length > 0;

  // Driver roster (Active) — default to the trip's (or edited record's) driver.
  const [driverOptions, setDriverOptions] = useState<string[]>(
    defaultDriver ? [defaultDriver] : [],
  );
  const [driverName, setDriverName] = useState(defaultDriver);

  useEffect(() => {
    let active = true;
    listDrivers().then(
      (rows) => {
        if (!active) return;
        const names = rows.filter((d) => d.status === "Active").map((d) => d.name);
        const merged = defaultDriver && !names.includes(defaultDriver) ? [defaultDriver, ...names] : names;
        setDriverOptions(merged);
        setDriverName((cur) => cur || merged[0] || "");
      },
      () => undefined,
    );
    return () => {
      active = false;
    };
  }, [defaultDriver]);

  const [odometer, setOdometer] = useState(existing?.odometerKm != null ? String(existing.odometerKm) : "");
  const [checklist, setChecklist] = useState<ChecklistRow[]>(() =>
    existing ? rebuilt : rowsFor(unit, mode),
  );

  // Pre-trip sections
  const [weather, setWeather] = useState<InspectionWeather[]>(existing?.weather ?? []);
  const [roadConditions, setRoadConditions] = useState<InspectionRoadCondition[]>(existing?.roadConditions ?? []);
  const [visibility, setVisibility] = useState<InspectionVisibility | null>(existing?.visibility ?? null);
  const [temperatureC, setTemperatureC] = useState(existing?.temperatureC ?? "");
  const [roadAdvisories, setRoadAdvisories] = useState(existing?.roadAdvisories ?? "");
  const [fuelLevel, setFuelLevel] = useState<InspectionFuelLevel | null>(existing?.fuelLevel ?? null);

  // Post-trip sections
  const [issues, setIssues] = useState<string[]>(existing?.issues ?? []);
  const [signature, setSignature] = useState(existing?.driverSignatureName ?? trip.driverName ?? "");
  const [certifiedAt, setCertifiedAt] = useState(existing ? isoToLocal(existing.certifiedAt) : nowLocal());
  const [fuelAdded, setFuelAdded] = useState(existing?.fuelAdded ?? false);
  const [fuelLitres, setFuelLitres] = useState(existing?.fuelLitres != null ? String(existing.fuelLitres) : "");
  const [fuelCostCad, setFuelCostCad] = useState(existing?.fuelCostCad != null ? String(existing.fuelCostCad) : "");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function patchRow(itemKey: string, patch: Partial<ChecklistRow>) {
    setChecklist((prev) => prev.map((r) => (r.itemKey === itemKey ? { ...r, ...patch } : r)));
  }

  const result = groupResult(checklist);
  const resultMeta = statusMeta(result === "Pass" ? "ontime" : result === "Fail" ? "over" : "soon");
  const unanswered = unansweredCount(checklist);

  function validate(): string | null {
    if (!driverName.trim()) return "Select the driver who performed the inspection.";
    const defectNoNote = checklist.find((r) => r.state === "Defect" && !r.note.trim());
    if (defectNoNote) return `Defect rows need a note — add one for "${defectNoNote.label}".`;
    // With 67–80 rows, "something is unanswered" is not actionable. Say how many.
    if (unanswered > 0) {
      return `${unanswered} of ${checklist.length} checks ${unanswered === 1 ? "is" : "are"} unanswered — every row needs OK, Defect or N-A.`;
    }
    if (!isPre) {
      if (!signature.trim()) return "Driver signature name is required.";
      if (fuelAdded && !(parseFloat(fuelLitres) > 0)) return "Fuel was added — enter the litres.";
    }
    return null;
  }

  function buildInput(): InspectionInput {
    const odo = parseInt(odometer, 10);
    const base: InspectionInput = {
      type: insType,
      source: "Dispatcher",
      tripNumber: trip.tripNumber,
      vehicleId: trip.vehicleId,
      unit: trip.vehicleUnit ?? "",
      driverName: driverName.trim(),
      enteredBy,
      odometerKm: Number.isFinite(odo) ? odo : null,
      // Every row, in both modes — close-out rows are ordinary checklist rows now.
      checklist: checklistWire(checklist),
      defects: defectsWire(checklist),
    };
    if (isPre) {
      return {
        ...base,
        weather,
        temperatureC: temperatureC.trim() || null,
        roadConditions,
        visibility,
        roadAdvisories: roadAdvisories.trim() || null,
        fuelLevel,
      };
    }
    const cert = new Date(certifiedAt.trim().replace(" ", "T"));
    return {
      ...base,
      issues: issues.map((s) => s.trim()).filter(Boolean),
      // One §10 certification sentence replaces the five NL-TM-01 attestations.
      // The sentence itself is stored with the record; `attestations` keeps one
      // true so the wire field still reads "certified".
      attestations: [true],
      certificationStatement: NL_PTI_01_CERTIFICATION,
      driverSignatureName: signature.trim(),
      certifiedAt: Number.isNaN(cert.getTime()) ? null : cert.toISOString(),
      fuelAdded,
      fuelLitres: fuelAdded ? parseFloat(fuelLitres) || null : null,
      fuelCostCad: fuelAdded ? parseFloat(fuelCostCad) || null : null,
    };
  }

  async function submit() {
    if (busy || readOnly) return;
    const problem = validate();
    if (problem) {
      setError(problem);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      if (existing) {
        await updateInspection(existing.id, buildInput());
      } else {
        await createInspection(buildInput());
      }
      await onSaved();
      onClose();
    } catch (e) {
      // A pre/post-trip already exists for this trip — the backend rejects a
      // second one. Map the code to a friendly inline message.
      if (e instanceof ApiError && e.code === "Fleet.Inspection.DuplicateForTrip") {
        setError(
          `A ${isPre ? "pre-trip" : "post-trip"} inspection already exists for ${trip.tripNumber} — edit that one instead of entering another.`,
        );
      } else {
        setError(e instanceof ApiError ? e.message : "Failed to save the inspection — please try again.");
      }
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Fleet · ${trip.tripNumber} · ${trip.vehicleUnit ?? "no unit"} · ${existing ? "Edit inspection" : "Inspection"}`}
      title={
        existing
          ? isPre
            ? "Edit Pre-Trip Inspection"
            : "Edit Post-Trip Inspection"
          : isPre
            ? "Enter Pre-Trip Inspection"
            : "Enter Post-Trip Inspection"
      }
      onClose={onClose}
      error={error}
      maxWidth={880}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {readOnly ? (
              "Read-only — editing is disabled for this record"
            ) : (
              <>
                {unanswered > 0 ? `${unanswered} unanswered · ` : ""}Result:{" "}
                <span style={{ color: resultMeta.t, fontWeight: 700 }}>
                  {resultMeta.g} {result}
                </span>
              </>
            )}
          </span>
          <ActionButton onClick={onClose}>{readOnly ? "CLOSE" : "CANCEL"}</ActionButton>
          {!readOnly && (
            <ActionButton variant="primary" onClick={submit} disabled={busy}>
              {busy ? "SAVING…" : existing ? "SAVE CHANGES" : "SAVE INSPECTION"}
            </ActionButton>
          )}
        </>
      }
    >
      {readOnly && existing ? (
        <>
          <Caution kind="over">
            {retired
              ? "Recorded against a retired form revision — editing is disabled. Remove and re-enter to move this record to NL-PTI-01."
              : `This record answers ${outsideForm.length} ${outsideForm.length === 1 ? "row" : "rows"} the current form does not show for ${unit ?? "this unit"} — editing is disabled so those answers cannot be dropped. Remove and re-enter to move this record to the form as it now reads.`}
          </Caution>
          <RetiredFormChecklist inspection={existing} />
        </>
      ) : (
        <>
          {!trip.vehicleId && (
            <Caution kind="soon">
              No vehicle is assigned to this trip — the inspection will be recorded without a vehicle link (odometer
              will not advance). Assign a vehicle first for full linkage.
            </Caution>
          )}

          {!unit && (
            <Caution kind="soon">
              No unit assigned — showing the full form; mark bus-only rows N/A.
            </Caution>
          )}

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 18 }}>
            <SelectField
              label="Driver"
              value={driverName}
              onChange={setDriverName}
              options={driverOptions.map((n) => ({ value: n, label: n }))}
            />
            <NumberField
              label={isPre ? "Odometer start (km)" : "Odometer end (km)"}
              value={odometer}
              onChange={setOdometer}
              min={0}
              step={1}
            />
          </div>

          <SectionLabel>
            {isPre ? "Pre-trip checklist" : "Post-trip checklist"} · Form NL-PTI-01 · {checklist.length} checks
          </SectionLabel>
          {groups.map((g) => (
            <ChecklistGroupEditor
              key={g.key}
              group={g}
              rows={checklist.filter((r) => r.groupKey === g.key)}
              onPatch={patchRow}
              legend={legendKeys.has(g.key)}
            />
          ))}

          {isPre ? (
            <>
              <SectionLabel>Weather &amp; road conditions</SectionLabel>
              <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14, marginBottom: 14 }}>
                <div>
                  <FieldLabel>Weather (all that apply)</FieldLabel>
                  <div style={{ display: "flex", gap: 7, flexWrap: "wrap", paddingTop: 4 }}>
                    {WEATHER_OPTIONS.map((w) => (
                      <OptChip
                        key={w.value}
                        active={weather.includes(w.value)}
                        label={w.label}
                        onClick={() =>
                          setWeather((cur) => (cur.includes(w.value) ? cur.filter((x) => x !== w.value) : [...cur, w.value]))
                        }
                      />
                    ))}
                  </div>
                </div>
                <NumberField label="Temperature (°C)" value={temperatureC} onChange={setTemperatureC} step={1} />
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14, marginBottom: 14 }}>
                <div>
                  <FieldLabel>Road conditions (all that apply)</FieldLabel>
                  <div style={{ display: "flex", gap: 7, flexWrap: "wrap", paddingTop: 4 }}>
                    {ROAD_OPTIONS.map((r) => (
                      <OptChip
                        key={r.value}
                        active={roadConditions.includes(r.value)}
                        label={r.label}
                        onClick={() =>
                          setRoadConditions((cur) =>
                            cur.includes(r.value) ? cur.filter((x) => x !== r.value) : [...cur, r.value],
                          )
                        }
                      />
                    ))}
                  </div>
                </div>
                <div>
                  <FieldLabel>Visibility</FieldLabel>
                  <div style={{ display: "flex", gap: 7, flexWrap: "wrap", paddingTop: 4 }}>
                    {VISIBILITY_OPTIONS.map((v) => (
                      <OptChip key={v} active={visibility === v} label={v} onClick={() => setVisibility((c) => (c === v ? null : v))} />
                    ))}
                  </div>
                </div>
              </div>
              <div style={{ marginBottom: 14 }}>
                <TextAreaField label="Road advisories" value={roadAdvisories} onChange={setRoadAdvisories} rows={2} />
              </div>
              <div>
                <FieldLabel>Fuel level</FieldLabel>
                <div style={{ display: "flex", gap: 7, paddingTop: 4, flexWrap: "wrap" }}>
                  {FUEL_LEVELS.map((f) => (
                    <OptChip key={f.value} active={fuelLevel === f.value} label={f.label} onClick={() => setFuelLevel((c) => (c === f.value ? null : f.value))} />
                  ))}
                </div>
              </div>
            </>
          ) : (
            <>
              <SectionLabel>Issues / defects</SectionLabel>
              <div style={{ marginBottom: 16 }}>
                {issues.map((line, i) => (
                  <div key={i} style={{ display: "grid", gridTemplateColumns: "1fr auto", gap: 10, alignItems: "end", marginBottom: 8 }}>
                    <TextField label={`Issue ${i + 1}`} value={line} onChange={(v) => setIssues((cur) => cur.map((x, y) => (y === i ? v : x)))} />
                    <div style={{ paddingBottom: 6 }}>
                      <span
                        onClick={() => setIssues((cur) => cur.filter((_, x) => x !== i))}
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
                ))}
                <ActionButton onClick={() => setIssues((cur) => [...cur, ""])}>+ ADD ISSUE</ActionButton>
              </div>

              <SectionLabel>Fuel added</SectionLabel>
              <div style={{ display: "flex", alignItems: "flex-end", gap: 14, marginBottom: 16 }}>
                <div style={{ display: "flex", gap: 7 }}>
                  <OptChip active={fuelAdded} label="Yes" onClick={() => setFuelAdded(true)} />
                  <OptChip active={!fuelAdded} label="No" onClick={() => setFuelAdded(false)} />
                </div>
                {fuelAdded && (
                  <div style={{ display: "grid", gridTemplateColumns: "150px 150px", gap: 14 }}>
                    <NumberField label="Litres" value={fuelLitres} onChange={setFuelLitres} min={0} step={1} />
                    <NumberField label="Fuel cost (CAD)" value={fuelCostCad} onChange={setFuelCostCad} min={0} step={1} />
                  </div>
                )}
              </div>

              <SectionLabel>Certification (Form NL-PTI-01, §10)</SectionLabel>
              <div
                style={{
                  padding: "11px 14px",
                  background: colors.inputBg,
                  border: `1px solid ${colors.border}`,
                  borderRadius: 10,
                  margin: "6px 0 4px",
                  fontFamily: fonts.body,
                  fontSize: 12.5,
                  color: colors.textSecondary,
                  lineHeight: 1.6,
                }}
              >
                {NL_PTI_01_CERTIFICATION}
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 6 }}>
                Signing below records this sentence verbatim with the inspection.
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginTop: 8 }}>
                <TextField label="Driver signature name" value={signature} onChange={setSignature} />
                <TextField label="Certified date / time" value={certifiedAt} onChange={setCertifiedAt} mono placeholder="YYYY-MM-DD HH:MM" />
              </div>
            </>
          )}
        </>
      )}
    </ModalShell>
  );
}

/** Colour + icon + label caution/warning line. */
function Caution({ kind, children }: { kind: "soon" | "over"; children: React.ReactNode }) {
  const m = statusMeta(kind);
  return (
    <div
      style={{
        padding: "11px 14px",
        background: m.bg,
        border: `1px solid ${m.bd}`,
        borderRadius: 10,
        marginBottom: 16,
        fontFamily: fonts.body,
        fontSize: 12,
        fontWeight: 600,
        color: m.t,
        lineHeight: 1.5,
      }}
    >
      {m.g} {children}
    </div>
  );
}
