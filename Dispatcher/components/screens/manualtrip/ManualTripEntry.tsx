"use client";

import { useEffect, useId, useRef, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import {
  ApiError,
  createTripManifest,
  getTripManifest,
  type ManifestCargoSecured,
  type ManifestDirection,
  type ManifestFuelLevel,
  type ManifestRoadCondition,
  type ManifestVisibility,
  type ManifestWeather,
  type TripManifest,
  type TripManifestInput,
} from "@/lib/api";
import {
  ATTESTATIONS,
  MAX_CARGO_ROWS,
  MAX_PASSENGER_ROWS,
  POST_TRIP_ITEMS,
  PRE_TRIP_GROUPS,
} from "@/lib/tripManifestChecklist";
import { communities, drivers, fleet, trips } from "@/lib/data";
import type { WorkOrderPrefill, WorkOrderPriority } from "@/lib/types";
import { printTripManifest } from "@/lib/documents/tripManifestPdf";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { ModalError } from "@/components/ui/ModalShell";
import { FieldLabel, NumberField, TextAreaField, TextField, SelectField } from "@/components/ui/Field";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import WorkOrderModal from "@/components/WorkOrderModal";
import ChecklistGroupEditor, { type ChecklistRow } from "./ChecklistGroupEditor";

// Manual Trip Entry — transcribe a completed paper NL-TM-01 Trip Manifest &
// Vehicle Inspection Report into the Trips API. Ten numbered sections mirror
// the paper form; the record is saved Paper-sourced with transcriber + timestamp.

const EYEBROW = "Operations · Paper manifest transcription";
const TITLE = "Manual Trip Entry";

const FUEL_LEVELS: { value: ManifestFuelLevel; label: string }[] = [
  { value: "Full", label: "Full" },
  { value: "ThreeQuarters", label: "3/4" },
  { value: "Half", label: "1/2" },
  { value: "Quarter", label: "1/4" },
];

const WEATHER_OPTIONS: { value: ManifestWeather; label: string }[] = [
  { value: "Clear", label: "Clear" },
  { value: "Cloudy", label: "Cloudy" },
  { value: "Rain", label: "Rain" },
  { value: "Snow", label: "Snow" },
  { value: "Fog", label: "Fog" },
  { value: "ExtremeCold", label: "Extreme Cold" },
];

const ROAD_OPTIONS: { value: ManifestRoadCondition; label: string }[] = [
  { value: "Dry", label: "Dry" },
  { value: "Wet", label: "Wet" },
  { value: "Icy", label: "Icy" },
  { value: "SnowCovered", label: "Snow-covered" },
  { value: "Muddy", label: "Muddy" },
];

const VISIBILITY_OPTIONS: ManifestVisibility[] = ["Good", "Reduced", "Poor"];

interface PaxRow {
  name: string;
  contact: string;
  pickup: string;
  dropoff: string;
  idVerified: boolean;
  boardedOn: boolean;
  boardedOff: boolean;
}

interface CargoRow {
  description: string;
  ownerRecipient: string;
  weightKg: string;
  chargeCad: string;
  hazmat: boolean;
  secured: boolean;
}

const emptyPax = (): PaxRow => ({
  name: "",
  contact: "",
  pickup: "",
  dropoff: "",
  idVerified: false,
  boardedOn: false,
  boardedOff: false,
});

const emptyCargo = (): CargoRow => ({
  description: "",
  ownerRecipient: "",
  weightKg: "",
  chargeCad: "",
  hazmat: false,
  secured: true,
});

function initialRows(): ChecklistRow[] {
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

function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}

function nowLocal(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`;
}

// ---------------------------------------------------------------------------

export default function ManualTripEntry() {
  const [epoch, setEpoch] = useState(0);
  const [saved, setSaved] = useState<TripManifest | null>(null);
  const [woPrefill, setWoPrefill] = useState<WorkOrderPrefill | null>(null);
  const submitRef = useRef<(() => void) | null>(null);

  function onSaved(record: TripManifest, prefill: WorkOrderPrefill | null) {
    setSaved(record);
    setWoPrefill(prefill);
  }

  function reset() {
    setSaved(null);
    setWoPrefill(null);
    setEpoch((e) => e + 1);
  }

  const woUnits = fleet.map((f) => ({ value: f.unit, label: `${f.unit} · ${f.model}` }));
  if (saved && !woUnits.some((u) => u.value === saved.unit)) {
    woUnits.unshift({ value: saved.unit, label: saved.unit });
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader
          eyebrow={EYEBROW}
          title={TITLE}
          right={
            !saved ? (
              <div style={{ display: "flex", gap: 9 }}>
                <ActionButton onClick={() => printTripManifest(null)}>PRINT BLANK FORM</ActionButton>
                <ActionButton variant="primary" onClick={() => submitRef.current?.()}>
                  SAVE MANIFEST
                </ActionButton>
              </div>
            ) : undefined
          }
        />
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ maxWidth: 880, margin: "0 auto", padding: "20px 26px 40px" }}>
          {saved ? (
            <SuccessPanel record={saved} onEnterAnother={reset} />
          ) : (
            <TranscriptionForm key={epoch} submitRef={submitRef} onSaved={onSaved} />
          )}
        </div>
      </div>

      {saved && woPrefill && (
        <WorkOrderModal
          units={woUnits}
          defaultUnit={saved.unit}
          prefill={woPrefill}
          onClose={() => setWoPrefill(null)}
          onSaved={() => undefined}
        />
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Success panel — replaces the form after a 201.
// ---------------------------------------------------------------------------

function SuccessPanel({ record, onEnterAnother }: { record: TripManifest; onEnterAnother: () => void }) {
  const m = statusMeta("ontime");
  return (
    <Panel borderColor={m.bd} style={{ background: m.bg, padding: "22px 24px" }}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 10 }}>
        <StatusChip kind="ontime" label="Manifest saved" />
        <MonoTag color={colors.skyBlue}>{record.id}</MonoTag>
      </div>
      <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright, marginBottom: 6 }}>
        Paper manifest transcribed
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textSecondary, lineHeight: 1.6, marginBottom: 16 }}>
        Trip <span style={{ fontFamily: fonts.mono }}>{record.tripNumber}</span> · Unit{" "}
        <span style={{ fontFamily: fonts.mono }}>{record.unit}</span> · {record.driverName}. Recorded as{" "}
        <span style={{ fontWeight: 600 }}>Paper-sourced</span>
        {record.enteredBy ? <> — transcribed by <span style={{ fontWeight: 600 }}>{record.enteredBy}</span></> : null}.
      </div>
      <div style={{ display: "flex", gap: 9, flexWrap: "wrap" }}>
        <ActionButton variant="primary" onClick={() => printTripManifest(record)}>
          PRINT MANIFEST PDF
        </ActionButton>
        <ActionButton onClick={onEnterAnother}>ENTER ANOTHER</ActionButton>
      </div>
    </Panel>
  );
}

// ---------------------------------------------------------------------------
// The 10-section transcription form.
// ---------------------------------------------------------------------------

function TranscriptionForm({
  submitRef,
  onSaved,
}: {
  submitRef: React.MutableRefObject<(() => void) | null>;
  onSaved: (record: TripManifest, woPrefill: WorkOrderPrefill | null) => void;
}) {
  // §1 Trip information
  const [prefillSel, setPrefillSel] = useState("");
  const [tripDate, setTripDate] = useState(todayIso());
  const [tripNumber, setTripNumber] = useState("");
  const [route, setRoute] = useState("");
  const [client, setClient] = useState("");
  const [direction, setDirection] = useState<ManifestDirection | null>(null);

  // §2 Vehicle & driver
  const [unit, setUnit] = useState("");
  const [licencePlate, setLicencePlate] = useState("");
  const [driverName, setDriverName] = useState("");
  const [driverLicenceNo, setDriverLicenceNo] = useState("");
  const [odoStart, setOdoStart] = useState("");
  const [fuelLevel, setFuelLevel] = useState<ManifestFuelLevel | null>(null);

  // §3 Pre-trip inspection
  const [rows, setRows] = useState<ChecklistRow[]>(initialRows);

  // §4 Weather & road
  const [weather, setWeather] = useState<ManifestWeather[]>([]);
  const [roadConditions, setRoadConditions] = useState<ManifestRoadCondition[]>([]);
  const [visibility, setVisibility] = useState<ManifestVisibility | null>(null);
  const [temperatureC, setTemperatureC] = useState("");
  const [roadAdvisories, setRoadAdvisories] = useState("");

  // §5 Passengers
  const [passengers, setPassengers] = useState<PaxRow[]>([emptyPax()]);
  const [allSeatbeltsVerified, setAllSeatbeltsVerified] = useState(false);

  // §6 Cargo
  const [cargo, setCargo] = useState<CargoRow[]>([emptyCargo()]);
  const [allCargoSecured, setAllCargoSecured] = useState<ManifestCargoSecured | null>(null);

  // §7 Issues
  const [noIssues, setNoIssues] = useState(false);
  const [issues, setIssues] = useState<string[]>([]);

  // §8 Completion log
  const [departureTime, setDepartureTime] = useState("");
  const [arrivalTime, setArrivalTime] = useState("");
  const [odoEnd, setOdoEnd] = useState("");
  const [fuelAdded, setFuelAdded] = useState(false);
  const [fuelLitres, setFuelLitres] = useState("");
  const [fuelCostCad, setFuelCostCad] = useState("");

  // §9 Post-trip (default checked)
  const [postTrip, setPostTrip] = useState<boolean[]>(POST_TRIP_ITEMS.map(() => true));

  // §10 Certification
  const [attest, setAttest] = useState<boolean[]>(ATTESTATIONS.map(() => false));
  const [signature, setSignature] = useState("");
  const [certifiedAtStr, setCertifiedAtStr] = useState(nowLocal());
  const [transcribedBy, setTranscribedBy] = useState("Dispatch");

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // --- helpers -------------------------------------------------------------

  function applyUnit(u: string) {
    setUnit(u);
    const v = fleet.find((f) => f.unit === u);
    if (v) setLicencePlate(v.plate);
  }

  function applyDriver(name: string) {
    setSignature((sig) => (sig === "" || sig === driverName ? name : sig));
    setDriverName(name);
  }

  function applyTripPrefill(id: string) {
    setPrefillSel(id);
    const t = trips.find((x) => x.id === id);
    if (!t) return;
    setTripNumber(t.id);
    setRoute(t.stops.join(" → "));
    setClient(t.client);
    const unitMatch = t.vehicle.match(/U-\d+/)?.[0];
    if (unitMatch) applyUnit(unitMatch);
    if (t.driver) applyDriver(t.driver);
  }

  function patchRow(itemKey: string, patch: Partial<ChecklistRow>) {
    const cur = rows.find((r) => r.itemKey === itemKey);
    // A newly-recorded FAIL auto-suggests an editable §7 issue line.
    if (patch.fail === true && cur && !cur.fail) {
      const line = `${cur.label} — FAIL (see §3)`;
      setIssues((is) => (is.includes(line) ? is : [...is, line]));
      setNoIssues(false);
    }
    setRows((prev) => prev.map((r) => (r.itemKey === itemKey ? { ...r, ...patch } : r)));
  }

  const paxCount = passengers.filter((p) => p.name.trim()).length;
  const cargoTotal = cargo.reduce((sum, c) => sum + (parseFloat(c.chargeCad) || 0), 0);
  const odoStartNum = parseInt(odoStart, 10);
  const odoEndNum = parseInt(odoEnd, 10);
  const totalKm =
    Number.isFinite(odoStartNum) && Number.isFinite(odoEndNum) ? odoEndNum - odoStartNum : null;

  // --- validation (mirrors the domain invariants; backend stays authoritative) ---

  function validate(): string | null {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(tripDate.trim())) return "Enter the trip date as YYYY-MM-DD (§1).";
    if (!tripNumber.trim()) return "Trip number is required (§1).";
    if (!unit.trim()) return "Vehicle unit is required (§2).";
    if (!driverName.trim()) return "Driver name is required (§2).";
    const failNoNote = rows.find((r) => r.fail && !r.note.trim());
    if (failNoNote) return `FAIL items need a note — add one for "${failNoNote.label}" (§3).`;
    if (totalKm != null && totalKm < 0) return "Odometer end must be greater than or equal to odometer start (§8).";
    if (fuelAdded && !(parseFloat(fuelLitres) > 0)) return "Fuel was added — enter the litres (§8).";
    if (!attest.every(Boolean)) return "All five driver attestations must be checked as they appear on the paper form (§10).";
    if (!signature.trim()) return "Driver signature name is required (§10).";
    if (!transcribedBy.trim()) return "Transcribed by is required — paper entries record who transcribed them (§10).";
    return null;
  }

  function buildInput(): TripManifestInput {
    const cert = new Date(certifiedAtStr.trim().replace(" ", "T"));
    return {
      tripDate: tripDate.trim(),
      tripNumber: tripNumber.trim(),
      route: route.trim(),
      direction,
      client: client.trim() || null,
      unit: unit.trim(),
      driverName: driverName.trim(),
      driverLicenceNo: driverLicenceNo.trim() || null,
      licencePlate: licencePlate.trim() || null,
      odometerStartKm: Number.isFinite(odoStartNum) ? odoStartNum : null,
      fuelLevel,
      preTrip: rows.map((r) => ({
        group: r.groupKey,
        item: r.itemKey,
        status: r.fail ? ("Fail" as const) : ("Ok" as const),
        severity: r.fail ? r.severity : null,
        note: r.fail ? r.note.trim() : null,
      })),
      weather,
      temperatureC: temperatureC.trim() || null,
      roadConditions,
      visibility,
      roadAdvisories: roadAdvisories.trim() || null,
      passengers: passengers
        .filter((p) => p.name.trim())
        .slice(0, MAX_PASSENGER_ROWS)
        .map((p) => ({
          name: p.name.trim(),
          contact: p.contact.trim() || null,
          pickup: p.pickup.trim() || null,
          dropoff: p.dropoff.trim() || null,
          idVerified: p.idVerified,
          boardedOn: p.boardedOn,
          boardedOff: p.boardedOff,
        })),
      allSeatbeltsVerified,
      cargo: cargo
        .filter((c) => c.description.trim())
        .slice(0, MAX_CARGO_ROWS)
        .map((c) => ({
          description: c.description.trim(),
          ownerRecipient: c.ownerRecipient.trim() || null,
          weightKg: parseFloat(c.weightKg) || null,
          chargeCad: parseFloat(c.chargeCad) || null,
          hazmat: c.hazmat,
          secured: c.secured,
        })),
      allCargoSecured,
      issues: noIssues ? [] : issues.map((s) => s.trim()).filter(Boolean),
      noIssues,
      departureTime: departureTime.trim() || null,
      arrivalTime: arrivalTime.trim() || null,
      odometerEndKm: Number.isFinite(odoEndNum) ? odoEndNum : null,
      totalKm,
      fuelAdded,
      fuelLitres: fuelAdded ? parseFloat(fuelLitres) || null : null,
      fuelCostCad: fuelAdded ? parseFloat(fuelCostCad) || null : null,
      postTrip: POST_TRIP_ITEMS.map((item, i) => ({ item, ok: postTrip[i] })),
      attestations: attest,
      driverSignatureName: signature.trim(),
      certifiedAt: Number.isNaN(cert.getTime()) ? undefined : cert.toISOString(),
      source: "Paper",
      enteredBy: transcribedBy.trim(),
    };
  }

  function workOrderPrefillFromFails(): WorkOrderPrefill | null {
    const serious = rows.filter((r) => r.fail && (r.severity === "Major" || r.severity === "OutOfService"));
    if (serious.length === 0) return null;
    const priority: WorkOrderPriority = serious.some((r) => r.severity === "OutOfService")
      ? "Critical"
      : "High";
    const sevLabel = (s: ChecklistRow["severity"]) => (s === "OutOfService" ? "Out-of-Service" : s);
    return {
      source: "Pre-Trip Inspection",
      sourceRef: tripNumber.trim(),
      title:
        serious.length === 1
          ? `${serious[0].itemKey} — ${unit.trim()}`
          : `Pre-trip FAIL items — ${unit.trim()}`,
      description: `FAIL item(s) recorded on paper manifest ${tripNumber.trim()} (transcribed by ${transcribedBy.trim()}).`,
      lineItems: rows
        .filter((r) => r.fail)
        .map((r) => `${r.itemKey} — ${sevLabel(r.severity)}${r.note.trim() ? `: ${r.note.trim()}` : ""}`),
      priority,
    };
  }

  async function submit() {
    if (busy) return;
    const problem = validate();
    if (problem) {
      setError(problem);
      return;
    }
    setError(null);
    setBusy(true);
    try {
      const { id } = await createTripManifest(buildInput());
      const record = await getTripManifest(id);
      onSaved(record, workOrderPrefillFromFails());
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Unexpected error — please try again.");
      setBusy(false);
    }
  }

  // Expose submit to the sticky header SAVE button (ref written post-render).
  useEffect(() => {
    submitRef.current = submit;
  });

  // --- render ----------------------------------------------------------------

  return (
    <div>
      {/* paper-backup context banner (InspectionEntryModal pattern) */}
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
        Dispatcher paper transcription — use when the Driver App failed in the field and the driver
        completed the paper NL-TM-01 backup form. The manifest is recorded as
        <span style={{ fontWeight: 600 }}> Paper-sourced</span>
        {" with the transcriber's name and a timestamp."}
      </div>

      {error && <ModalError message={error} />}

      {/* §1 */}
      <Section n={1} title="Trip Information">
        <div style={{ marginBottom: 14 }}>
          <SelectField
            label="Prefill from trip (optional)"
            value={prefillSel}
            onChange={applyTripPrefill}
            options={[
              { value: "", label: "— none —" },
              ...trips.map((t) => ({ value: t.id, label: `${t.id} · ${t.stops.join(" → ")}` })),
            ]}
            hint={<span style={{ color: colors.textFaint }}>· fills the fields below; everything stays editable</span>}
          />
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
          <TextField label="Date" value={tripDate} onChange={setTripDate} mono placeholder="YYYY-MM-DD" />
          <TextField label="Trip #" value={tripNumber} onChange={setTripNumber} mono placeholder="TR-4823" />
          <div>
            <FieldLabel>Direction</FieldLabel>
            <div style={{ display: "flex", gap: 7, paddingTop: 6 }}>
              {(["Inbound", "Outbound"] as ManifestDirection[]).map((d) => (
                <OptChip
                  key={d}
                  active={direction === d}
                  label={d}
                  onClick={() => setDirection((cur) => (cur === d ? null : d))}
                />
              ))}
            </div>
          </div>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14, marginTop: 14 }}>
          <TextField label="Route" value={route} onChange={setRoute} placeholder="Thompson → Leaf Rapids → Lynn Lake" />
          <TextField label="Client" value={client} onChange={setClient} placeholder="Alamos Gold" />
        </div>
      </Section>

      {/* §2 */}
      <Section n={2} title="Vehicle & Driver Information">
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14 }}>
          <ComboField
            label="Vehicle unit"
            value={unit}
            onChange={applyUnit}
            options={fleet.map((f) => f.unit)}
            mono
            placeholder="U-04"
            hint={<span style={{ color: colors.textFaint }}>· pick or type; plate autofills</span>}
          />
          <ComboField
            label="Driver name"
            value={driverName}
            onChange={applyDriver}
            options={drivers.map((d) => d.name)}
            placeholder="D. Chartrand"
          />
          <NumberField label="Odometer start (km)" value={odoStart} onChange={setOdoStart} min={0} step={1} />
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14, marginTop: 14 }}>
          <TextField label="Licence plate" value={licencePlate} onChange={setLicencePlate} mono placeholder="MB · XXX 000" />
          <TextField label="Driver licence #" value={driverLicenceNo} onChange={setDriverLicenceNo} mono />
          <div>
            <FieldLabel>Fuel level</FieldLabel>
            <div style={{ display: "flex", gap: 7, paddingTop: 6, flexWrap: "wrap" }}>
              {FUEL_LEVELS.map((f) => (
                <OptChip
                  key={f.value}
                  active={fuelLevel === f.value}
                  label={f.label}
                  onClick={() => setFuelLevel((cur) => (cur === f.value ? null : f.value))}
                />
              ))}
            </div>
          </div>
        </div>
      </Section>

      {/* §3 */}
      <Section n={3} title="Pre-Trip Vehicle Inspection">
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, marginBottom: 12 }}>
          Mark each item exactly as recorded on the paper form. FAIL items require a severity and note,
          and are logged to §7 automatically.
        </div>
        {PRE_TRIP_GROUPS.map((g) => (
          <ChecklistGroupEditor
            key={g.group}
            group={g}
            rows={rows.filter((r) => r.groupKey === g.key)}
            onPatch={patchRow}
          />
        ))}
      </Section>

      {/* §4 */}
      <Section n={4} title="Weather & Road Conditions">
        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14 }}>
          <div>
            <FieldLabel>Weather (all that apply)</FieldLabel>
            <div style={{ display: "flex", gap: 7, flexWrap: "wrap", paddingTop: 6 }}>
              {WEATHER_OPTIONS.map((w) => (
                <OptChip
                  key={w.value}
                  active={weather.includes(w.value)}
                  label={w.label}
                  onClick={() =>
                    setWeather((cur) =>
                      cur.includes(w.value) ? cur.filter((x) => x !== w.value) : [...cur, w.value],
                    )
                  }
                />
              ))}
            </div>
          </div>
          <TextField label="Temperature (°C)" value={temperatureC} onChange={setTemperatureC} mono placeholder="-28" />
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 14, marginTop: 14 }}>
          <div>
            <FieldLabel>Road conditions (all that apply)</FieldLabel>
            <div style={{ display: "flex", gap: 7, flexWrap: "wrap", paddingTop: 6 }}>
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
            <div style={{ display: "flex", gap: 7, flexWrap: "wrap", paddingTop: 6 }}>
              {VISIBILITY_OPTIONS.map((v) => (
                <OptChip
                  key={v}
                  active={visibility === v}
                  label={v}
                  onClick={() => setVisibility((cur) => (cur === v ? null : v))}
                />
              ))}
            </div>
          </div>
        </div>
        <div style={{ marginTop: 14 }}>
          <TextAreaField label="Road advisories" value={roadAdvisories} onChange={setRoadAdvisories} rows={2} />
        </div>
      </Section>

      {/* §5 */}
      <Section n={5} title="Passenger Manifest">
        {passengers.map((p, i) => (
          <div
            key={i}
            style={{
              border: `1px solid ${colors.borderSubtle}`,
              borderRadius: 9,
              padding: "11px 12px",
              marginBottom: 8,
            }}
          >
            <div style={{ display: "grid", gridTemplateColumns: "24px 1.4fr 1.2fr", gap: 10, alignItems: "end" }}>
              <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textDim, paddingBottom: 12 }}>
                {i + 1}
              </div>
              <TextField label="Passenger name" value={p.name} onChange={(v) => patchPax(i, { name: v })} />
              <TextField label="Email / phone" value={p.contact} onChange={(v) => patchPax(i, { contact: v })} />
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto auto", gap: 10, marginTop: 9, alignItems: "end" }}>
              <ComboField label="Pickup" value={p.pickup} onChange={(v) => patchPax(i, { pickup: v })} options={communities} />
              <ComboField label="Drop-off" value={p.dropoff} onChange={(v) => patchPax(i, { dropoff: v })} options={communities} />
              <div style={{ display: "flex", gap: 6, paddingBottom: 6 }}>
                <OptChip active={p.idVerified} label="ID verified" onClick={() => patchPax(i, { idVerified: !p.idVerified })} />
                <OptChip active={p.boardedOn} label="On" onClick={() => patchPax(i, { boardedOn: !p.boardedOn })} />
                <OptChip active={p.boardedOff} label="Off" onClick={() => patchPax(i, { boardedOff: !p.boardedOff })} />
              </div>
              <div style={{ paddingBottom: 6 }}>
                <RemoveButton onClick={() => setPassengers((cur) => cur.filter((_, x) => x !== i))} />
              </div>
            </div>
          </div>
        ))}
        <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 4 }}>
          {passengers.length < MAX_PASSENGER_ROWS && (
            <ActionButton onClick={() => setPassengers((cur) => [...cur, emptyPax()])}>+ ADD PASSENGER</ActionButton>
          )}
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            Total passengers: <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>{paxCount}</span>{" "}
            (max {MAX_PASSENGER_ROWS})
          </span>
          <div style={{ marginLeft: "auto" }}>
            <CheckRow
              checked={allSeatbeltsVerified}
              label="All seatbelts verified"
              onToggle={() => setAllSeatbeltsVerified((v) => !v)}
            />
          </div>
        </div>
      </Section>

      {/* §6 */}
      <Section n={6} title="Cargo Manifest">
        {cargo.map((c, i) => (
          <div
            key={i}
            style={{
              border: `1px solid ${colors.borderSubtle}`,
              borderRadius: 9,
              padding: "11px 12px",
              marginBottom: 8,
            }}
          >
            <div style={{ display: "grid", gridTemplateColumns: "24px 1.4fr 1fr", gap: 10, alignItems: "end" }}>
              <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textDim, paddingBottom: 12 }}>
                {i + 1}
              </div>
              <TextField label="Description" value={c.description} onChange={(v) => patchCargo(i, { description: v })} />
              <TextField label="Owner / recipient" value={c.ownerRecipient} onChange={(v) => patchCargo(i, { ownerRecipient: v })} />
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto auto", gap: 10, marginTop: 9, alignItems: "end" }}>
              <NumberField label="Weight (kg)" value={c.weightKg} onChange={(v) => patchCargo(i, { weightKg: v })} min={0} step={1} />
              <NumberField label="Charge (CAD)" value={c.chargeCad} onChange={(v) => patchCargo(i, { chargeCad: v })} min={0} step={5} />
              <div style={{ display: "flex", gap: 6, paddingBottom: 6 }}>
                <OptChip active={c.hazmat} label="Hazmat" onClick={() => patchCargo(i, { hazmat: !c.hazmat })} />
                <OptChip active={c.secured} label="Secured" onClick={() => patchCargo(i, { secured: !c.secured })} />
              </div>
              <div style={{ paddingBottom: 6 }}>
                <RemoveButton onClick={() => setCargo((cur) => cur.filter((_, x) => x !== i))} />
              </div>
            </div>
          </div>
        ))}
        <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 4, flexWrap: "wrap" }}>
          {cargo.length < MAX_CARGO_ROWS && (
            <ActionButton onClick={() => setCargo((cur) => [...cur, emptyCargo()])}>+ ADD CARGO</ActionButton>
          )}
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            Total charges:{" "}
            <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>
              ${cargoTotal.toFixed(2)}
            </span>
          </span>
          <div style={{ marginLeft: "auto", display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textLabel }}>All cargo secured:</span>
            <OptChip
              active={allCargoSecured === "Yes"}
              label="Yes"
              onClick={() => setAllCargoSecured((cur) => (cur === "Yes" ? null : "Yes"))}
            />
            <OptChip
              active={allCargoSecured === "NotApplicable"}
              label="N/A"
              onClick={() => setAllCargoSecured((cur) => (cur === "NotApplicable" ? null : "NotApplicable"))}
            />
          </div>
        </div>
      </Section>

      {/* §7 */}
      <Section n={7} title="Issues / Defects Log">
        <CheckRow
          checked={noIssues}
          label="No issues to report"
          onToggle={() => {
            setNoIssues((v) => !v);
          }}
        />
        {!noIssues && (
          <div style={{ marginTop: 8 }}>
            {issues.map((line, i) => (
              <div key={i} style={{ display: "grid", gridTemplateColumns: "1fr auto", gap: 10, alignItems: "end", marginBottom: 8 }}>
                <TextField
                  label={`Issue ${i + 1}`}
                  value={line}
                  onChange={(v) => setIssues((cur) => cur.map((x, y) => (y === i ? v : x)))}
                />
                <div style={{ paddingBottom: 6 }}>
                  <RemoveButton onClick={() => setIssues((cur) => cur.filter((_, x) => x !== i))} />
                </div>
              </div>
            ))}
            <ActionButton onClick={() => setIssues((cur) => [...cur, ""])}>+ ADD ISSUE</ActionButton>
          </div>
        )}
        {noIssues && issues.length > 0 && (
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: statusMeta("soon").t, marginTop: 8, fontWeight: 600 }}>
            ◐ &quot;No issues&quot; is checked — the {issues.length} logged issue line(s) will NOT be submitted.
          </div>
        )}
      </Section>

      {/* §8 */}
      <Section n={8} title="Trip Completion Log">
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: 14 }}>
          <TextField label="Departure time" value={departureTime} onChange={setDepartureTime} mono placeholder="HH:MM" />
          <TextField label="Arrival time" value={arrivalTime} onChange={setArrivalTime} mono placeholder="HH:MM" />
          <NumberField label="Odometer end (km)" value={odoEnd} onChange={setOdoEnd} min={0} step={1} />
          <div>
            <FieldLabel>Total km (computed)</FieldLabel>
            <div
              style={{
                height: 40,
                display: "flex",
                alignItems: "center",
                padding: "0 13px",
                borderRadius: 9,
                border: `1px dashed ${colors.borderStrong}`,
                fontFamily: fonts.mono,
                fontSize: 13,
                color: totalKm != null && totalKm < 0 ? statusMeta("over").t : colors.textSecondary,
              }}
            >
              {totalKm != null ? totalKm.toLocaleString("en-CA") : "—"}
            </div>
          </div>
        </div>
        <div style={{ display: "flex", alignItems: "flex-end", gap: 14, marginTop: 14 }}>
          <div>
            <FieldLabel>Fuel added</FieldLabel>
            <div style={{ display: "flex", gap: 7, paddingTop: 6 }}>
              <OptChip active={fuelAdded} label="Yes" onClick={() => setFuelAdded(true)} />
              <OptChip active={!fuelAdded} label="No" onClick={() => setFuelAdded(false)} />
            </div>
          </div>
          {fuelAdded && (
            <div style={{ display: "grid", gridTemplateColumns: "150px 150px", gap: 14 }}>
              <NumberField label="Litres" value={fuelLitres} onChange={setFuelLitres} min={0} step={1} />
              <NumberField label="Fuel cost (CAD)" value={fuelCostCad} onChange={setFuelCostCad} min={0} step={1} />
            </div>
          )}
        </div>
      </Section>

      {/* §9 */}
      <Section n={9} title="Post-Trip Inspection">
        {POST_TRIP_ITEMS.map((item, i) => (
          <CheckRow
            key={item}
            checked={postTrip[i]}
            label={item}
            onToggle={() => setPostTrip((cur) => cur.map((v, x) => (x === i ? !v : v)))}
          />
        ))}
      </Section>

      {/* §10 */}
      <Section n={10} title="Driver Certification">
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim, marginBottom: 8 }}>
          I certify that (check each attestation exactly as signed on the paper form):
        </div>
        {ATTESTATIONS.map((a, i) => (
          <CheckRow
            key={a}
            checked={attest[i]}
            label={`(${i + 1}) ${a}`}
            onToggle={() => setAttest((cur) => cur.map((v, x) => (x === i ? !v : v)))}
          />
        ))}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14, marginTop: 14 }}>
          <TextField
            label="Driver signature name"
            value={signature}
            onChange={setSignature}
            hint={<span style={{ color: colors.textFaint }}>· as signed on the paper form</span>}
          />
          <TextField
            label="Certified date / time"
            value={certifiedAtStr}
            onChange={setCertifiedAtStr}
            mono
            placeholder="YYYY-MM-DD HH:MM"
          />
          <TextField
            label="Transcribed by"
            value={transcribedBy}
            onChange={setTranscribedBy}
            hint={<span style={{ color: colors.textFaint }}>· recorded on the manifest</span>}
          />
        </div>
      </Section>

      {/* bottom actions */}
      <div style={{ display: "flex", gap: 9, justifyContent: "flex-end", marginTop: 4 }}>
        <ActionButton onClick={() => printTripManifest(null)}>PRINT BLANK FORM</ActionButton>
        <ActionButton variant="primary" onClick={submit} style={{ opacity: busy ? 0.6 : 1 }}>
          {busy ? "SAVING…" : "SAVE MANIFEST"}
        </ActionButton>
      </div>
    </div>
  );

  function patchPax(i: number, patch: Partial<PaxRow>) {
    setPassengers((cur) => cur.map((p, x) => (x === i ? { ...p, ...patch } : p)));
  }

  function patchCargo(i: number, patch: Partial<CargoRow>) {
    setCargo((cur) => cur.map((c, x) => (x === i ? { ...c, ...patch } : c)));
  }
}

// ---------------------------------------------------------------------------
// Local primitives (theme-token styled, no ad hoc colours).
// ---------------------------------------------------------------------------

function Section({ n, title, children }: { n: number; title: string; children: React.ReactNode }) {
  return (
    <Panel style={{ marginBottom: 14 }}>
      <SectionLabel>
        {n}. {title}
      </SectionLabel>
      {children}
    </Panel>
  );
}

/** Selection chip — checkbox glyph + label so state never reads by colour alone. */
function OptChip({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
  return (
    <span
      onClick={onClick}
      style={{
        fontFamily: fonts.body,
        fontWeight: active ? 600 : 500,
        fontSize: 12,
        padding: "5px 12px",
        borderRadius: 7,
        background: active ? colors.cardBgActive : colors.cardBg,
        border: `1px solid ${active ? colors.borderActive : colors.border}`,
        color: active ? colors.headingBright : colors.textMuted,
        cursor: "pointer",
        whiteSpace: "nowrap",
        userSelect: "none",
      }}
    >
      {active ? "☒" : "☐"} {label}
    </span>
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
      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.45 }}>
        {label}
      </span>
    </div>
  );
}

function RemoveButton({ onClick }: { onClick: () => void }) {
  return (
    <span
      onClick={onClick}
      title="Remove row"
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
  );
}

/** Text input with datalist suggestions — a select over mock data that still accepts free text. */
function ComboField({
  label,
  value,
  onChange,
  options,
  placeholder,
  mono = false,
  hint,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  options: string[];
  placeholder?: string;
  mono?: boolean;
  hint?: React.ReactNode;
}) {
  const listId = useId();
  return (
    <div>
      <FieldLabel hint={hint}>{label}</FieldLabel>
      <input
        type="text"
        className="nl-input"
        list={listId}
        value={value}
        placeholder={placeholder}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          height: 40,
          boxSizing: "border-box",
          borderRadius: 9,
          background: colors.inputBg,
          border: `1px solid ${colors.borderStrong}`,
          padding: "0 13px",
          fontFamily: mono ? fonts.mono : fonts.body,
          fontSize: mono ? 13 : 13.5,
          color: colors.textPrimary,
          outline: "none",
        }}
      />
      <datalist id={listId}>
        {options.map((o) => (
          <option key={o} value={o} />
        ))}
      </datalist>
    </div>
  );
}
