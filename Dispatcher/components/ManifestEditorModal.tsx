"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import {
  ApiError,
  createTripManifest,
  updateTripManifest,
  type ManifestCargoSecured,
  type ManifestDirection,
  type TripManifest,
  type TripManifestInput,
} from "@/lib/api";
import { stopNames, type TripRecord } from "@/lib/api/trips";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { SectionLabel } from "@/components/ui/Panel";
import { FieldLabel } from "@/components/ui/Field";
import {
  CargoRowsEditor,
  OptChip,
  PassengerRowsEditor,
  cargoRowsFromManifest,
  cargoRowsToWire,
  emptyCargo,
  emptyPax,
  passengerCapFor,
  paxRowsFromManifest,
  paxRowsToWire,
  type CargoRow,
  type PaxRow,
  type StopOption,
} from "@/components/manifest/manifestRows";
import PassengerCsvImport from "@/components/manifest/PassengerCsvImport";

// Inline manifest editor — the slim passenger + cargo manifest for a trip.
// Create or edit, always source "Dispatcher" (with the dispatcher label as
// enteredBy). Editing never changes trip status; a trip needs ≥1 passenger here
// before it can start. Pickup/dropoff pickers draw from the trip's route stops.

/** Trip route stops → picker options (free-text stops keep a null id). */
function stopOptionsFor(trip: TripRecord): StopOption[] {
  return [...trip.stops]
    .sort((a, b) => a.order - b.order)
    .map((s) => ({ stopId: s.stopId ?? null, name: s.name }));
}

export default function ManifestEditorModal({
  trip,
  existing,
  enteredBy,
  onClose,
  onSaved,
  readOnly = false,
}: {
  trip: TripRecord;
  existing: TripManifest | null;
  enteredBy: string;
  onClose: () => void;
  /** Called with the manifest id once the create/update has been accepted. */
  onSaved: (manifestId: string) => Promise<void>;
  /** View-only — used for completed/cancelled trips whose manifest is locked. */
  readOnly?: boolean;
}) {
  const stops = stopOptionsFor(trip);

  const [passengers, setPassengers] = useState<PaxRow[]>(
    existing ? paxRowsFromManifest(existing.passengers, stops) : [emptyPax()],
  );
  const [cargo, setCargo] = useState<CargoRow[]>(
    existing && existing.cargo.length > 0 ? cargoRowsFromManifest(existing.cargo) : [emptyCargo()],
  );
  const [allSeatbeltsVerified, setAllSeatbeltsVerified] = useState(existing?.allSeatbeltsVerified ?? false);
  const [allCargoSecured, setAllCargoSecured] = useState<ManifestCargoSecured | null>(
    existing?.allCargoSecured ?? null,
  );
  const [direction, setDirection] = useState<ManifestDirection | null>(existing?.direction ?? trip.direction ?? null);

  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Passenger cap tracks the assigned unit's seat capacity (snapshotted onto the
  // trip), falling back to the default 8 when the trip carries no capacity.
  const maxPax = passengerCapFor(trip.seatsCapacity);
  const namedPax = passengers.filter((p) => p.name.trim()).length;
  const contentPax = passengers.filter((p) => p.name.trim() || p.contact.trim()).length;

  // Merge imported rows onto the existing content rows (clamped to the cap) and
  // adopt the sheet's direction when the manifest doesn't already have one.
  function applyImport(rows: PaxRow[], detected: { direction: ManifestDirection | null }) {
    setPassengers((prev) => {
      const kept = prev.filter((p) => p.name.trim() || p.contact.trim());
      const merged = [...kept, ...rows].slice(0, maxPax);
      return merged.length > 0 ? merged : [emptyPax()];
    });
    if (detected.direction && !direction) setDirection(detected.direction);
  }

  async function submit() {
    if (busy || readOnly) return;
    if (namedPax === 0) {
      setError("Add at least one named passenger — a trip cannot start without a passenger manifest.");
      return;
    }
    const input: TripManifestInput = {
      tripDate: trip.serviceDate,
      tripNumber: trip.tripNumber,
      route: stopNames(trip).join(" → "),
      direction,
      client: trip.clientName,
      passengers: paxRowsToWire(passengers, stops, maxPax),
      allSeatbeltsVerified,
      cargo: cargoRowsToWire(cargo),
      allCargoSecured,
      source: "Dispatcher",
      enteredBy,
    };

    setBusy(true);
    setError(null);
    try {
      let manifestId: string;
      if (existing) {
        await updateTripManifest(existing.id, input);
        manifestId = existing.id;
      } else {
        const res = await createTripManifest(input);
        manifestId = res.id;
      }
      await onSaved(manifestId);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the manifest — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · Passenger & cargo manifest`}
      title={readOnly ? "View Manifest" : existing ? "Edit Manifest" : "Add Manifest"}
      onClose={onClose}
      error={error}
      maxWidth={880}
      footer={
        readOnly ? (
          <>
            <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
              {namedPax} passenger{namedPax === 1 ? "" : "s"} · read-only ({trip.status.toLowerCase()})
            </span>
            <ActionButton onClick={onClose}>CLOSE</ActionButton>
          </>
        ) : (
          <>
            <span style={{ marginRight: "auto", fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
              {namedPax} passenger{namedPax === 1 ? "" : "s"} · source{" "}
              <span style={{ fontWeight: 600, color: colors.textSecondary }}>Dispatcher</span>
            </span>
            <ActionButton onClick={onClose}>CANCEL</ActionButton>
            <ActionButton variant="primary" onClick={submit} disabled={busy}>
              {busy ? "SAVING…" : existing ? "SAVE MANIFEST" : "CREATE MANIFEST"}
            </ActionButton>
          </>
        )
      }
    >
      {/* trip context */}
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
        {stopNames(trip).join(" → ")}
        {trip.clientName ? ` · ${trip.clientName}` : ""}. Passengers pick up and drop off at the trip&rsquo;s route
        stops. {readOnly
          ? `This trip is ${trip.status.toLowerCase()} — the manifest is read-only.`
          : "Editing the manifest does not change trip status."}
      </div>

      <div style={{ marginBottom: 16 }}>
        <FieldLabel>Direction</FieldLabel>
        <div style={{ display: "flex", gap: 7, paddingTop: 4 }}>
          {(["Inbound", "Outbound"] as ManifestDirection[]).map((d) => (
            <OptChip
              key={d}
              active={direction === d}
              label={d}
              onClick={() => setDirection((c) => (c === d ? null : d))}
              disabled={readOnly}
            />
          ))}
        </div>
      </div>

      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
        <SectionLabel>Passengers</SectionLabel>
        {!readOnly && (
          <PassengerCsvImport
            stops={stops}
            capacity={trip.seatsCapacity}
            existingCount={contentPax}
            trip={{ clientName: trip.clientName, serviceDate: trip.serviceDate, direction }}
            onApply={applyImport}
          />
        )}
      </div>
      <div style={{ marginBottom: 8 }}>
        <PassengerRowsEditor rows={passengers} stops={stops} onChange={setPassengers} maxRows={maxPax} readOnly={readOnly} />
      </div>
      <div style={{ display: "flex", justifyContent: "flex-end", marginBottom: 20 }}>
        <OptChip
          active={allSeatbeltsVerified}
          label="All seatbelts verified"
          onClick={() => setAllSeatbeltsVerified((v) => !v)}
          disabled={readOnly}
        />
      </div>

      <SectionLabel>Cargo</SectionLabel>
      <div style={{ marginBottom: 8 }}>
        <CargoRowsEditor rows={cargo} onChange={setCargo} readOnly={readOnly} />
      </div>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "flex-end", gap: 8 }}>
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textLabel }}>All cargo secured:</span>
        <OptChip
          active={allCargoSecured === "Yes"}
          label="Yes"
          onClick={() => setAllCargoSecured((c) => (c === "Yes" ? null : "Yes"))}
          disabled={readOnly}
        />
        <OptChip
          active={allCargoSecured === "NotApplicable"}
          label="N/A"
          onClick={() => setAllCargoSecured((c) => (c === "NotApplicable" ? null : "NotApplicable"))}
          disabled={readOnly}
        />
      </div>
    </ModalShell>
  );
}
