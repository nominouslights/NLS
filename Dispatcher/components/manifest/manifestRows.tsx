"use client";

import { useId } from "react";
import { colors, fonts } from "@/lib/theme";
import type { ManifestCargo, ManifestPassenger } from "@/lib/api/trips";
import { FieldLabel, NumberField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Reusable passenger + cargo row editors for the slim trip manifest. Salvaged
// from the retired Manual Trip Entry form's §5/§6 editors and shared by the
// inline manifest editor (Trips) and the Create Trip wizard's passenger step.
// Passengers reference the trip's route stops for pickup/dropoff.

export const MAX_PASSENGER_ROWS = 8;
export const MAX_CARGO_ROWS = 8;

/** A pickable stop on the trip's route — id may be null for free-text stops. */
export interface StopOption {
  stopId: string | null;
  name: string;
}

export interface PaxRow {
  name: string;
  contact: string;
  /** Index into the stop options ("" = none). */
  pickupIdx: string;
  dropoffIdx: string;
  idVerified: boolean;
  boardedOn: boolean;
  boardedOff: boolean;
}

export interface CargoRow {
  description: string;
  ownerRecipient: string;
  weightKg: string;
  chargeCad: string;
  hazmat: boolean;
  secured: boolean;
}

export const emptyPax = (): PaxRow => ({
  name: "",
  contact: "",
  pickupIdx: "",
  dropoffIdx: "",
  idVerified: false,
  boardedOn: false,
  boardedOff: false,
});

export const emptyCargo = (): CargoRow => ({
  description: "",
  ownerRecipient: "",
  weightKg: "",
  chargeCad: "",
  hazmat: false,
  secured: true,
});

/** Build editor rows from an existing manifest's passengers, matching pickup/
 *  dropoff back to the current stop options by id (then by name). */
export function paxRowsFromManifest(passengers: ManifestPassenger[], stops: StopOption[]): PaxRow[] {
  const idxFor = (id: string | null | undefined, name: string | null | undefined): string => {
    if (id) {
      const byId = stops.findIndex((s) => s.stopId === id);
      if (byId >= 0) return String(byId);
    }
    if (name) {
      const byName = stops.findIndex((s) => s.name === name);
      if (byName >= 0) return String(byName);
    }
    return "";
  };
  return passengers.map((p) => ({
    name: p.name,
    contact: p.contact ?? "",
    pickupIdx: idxFor(p.pickupStopId, p.pickupStopName),
    dropoffIdx: idxFor(p.dropoffStopId, p.dropoffStopName),
    idVerified: p.idVerified,
    boardedOn: p.boardedOn,
    boardedOff: p.boardedOff,
  }));
}

export function cargoRowsFromManifest(cargo: ManifestCargo[]): CargoRow[] {
  return cargo.map((c) => ({
    description: c.description,
    ownerRecipient: c.ownerRecipient ?? "",
    weightKg: c.weightKg != null ? String(c.weightKg) : "",
    chargeCad: c.chargeCad != null ? String(c.chargeCad) : "",
    hazmat: c.hazmat,
    secured: c.secured,
  }));
}

/** Convert editor rows to the wire ManifestPassenger[] (names required). */
export function paxRowsToWire(rows: PaxRow[], stops: StopOption[]): ManifestPassenger[] {
  const stopAt = (idx: string): StopOption | null => {
    if (idx === "") return null;
    const n = Number(idx);
    return Number.isInteger(n) && n >= 0 && n < stops.length ? stops[n] : null;
  };
  return rows
    .filter((p) => p.name.trim())
    .slice(0, MAX_PASSENGER_ROWS)
    .map((p) => {
      const pickup = stopAt(p.pickupIdx);
      const dropoff = stopAt(p.dropoffIdx);
      return {
        name: p.name.trim(),
        contact: p.contact.trim() || null,
        pickupStopId: pickup?.stopId ?? null,
        pickupStopName: pickup?.name ?? null,
        dropoffStopId: dropoff?.stopId ?? null,
        dropoffStopName: dropoff?.name ?? null,
        idVerified: p.idVerified,
        boardedOn: p.boardedOn,
        boardedOff: p.boardedOff,
      };
    });
}

export function cargoRowsToWire(rows: CargoRow[]): ManifestCargo[] {
  return rows
    .filter((c) => c.description.trim())
    .slice(0, MAX_CARGO_ROWS)
    .map((c) => ({
      description: c.description.trim(),
      ownerRecipient: c.ownerRecipient.trim() || null,
      weightKg: parseFloat(c.weightKg) || null,
      chargeCad: parseFloat(c.chargeCad) || null,
      hazmat: c.hazmat,
      secured: c.secured,
    }));
}

// ---------------------------------------------------------------------------
// Editors
// ---------------------------------------------------------------------------

export function PassengerRowsEditor({
  rows,
  stops,
  onChange,
}: {
  rows: PaxRow[];
  stops: StopOption[];
  onChange: (rows: PaxRow[]) => void;
}) {
  const stopOptions = [
    { value: "", label: "— none —" },
    ...stops.map((s, i) => ({ value: String(i), label: s.name })),
  ];
  const patch = (i: number, p: Partial<PaxRow>) =>
    onChange(rows.map((r, x) => (x === i ? { ...r, ...p } : r)));
  const paxCount = rows.filter((p) => p.name.trim()).length;

  return (
    <div>
      {rows.map((p, i) => (
        <div
          key={i}
          style={{ border: `1px solid ${colors.borderSubtle}`, borderRadius: 9, padding: "11px 12px", marginBottom: 8 }}
        >
          <div style={{ display: "grid", gridTemplateColumns: "24px 1.4fr 1.2fr", gap: 10, alignItems: "end" }}>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textDim, paddingBottom: 12 }}>{i + 1}</div>
            <TextField label="Passenger name" value={p.name} onChange={(v) => patch(i, { name: v })} />
            <TextField label="Email / phone" value={p.contact} onChange={(v) => patch(i, { contact: v })} />
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto auto", gap: 10, marginTop: 9, alignItems: "end" }}>
            <SelectField
              label="Pickup stop"
              value={p.pickupIdx}
              onChange={(v) => patch(i, { pickupIdx: v })}
              options={stopOptions}
            />
            <SelectField
              label="Drop-off stop"
              value={p.dropoffIdx}
              onChange={(v) => patch(i, { dropoffIdx: v })}
              options={stopOptions}
            />
            <div style={{ display: "flex", gap: 6, paddingBottom: 6 }}>
              <OptChip active={p.idVerified} label="ID verified" onClick={() => patch(i, { idVerified: !p.idVerified })} />
              <OptChip active={p.boardedOn} label="On" onClick={() => patch(i, { boardedOn: !p.boardedOn })} />
              <OptChip active={p.boardedOff} label="Off" onClick={() => patch(i, { boardedOff: !p.boardedOff })} />
            </div>
            <div style={{ paddingBottom: 6 }}>
              <RemoveButton onClick={() => onChange(rows.filter((_, x) => x !== i))} />
            </div>
          </div>
        </div>
      ))}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 4 }}>
        {rows.length < MAX_PASSENGER_ROWS && (
          <ActionButton onClick={() => onChange([...rows, emptyPax()])}>+ ADD PASSENGER</ActionButton>
        )}
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          Passengers: <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>{paxCount}</span> (max{" "}
          {MAX_PASSENGER_ROWS})
        </span>
      </div>
    </div>
  );
}

export function CargoRowsEditor({
  rows,
  onChange,
}: {
  rows: CargoRow[];
  onChange: (rows: CargoRow[]) => void;
}) {
  const patch = (i: number, p: Partial<CargoRow>) =>
    onChange(rows.map((r, x) => (x === i ? { ...r, ...p } : r)));
  const total = rows.reduce((sum, c) => sum + (parseFloat(c.chargeCad) || 0), 0);

  return (
    <div>
      {rows.map((c, i) => (
        <div
          key={i}
          style={{ border: `1px solid ${colors.borderSubtle}`, borderRadius: 9, padding: "11px 12px", marginBottom: 8 }}
        >
          <div style={{ display: "grid", gridTemplateColumns: "24px 1.4fr 1fr", gap: 10, alignItems: "end" }}>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textDim, paddingBottom: 12 }}>{i + 1}</div>
            <TextField label="Description" value={c.description} onChange={(v) => patch(i, { description: v })} />
            <TextField label="Owner / recipient" value={c.ownerRecipient} onChange={(v) => patch(i, { ownerRecipient: v })} />
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto auto", gap: 10, marginTop: 9, alignItems: "end" }}>
            <NumberField label="Weight (kg)" value={c.weightKg} onChange={(v) => patch(i, { weightKg: v })} min={0} step={1} />
            <NumberField label="Charge (CAD)" value={c.chargeCad} onChange={(v) => patch(i, { chargeCad: v })} min={0} step={5} />
            <div style={{ display: "flex", gap: 6, paddingBottom: 6 }}>
              <OptChip active={c.hazmat} label="Hazmat" onClick={() => patch(i, { hazmat: !c.hazmat })} />
              <OptChip active={c.secured} label="Secured" onClick={() => patch(i, { secured: !c.secured })} />
            </div>
            <div style={{ paddingBottom: 6 }}>
              <RemoveButton onClick={() => onChange(rows.filter((_, x) => x !== i))} />
            </div>
          </div>
        </div>
      ))}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 4 }}>
        {rows.length < MAX_CARGO_ROWS && (
          <ActionButton onClick={() => onChange([...rows, emptyCargo()])}>+ ADD CARGO</ActionButton>
        )}
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          Total charges: <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>${total.toFixed(2)}</span>
        </span>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Shared primitives (theme-token styled; selection state reads by glyph, not
// colour alone).
// ---------------------------------------------------------------------------

export function OptChip({ active, label, onClick }: { active: boolean; label: string; onClick: () => void }) {
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

export function RemoveButton({ onClick }: { onClick: () => void }) {
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

/** Text input with datalist suggestions — kept for callers that still offer a
 *  combo over mock/reference data while accepting free text. */
export function ComboField({
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
