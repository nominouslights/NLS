"use client";

import { useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import {
  ApiError,
  createInspection,
  type InspectionFuelLevel,
  type InspectionInput,
  type InspectionRoadCondition,
  type InspectionType,
  type InspectionVisibility,
  type InspectionWeather,
} from "@/lib/api";
import type { TripRecord } from "@/lib/api/trips";
import { listDrivers } from "@/lib/api/drivers";
import {
  ATTESTATIONS,
  POST_TRIP_ITEMS,
  PRE_TRIP_GROUPS,
} from "@/lib/tripManifestChecklist";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { SectionLabel } from "@/components/ui/Panel";
import { FieldLabel, NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { OptChip } from "@/components/manifest/manifestRows";
import ChecklistGroupEditor, { type ChecklistRow, groupResult } from "@/components/inspection/ChecklistGroupEditor";

// Trip-context Fleet inspection entry. Posts a real Fleet VehicleInspection
// (POST /api/fleet/inspections) tagged with the trip's tripNumber + vehicleId,
// source "Dispatcher". Pre-trip carries the checklist + odometer + weather/road/
// fuel; post-trip carries the checklist + odometer + issues + attestations +
// signature + fuel-added. Salvaged from the retired Manual Trip Entry form.

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

function initialChecklistRows(): ChecklistRow[] {
  return PRE_TRIP_GROUPS.flatMap((g) =>
    g.items.map((it) => ({
      groupKey: g.key,
      itemKey: it.key,
      label: it.label,
      fail: false,
      severity: "Minor" as const,
      note: "",
    })),
  );
}

function nowLocal(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

export default function TripInspectionModal({
  trip,
  type,
  enteredBy,
  onClose,
  onSaved,
}: {
  trip: TripRecord;
  type: InspectionType;
  enteredBy: string;
  onClose: () => void;
  onSaved: () => Promise<void>;
}) {
  const isPre = type === "PreTrip";

  // Driver roster (Active) — default to the trip's assigned driver.
  const [driverOptions, setDriverOptions] = useState<string[]>(
    trip.driverName ? [trip.driverName] : [],
  );
  const [driverName, setDriverName] = useState(trip.driverName ?? "");

  useEffect(() => {
    let active = true;
    listDrivers().then(
      (rows) => {
        if (!active) return;
        const names = rows.filter((d) => d.status === "Active").map((d) => d.name);
        const merged = trip.driverName && !names.includes(trip.driverName) ? [trip.driverName, ...names] : names;
        setDriverOptions(merged);
        setDriverName((cur) => cur || merged[0] || "");
      },
      () => undefined,
    );
    return () => {
      active = false;
    };
  }, [trip.driverName]);

  const [odometer, setOdometer] = useState("");
  const [checklist, setChecklist] = useState<ChecklistRow[]>(initialChecklistRows);

  // Pre-trip sections
  const [weather, setWeather] = useState<InspectionWeather[]>([]);
  const [roadConditions, setRoadConditions] = useState<InspectionRoadCondition[]>([]);
  const [visibility, setVisibility] = useState<InspectionVisibility | null>(null);
  const [temperatureC, setTemperatureC] = useState("");
  const [roadAdvisories, setRoadAdvisories] = useState("");
  const [fuelLevel, setFuelLevel] = useState<InspectionFuelLevel | null>(null);

  // Post-trip sections
  const [postChecks, setPostChecks] = useState<boolean[]>(POST_TRIP_ITEMS.map(() => true));
  const [issues, setIssues] = useState<string[]>([]);
  const [attest, setAttest] = useState<boolean[]>(ATTESTATIONS.map(() => false));
  const [signature, setSignature] = useState(trip.driverName ?? "");
  const [certifiedAt, setCertifiedAt] = useState(nowLocal());
  const [fuelAdded, setFuelAdded] = useState(false);
  const [fuelLitres, setFuelLitres] = useState("");
  const [fuelCostCad, setFuelCostCad] = useState("");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function patchRow(itemKey: string, patch: Partial<ChecklistRow>) {
    setChecklist((prev) => prev.map((r) => (r.itemKey === itemKey ? { ...r, ...patch } : r)));
  }

  const result = isPre
    ? groupResult(checklist)
    : postChecks.every(Boolean)
      ? "Pass"
      : "Pass with defects";
  const resultMeta = statusMeta(result === "Pass" ? "ontime" : result === "Fail" ? "over" : "soon");

  function validate(): string | null {
    if (!driverName.trim()) return "Select the driver who performed the inspection.";
    if (isPre) {
      const failNoNote = checklist.find((r) => r.fail && !r.note.trim());
      if (failNoNote) return `FAIL items need a note — add one for "${failNoNote.label}".`;
    } else {
      if (!attest.every(Boolean)) return "All post-trip attestations must be checked.";
      if (!signature.trim()) return "Driver signature name is required.";
      if (fuelAdded && !(parseFloat(fuelLitres) > 0)) return "Fuel was added — enter the litres.";
    }
    return null;
  }

  function buildInput(): InspectionInput {
    const odo = parseInt(odometer, 10);
    const base: InspectionInput = {
      type,
      source: "Dispatcher",
      tripNumber: trip.tripNumber,
      vehicleId: trip.vehicleId,
      unit: trip.vehicleUnit ?? "",
      driverName: driverName.trim(),
      enteredBy,
      odometerKm: Number.isFinite(odo) ? odo : null,
      checklist: isPre
        ? checklist.map((r) => ({ group: r.groupKey, item: r.itemKey, passed: !r.fail }))
        : POST_TRIP_ITEMS.map((item, i) => ({ group: null, item, passed: postChecks[i] })),
      defects: isPre
        ? checklist
            .filter((r) => r.fail)
            .map((r) => ({ item: r.itemKey, severity: r.severity, note: r.note.trim() || null }))
        : [],
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
      attestations: attest,
      driverSignatureName: signature.trim(),
      certifiedAt: Number.isNaN(cert.getTime()) ? null : cert.toISOString(),
      fuelAdded,
      fuelLitres: fuelAdded ? parseFloat(fuelLitres) || null : null,
      fuelCostCad: fuelAdded ? parseFloat(fuelCostCad) || null : null,
    };
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
      await createInspection(buildInput());
      await onSaved();
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the inspection — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Fleet · ${trip.tripNumber} · ${trip.vehicleUnit ?? "no unit"} · Inspection`}
      title={isPre ? "Enter Pre-Trip Inspection" : "Enter Post-Trip Inspection"}
      onClose={onClose}
      error={error}
      maxWidth={880}
      footer={
        <>
          <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            Result:{" "}
            <span style={{ color: resultMeta.t, fontWeight: 700 }}>
              {resultMeta.g} {result}
            </span>
          </span>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "SAVE INSPECTION"}
          </ActionButton>
        </>
      }
    >
      {!trip.vehicleId && (
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
          }}
        >
          ◐ No vehicle is assigned to this trip — the inspection will be recorded without a vehicle link (odometer
          will not advance). Assign a vehicle first for full linkage.
        </div>
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

      {isPre ? (
        <>
          <SectionLabel>Pre-trip checklist</SectionLabel>
          {PRE_TRIP_GROUPS.map((g) => (
            <ChecklistGroupEditor
              key={g.group}
              group={g}
              rows={checklist.filter((r) => r.groupKey === g.key)}
              onPatch={patchRow}
            />
          ))}

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
          <SectionLabel>Post-trip checklist</SectionLabel>
          <div style={{ marginBottom: 16 }}>
            {POST_TRIP_ITEMS.map((item, i) => (
              <CheckRow
                key={item}
                checked={postChecks[i]}
                label={item}
                onToggle={() => setPostChecks((cur) => cur.map((v, x) => (x === i ? !v : v)))}
              />
            ))}
          </div>

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

          <SectionLabel>Certification</SectionLabel>
          <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, margin: "6px 0 8px" }}>
            I certify that (check each attestation):
          </div>
          {ATTESTATIONS.map((a, i) => (
            <CheckRow
              key={a}
              checked={attest[i]}
              label={`(${i + 1}) ${a}`}
              onToggle={() => setAttest((cur) => cur.map((v, x) => (x === i ? !v : v)))}
            />
          ))}
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginTop: 14 }}>
            <TextField label="Driver signature name" value={signature} onChange={setSignature} />
            <TextField label="Certified date / time" value={certifiedAt} onChange={setCertifiedAt} mono placeholder="YYYY-MM-DD HH:MM" />
          </div>
        </>
      )}
    </ModalShell>
  );
}

function CheckRow({ checked, label, onToggle }: { checked: boolean; label: string; onToggle: () => void }) {
  const m = statusMeta("ontime");
  return (
    <div
      onClick={onToggle}
      style={{ display: "flex", gap: 9, alignItems: "flex-start", cursor: "pointer", padding: "5px 0", userSelect: "none" }}
    >
      <span
        style={{
          width: 18,
          height: 18,
          flex: "none",
          borderRadius: 5,
          border: `1px solid ${checked ? m.bd : colors.borderStrong}`,
          background: checked ? m.c : colors.inputBg,
          color: m.bt,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: 11,
          fontWeight: 800,
        }}
      >
        {checked ? "✓" : ""}
      </span>
      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.45 }}>{label}</span>
    </div>
  );
}
