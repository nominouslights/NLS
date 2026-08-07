"use client";

import { useId } from "react";
import { colors, fonts } from "@/lib/theme";
import type { FarePaymentMethod, ManifestCargo, ManifestPassenger } from "@/lib/api/trips";
import { FieldLabel, NumberField, SelectField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Reusable passenger + cargo row editors for the slim trip manifest. Salvaged
// from the retired Manual Trip Entry form's §5/§6 editors and shared by the
// inline manifest editor (Trips) and the Create Trip wizard's passenger step.
// Passengers reference the trip's route stops for pickup/dropoff.

export const MAX_PASSENGER_ROWS = 8;
export const MAX_CARGO_ROWS = 8;

/** The passenger cap for a trip: the assigned unit's seating capacity when
 *  known, else the classic 8-row default (also the printed form's blank-row
 *  floor). Different units seat different numbers, so the cap tracks the
 *  vehicle rather than a flat constant. */
export function passengerCapFor(seatingCapacity: number | null | undefined): number {
  return seatingCapacity && seatingCapacity > 0 ? seatingCapacity : MAX_PASSENGER_ROWS;
}

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
  /** Fare amount as typed ("" = none). */
  fareAmount: string;
  /** "" = no method picked (allowed while no amount is recorded). */
  fareMethod: FarePaymentMethod | "";
  /** Stamped when a method is first picked; cleared when the method is cleared. */
  farePaidAtUtc: string | null;
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
  fareAmount: "",
  fareMethod: "",
  farePaidAtUtc: null,
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
    fareAmount: p.fareAmountCad != null ? String(p.fareAmountCad) : "",
    fareMethod: p.farePaymentMethod ?? "",
    farePaidAtUtc: p.farePaidAtUtc ?? null,
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

/** Convert editor rows to the wire ManifestPassenger[] (names required). The cap
 *  clamps to the trip's passenger capacity (default 8 when unknown). */
export function paxRowsToWire(
  rows: PaxRow[],
  stops: StopOption[],
  maxRows: number = MAX_PASSENGER_ROWS,
): ManifestPassenger[] {
  const stopAt = (idx: string): StopOption | null => {
    if (idx === "") return null;
    const n = Number(idx);
    return Number.isInteger(n) && n >= 0 && n < stops.length ? stops[n] : null;
  };
  return rows
    .filter((p) => p.name.trim())
    .slice(0, maxRows)
    .map((p) => {
      const pickup = stopAt(p.pickupIdx);
      const dropoff = stopAt(p.dropoffIdx);
      const amount = p.fareAmount.trim() === "" ? null : Number(p.fareAmount);
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
        fareAmountCad: amount !== null && !Number.isNaN(amount) ? amount : null,
        farePaymentMethod: p.fareMethod || null,
        farePaidAtUtc: p.fareMethod ? p.farePaidAtUtc : null,
      };
    });
}

/** UI mirror of the backend's per-passenger fare rules — run before submit so
 *  the save never round-trips just to bounce. Returns the first problem found
 *  (1-based row number included), or null when every row is valid:
 *  amount ≥ 0 with max 2 decimals · amount > 0 requires a method ·
 *  Cash/Online requires amount > 0 · Waived requires amount 0 or empty. */
export function paxFareValidationError(rows: PaxRow[]): string | null {
  const withNames = rows.filter((p) => p.name.trim());
  for (const [i, p] of withNames.entries()) {
    const row = `Passenger ${i + 1}`;
    const raw = p.fareAmount.trim();
    const amount = raw === "" ? null : Number(raw);
    if (amount !== null) {
      if (Number.isNaN(amount) || amount < 0) return `${row}: fare must be zero or more.`;
      if (Math.round(amount * 100) !== amount * 100) return `${row}: fare can have at most 2 decimals.`;
      if (amount > 0 && !p.fareMethod) return `${row}: a fare amount needs a payment method (Cash / Online).`;
    }
    if ((p.fareMethod === "Cash" || p.fareMethod === "Online") && !(amount !== null && amount > 0)) {
      return `${row}: ${p.fareMethod} fares need an amount greater than zero.`;
    }
    if (p.fareMethod === "Waived" && amount !== null && amount !== 0) {
      return `${row}: a waived fare must have amount 0 (or none).`;
    }
  }
  return null;
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
  maxRows = MAX_PASSENGER_ROWS,
  readOnly = false,
}: {
  rows: PaxRow[];
  stops: StopOption[];
  onChange: (rows: PaxRow[]) => void;
  /** Passenger cap — the assigned unit's seat capacity (default 8). */
  maxRows?: number;
  /** Display-only (e.g. a completed trip) — inputs disabled, no add/remove. */
  readOnly?: boolean;
}) {
  const stopOptions = [
    { value: "", label: "— none —" },
    ...stops.map((s, i) => ({ value: String(i), label: s.name })),
  ];
  const patch = (i: number, p: Partial<PaxRow>) =>
    onChange(rows.map((r, x) => (x === i ? { ...r, ...p } : r)));
  const paxCount = rows.filter((p) => p.name.trim()).length;

  /** Pick / clear a fare payment method: stamps farePaidAtUtc the first time a
   *  method is picked, clears it when the method is cleared; Waived forces the
   *  amount to 0 (the amount input disables). */
  const pickFareMethod = (i: number, method: FarePaymentMethod) => {
    const row = rows[i];
    if (row.fareMethod === method) {
      patch(i, { fareMethod: "", farePaidAtUtc: null });
      return;
    }
    patch(i, {
      fareMethod: method,
      farePaidAtUtc: row.farePaidAtUtc ?? new Date().toISOString(),
      ...(method === "Waived" ? { fareAmount: "0" } : {}),
    });
  };

  // Fare rollup for the count line — recorded amounts only, not a settled figure.
  const named = rows.filter((p) => p.name.trim());
  const paidRows = named.filter((p) => p.fareMethod === "Cash" || p.fareMethod === "Online");
  const waivedCount = named.filter((p) => p.fareMethod === "Waived").length;
  const collected = paidRows.reduce((sum, p) => sum + (Number(p.fareAmount) || 0), 0);

  return (
    <div>
      {rows.map((p, i) => (
        <div
          key={i}
          style={{ border: `1px solid ${colors.borderSubtle}`, borderRadius: 9, padding: "11px 12px", marginBottom: 8 }}
        >
          <div style={{ display: "grid", gridTemplateColumns: "24px 1.4fr 1.2fr", gap: 10, alignItems: "end" }}>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textDim, paddingBottom: 12 }}>{i + 1}</div>
            <TextField label="Passenger name" value={p.name} onChange={(v) => patch(i, { name: v })} disabled={readOnly} />
            <TextField label="Email / phone" value={p.contact} onChange={(v) => patch(i, { contact: v })} disabled={readOnly} />
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto auto", gap: 10, marginTop: 9, alignItems: "end" }}>
            <SelectField
              label="Pickup stop"
              value={p.pickupIdx}
              onChange={(v) => patch(i, { pickupIdx: v })}
              options={stopOptions}
              disabled={readOnly}
            />
            <SelectField
              label="Drop-off stop"
              value={p.dropoffIdx}
              onChange={(v) => patch(i, { dropoffIdx: v })}
              options={stopOptions}
              disabled={readOnly}
            />
            <div style={{ display: "flex", gap: 6, paddingBottom: 6 }}>
              <OptChip active={p.idVerified} label="ID verified" onClick={() => patch(i, { idVerified: !p.idVerified })} disabled={readOnly} />
              <OptChip active={p.boardedOn} label="On" onClick={() => patch(i, { boardedOn: !p.boardedOn })} disabled={readOnly} />
              <OptChip active={p.boardedOff} label="Off" onClick={() => patch(i, { boardedOff: !p.boardedOff })} disabled={readOnly} />
            </div>
            {!readOnly && (
              <div style={{ paddingBottom: 6 }}>
                <RemoveButton onClick={() => onChange(rows.filter((_, x) => x !== i))} />
              </div>
            )}
          </div>
          {/* fare — recorded just after the run; Waived pins the amount at 0 */}
          <div style={{ display: "grid", gridTemplateColumns: "110px auto 1fr", gap: 10, marginTop: 9, alignItems: "end" }}>
            <NumberField
              label="Fare (CAD)"
              value={p.fareAmount}
              onChange={(v) => patch(i, { fareAmount: v })}
              min={0}
              step={0.01}
              disabled={readOnly || p.fareMethod === "Waived"}
            />
            <div style={{ display: "flex", gap: 6, paddingBottom: 6 }}>
              {(["Cash", "Online", "Waived"] as FarePaymentMethod[]).map((m) => (
                <OptChip
                  key={m}
                  active={p.fareMethod === m}
                  label={m.toUpperCase()}
                  onClick={() => pickFareMethod(i, m)}
                  disabled={readOnly}
                />
              ))}
            </div>
            {p.farePaidAtUtc && (
              <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim, paddingBottom: 12, textAlign: "right" }}>
                recorded {new Date(p.farePaidAtUtc).toLocaleString("en-CA", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", hour12: false })}
              </div>
            )}
          </div>
        </div>
      ))}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 4, flexWrap: "wrap" }}>
        {!readOnly && rows.length < maxRows && (
          <ActionButton onClick={() => onChange([...rows, emptyPax()])}>+ ADD PASSENGER</ActionButton>
        )}
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          Passengers: <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>{paxCount}</span> (max{" "}
          {maxRows})
        </span>
        {(paidRows.length > 0 || waivedCount > 0) && (
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            · Fares collected{" "}
            <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>${collected.toFixed(2)}</span> ·{" "}
            {paidRows.length} paid · {waivedCount} waived{" "}
            <span style={{ color: colors.textFaint, fontSize: 11 }}>(not yet reconciled to QuickBooks)</span>
          </span>
        )}
      </div>
    </div>
  );
}

export function CargoRowsEditor({
  rows,
  onChange,
  readOnly = false,
}: {
  rows: CargoRow[];
  onChange: (rows: CargoRow[]) => void;
  /** Display-only (e.g. a completed trip) — inputs disabled, no add/remove. */
  readOnly?: boolean;
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
            <TextField label="Description" value={c.description} onChange={(v) => patch(i, { description: v })} disabled={readOnly} />
            <TextField label="Owner / recipient" value={c.ownerRecipient} onChange={(v) => patch(i, { ownerRecipient: v })} disabled={readOnly} />
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr auto auto", gap: 10, marginTop: 9, alignItems: "end" }}>
            <NumberField label="Weight (kg)" value={c.weightKg} onChange={(v) => patch(i, { weightKg: v })} min={0} step={1} disabled={readOnly} />
            <NumberField label="Charge (CAD)" value={c.chargeCad} onChange={(v) => patch(i, { chargeCad: v })} min={0} step={5} disabled={readOnly} />
            <div style={{ display: "flex", gap: 6, paddingBottom: 6 }}>
              <OptChip active={c.hazmat} label="Hazmat" onClick={() => patch(i, { hazmat: !c.hazmat })} disabled={readOnly} />
              <OptChip active={c.secured} label="Secured" onClick={() => patch(i, { secured: !c.secured })} disabled={readOnly} />
            </div>
            {!readOnly && (
              <div style={{ paddingBottom: 6 }}>
                <RemoveButton onClick={() => onChange(rows.filter((_, x) => x !== i))} />
              </div>
            )}
          </div>
        </div>
      ))}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 4 }}>
        {!readOnly && rows.length < MAX_CARGO_ROWS && (
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

export function OptChip({
  active,
  label,
  onClick,
  disabled = false,
}: {
  active: boolean;
  label: string;
  onClick: () => void;
  /** Display-only — keeps the ☒/☐ glyph + label but drops interactivity. */
  disabled?: boolean;
}) {
  return (
    <span
      onClick={disabled ? undefined : onClick}
      aria-disabled={disabled || undefined}
      style={{
        fontFamily: fonts.body,
        fontWeight: active ? 600 : 500,
        fontSize: 12,
        padding: "5px 12px",
        borderRadius: 7,
        background: active ? colors.cardBgActive : colors.cardBg,
        border: `1px solid ${active ? colors.borderActive : colors.border}`,
        color: active ? colors.headingBright : colors.textMuted,
        cursor: disabled ? "default" : "pointer",
        opacity: disabled ? 0.7 : 1,
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
