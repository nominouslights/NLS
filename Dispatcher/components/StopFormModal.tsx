"use client";

import { useEffect, useRef, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import { STOP_TYPES, stopTypeLabel, type StopInput, type StopRecord } from "@/lib/api/stops";
import { loadGoogleMaps, parsePlace, type GoogleAutocomplete, type ParsedPlace } from "@/lib/googleMaps";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { ModalShell } from "@/components/ui/ModalShell";
import { FieldLabel, NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";

// Stop form modal — the reusable catalog stop editor (POST /api/trips/stops ·
// PUT /api/trips/stops/{id}). Extracted from the Stops screen so it can also be
// opened inline from the route-building form, letting a dispatcher create a
// catalog stop without leaving the route. Behaviour, validation and props are
// identical to the original in-screen version — the owning screen still supplies
// the create/update work via `onSaved`.

// Canadian provinces & territories. Values are the two-letter codes Google
// Places returns for administrative_area_level_1 (short_name), so an
// autocomplete-filled province maps straight onto a dropdown option.
const PROVINCES: { value: string; label: string }[] = [
  { value: "AB", label: "Alberta" },
  { value: "BC", label: "British Columbia" },
  { value: "MB", label: "Manitoba" },
  { value: "NB", label: "New Brunswick" },
  { value: "NL", label: "Newfoundland and Labrador" },
  { value: "NT", label: "Northwest Territories" },
  { value: "NS", label: "Nova Scotia" },
  { value: "NU", label: "Nunavut" },
  { value: "ON", label: "Ontario" },
  { value: "PE", label: "Prince Edward Island" },
  { value: "QC", label: "Quebec" },
  { value: "SK", label: "Saskatchewan" },
  { value: "YT", label: "Yukon" },
];

// ---------------------------------------------------------------------------
// Address autocomplete field — the one external-origin browser call in the app.
// Populates the structured address + coords on selection; fields stay editable,
// and a failure to load degrades gracefully to manual entry.
// ---------------------------------------------------------------------------

function AddressAutocompleteField({ onPlace }: { onPlace: (parsed: ParsedPlace) => void }) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const onPlaceRef = useRef(onPlace);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  const [errMsg, setErrMsg] = useState<string | null>(null);

  useEffect(() => {
    onPlaceRef.current = onPlace;
  }, [onPlace]);

  useEffect(() => {
    let cancelled = false;
    let autocomplete: GoogleAutocomplete | null = null;
    loadGoogleMaps().then(
      (g) => {
        if (cancelled || !inputRef.current) return;
        autocomplete = new g.maps.places.Autocomplete(inputRef.current, {
          fields: ["address_components", "geometry", "formatted_address", "name"],
        });
        autocomplete.addListener("place_changed", () => {
          if (!autocomplete) return;
          const parsed = parsePlace(autocomplete.getPlace());
          if (parsed) onPlaceRef.current(parsed);
        });
        setStatus("ready");
      },
      (e) => {
        if (cancelled) return;
        setStatus("error");
        setErrMsg(e instanceof Error ? e.message : "Google Places is unavailable.");
      },
    );
    return () => {
      cancelled = true;
      if (autocomplete && window.google) {
        window.google.maps.event.clearInstanceListeners(autocomplete);
      }
    };
  }, []);

  return (
    <div>
      <FieldLabel hint={<span style={{ color: colors.textFaint }}>· fills the address &amp; coordinates below — all stay editable</span>}>
        Search address (Google Places)
      </FieldLabel>
      <input
        ref={inputRef}
        className="nl-input"
        placeholder={status === "error" ? "Autocomplete off — enter the address manually below" : "Start typing an address…"}
        // Prevent an Enter keypress on the suggestion list from submitting the form.
        onKeyDown={(e) => {
          if (e.key === "Enter") e.preventDefault();
        }}
        disabled={status === "error"}
        style={{
          width: "100%",
          height: 40,
          boxSizing: "border-box",
          borderRadius: 9,
          background: colors.inputBg,
          border: `1px solid ${colors.borderStrong}`,
          padding: "0 13px",
          fontFamily: fonts.body,
          fontSize: 13.5,
          color: colors.textPrimary,
          outline: "none",
          opacity: status === "error" ? 0.55 : 1,
        }}
      />
      <div style={{ marginTop: 7 }}>
        {status === "loading" && (
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>Loading Google Places…</span>
        )}
        {status === "ready" && <StatusChip kind="ontime" label="Autocomplete ready" />}
        {status === "error" && <StatusChip kind="over" label={`Autocomplete unavailable — ${errMsg}`} />}
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Stop form modal — POST /api/trips/stops · PUT /api/trips/stops/{id}
// ---------------------------------------------------------------------------

export function StopFormModal({
  existing,
  onClose,
  onSaved,
}: {
  existing: StopRecord | null;
  onClose: () => void;
  onSaved: (input: StopInput, active: boolean, existingId: string | null) => Promise<void>;
}) {
  const editing = existing !== null;
  const [name, setName] = useState(existing?.name ?? "");
  const [stopType, setStopType] = useState(existing?.stopType ?? "");
  const [street, setStreet] = useState(existing?.street ?? "");
  const [city, setCity] = useState(existing?.city ?? "");
  const [province, setProvince] = useState(existing?.province ?? "");
  const [postalCode, setPostalCode] = useState(existing?.postalCode ?? "");
  const [country, setCountry] = useState(existing?.country ?? "Canada");
  const [latitude, setLatitude] = useState(existing ? String(existing.latitude) : "");
  const [longitude, setLongitude] = useState(existing ? String(existing.longitude) : "");
  const [notes, setNotes] = useState(existing?.notes ?? "");
  const [active, setActive] = useState(existing?.active ?? true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function applyPlace(p: ParsedPlace) {
    if (p.street) setStreet(p.street);
    if (p.city) setCity(p.city);
    if (p.province) setProvince(p.province);
    if (p.postalCode) setPostalCode(p.postalCode);
    if (p.country) setCountry(p.country);
    if (p.latitude != null) setLatitude(String(p.latitude));
    if (p.longitude != null) setLongitude(String(p.longitude));
    // Default the stop name to the address when the dispatcher hasn't typed one.
    if (!name.trim() && p.street) setName(p.street);
  }

  async function submit() {
    if (busy) return;
    if (!name.trim()) return setError("Enter the stop name.");
    if (!city.trim()) return setError("City is required.");
    if (!province.trim()) return setError("Province is required.");
    if (!country.trim()) return setError("Country is required.");
    const lat = Number(latitude);
    if (latitude.trim() === "" || Number.isNaN(lat) || lat < -90 || lat > 90)
      return setError("Latitude must be a number between -90 and 90 (pick an address to fill it).");
    const lng = Number(longitude);
    if (longitude.trim() === "" || Number.isNaN(lng) || lng < -180 || lng > 180)
      return setError("Longitude must be a number between -180 and 180 (pick an address to fill it).");

    setBusy(true);
    setError(null);
    try {
      await onSaved(
        {
          name: name.trim(),
          stopType: stopType || null,
          street: street.trim() || null,
          city: city.trim(),
          province: province.trim(),
          postalCode: postalCode.trim() || null,
          country: country.trim(),
          latitude: lat,
          longitude: lng,
          notes: notes.trim() || null,
        },
        active,
        existing?.id ?? null,
      );
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the stop — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Operations · Stop catalog"
      title={editing ? "Edit Stop" : "New Stop"}
      onClose={onClose}
      error={error}
      maxWidth={720}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : editing ? "SAVE STOP" : "CREATE STOP"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Stop name" value={name} onChange={setName} placeholder="Thompson Terminal" />
        <SelectField
          label="Stop type (optional)"
          value={stopType}
          onChange={setStopType}
          options={[
            { value: "", label: "— unspecified —" },
            ...STOP_TYPES.map((t) => ({ value: t, label: stopTypeLabel(t) })),
          ]}
        />
      </div>

      <div style={{ marginTop: 16 }}>
        <AddressAutocompleteField onPlace={applyPlace} />
      </div>

      <div style={{ marginTop: 16, display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <div style={{ gridColumn: "1 / -1" }}>
          <TextField label="Street (optional)" value={street} onChange={setStreet} placeholder="123 Mystery Lake Rd" />
        </div>
        <TextField label="City" value={city} onChange={setCity} placeholder="Thompson" />
        <SelectField
          label="Province / territory"
          value={province}
          onChange={setProvince}
          options={[{ value: "", label: "— select —" }, ...PROVINCES]}
        />
        <TextField label="Postal code (optional)" value={postalCode} onChange={setPostalCode} mono placeholder="R8N 0N2" />
        <TextField label="Country" value={country} onChange={setCountry} placeholder="Canada" />
        <NumberField label="Latitude" value={latitude} onChange={setLatitude} step={0.000001} placeholder="55.743" />
        <NumberField label="Longitude" value={longitude} onChange={setLongitude} step={0.000001} placeholder="-97.855" />
      </div>

      <div style={{ marginTop: 16 }}>
        <TextAreaField
          label="Notes (optional)"
          value={notes}
          onChange={setNotes}
          rows={2}
          placeholder="Gate code, curfew, meeting-point detail…"
        />
      </div>

      {editing && (
        <div style={{ marginTop: 16, display: "flex", alignItems: "center", gap: 10 }}>
          <ActionButton variant={active ? "secondary" : "success"} onClick={() => setActive((v) => !v)}>
            {active ? "MARK INACTIVE" : "MARK ACTIVE"}
          </ActionButton>
          {active ? (
            <StatusChip kind="ontime" label="Active — available for routes" />
          ) : (
            <StatusChip kind="off" label="Inactive — hidden from route pickers" />
          )}
        </div>
      )}
    </ModalShell>
  );
}
