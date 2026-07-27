"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createStop,
  listStops,
  refetchUntil,
  setStopActive,
  sortStops,
  stopAddressLine,
  STOP_TYPES,
  stopTypeLabel,
  updateStop,
  type StopInput,
  type StopRecord,
} from "@/lib/api/stops";
import { loadGoogleMaps, parsePlace, type GoogleAutocomplete, type ParsedPlace } from "@/lib/googleMaps";
import { PageHeader, Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { ModalShell } from "@/components/ui/ModalShell";
import { FieldLabel, NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";

// Stops — the reusable catalog of rich, geocoded stops (GET/POST/PUT
// /api/trips/stops). Each stop carries a structured address + lat/lng captured
// from Google Places autocomplete; routes are built by selecting stops from
// this catalog, and the coordinates feed the Live Map later. Styled to match
// Routes & Schedules; mutations use the same eventual-consistency refetch.

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

function StopFormModal({
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

// ---------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------

export default function Stops() {
  const [stops, setStops] = useState<StopRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selId, setSelId] = useState<string | null>(null);
  const [modal, setModal] = useState<null | "new" | "edit">(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const rows = await listStops();
      setStops(sortStops(rows));
      setLoadError(null);
    } catch (e) {
      setStops(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load stops.");
    }
  }, []);

  useEffect(() => {
    let active = true;
    listStops().then(
      (rows) => {
        if (active) {
          setStops(sortStops(rows));
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setStops(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load stops.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const selected = selId ? stops?.find((s) => s.id === selId) ?? null : null;
  const effective = selected ?? (selId === null ? stops?.[0] ?? null : null);

  async function saveStop(input: StopInput, active: boolean, existingId: string | null) {
    if (existingId) {
      await updateStop(existingId, input);
      // Active toggles via a separate endpoint (like schedule templates).
      if (active !== (stops?.find((s) => s.id === existingId)?.active ?? true)) {
        await setStopActive(existingId, active);
      }
      const fresh = await refetchUntil(listStops, (rows) => {
        const s = rows.find((x) => x.id === existingId);
        return s !== undefined && s.name === input.name && s.active === active && s.city === input.city;
      });
      setStops(sortStops(fresh));
    } else {
      const newId = await createStop(input);
      const fresh = await refetchUntil(listStops, (rows) => rows.some((x) => x.id === newId));
      setStops(sortStops(fresh));
      setSelId(newId);
    }
  }

  async function toggleActive(s: StopRecord) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await setStopActive(s.id, !s.active);
      const fresh = await refetchUntil(listStops, (rows) => rows.find((x) => x.id === s.id)?.active === !s.active);
      setStops(sortStops(fresh));
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to change the stop — please try again.");
    } finally {
      setBusy(false);
    }
  }

  const frame = (children: React.ReactNode) => (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader
          eyebrow="Operations · Reusable stop catalog with geocoded addresses"
          title="Stops"
          right={
            <ActionButton variant="primary" onClick={() => setModal("new")}>
              + NEW STOP
            </ActionButton>
          }
        />
      </div>
      {children}
      {modal === "new" && <StopFormModal existing={null} onClose={() => setModal(null)} onSaved={saveStop} />}
      {modal === "edit" && effective && <StopFormModal existing={effective} onClose={() => setModal(null)} onSaved={saveStop} />}
    </div>
  );

  if (loadError) {
    return frame(
      <div style={{ padding: "26px", maxWidth: 560 }}>
        <Panel borderColor="rgba(213,94,0,.4)">
          <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
            <StatusChip kind="over" label={`Stops unavailable — ${loadError}`} />
            <ActionButton variant="primary" onClick={load}>
              RETRY
            </ActionButton>
          </div>
        </Panel>
      </div>,
    );
  }

  if (stops === null) {
    return frame(
      <div style={{ padding: "16px 26px" }}>
        {[0, 1, 2, 3].map((i) => (
          <div
            key={i}
            style={{
              height: 58,
              borderRadius: 9,
              border: `1px solid ${colors.borderSubtle}`,
              background: colors.cardBg,
              marginBottom: 6,
              opacity: 0.55 - i * 0.1,
            }}
          />
        ))}
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
          Loading the stop catalog from API…
        </div>
      </div>,
    );
  }

  return frame(
    <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "34% 1fr", borderTop: `1px solid ${colors.border}` }}>
      {/* LEFT — stop list */}
      <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".14em",
            textTransform: "uppercase",
            color: colors.textFaint,
            marginBottom: 10,
          }}
        >
          Stops · {stops.length}
        </div>
        {stops.length === 0 && (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            No stops yet — create the first catalog stop, then build routes from it.
          </div>
        )}
        {stops.map((s) => {
          const active = effective?.id === s.id;
          return (
            <div
              key={s.id}
              onClick={() => setSelId(s.id)}
              style={{
                padding: "12px 14px",
                marginBottom: 5,
                borderRadius: 9,
                border: `1px solid ${active ? colors.borderActive : colors.borderSubtle}`,
                background: active ? colors.cardBgActive : colors.cardBg,
                boxShadow: active ? `inset 3px 0 0 ${colors.blue}, ${colors.shadowCard}` : colors.shadowCard,
                cursor: "pointer",
                opacity: s.active ? 1 : 0.62,
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary, minWidth: 0 }}>
                  {s.name}
                </div>
                {!s.active && <StatusChip kind="off" label="Inactive" />}
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 3 }}>
                {s.city}, {s.province}
                {s.stopType ? ` · ${stopTypeLabel(s.stopType)}` : ""}
              </div>
            </div>
          );
        })}
      </div>

      {/* RIGHT — detail */}
      <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
        {actionError && (
          <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
            <StatusChip kind="over" label={actionError} />
          </Panel>
        )}

        {effective ? (
          <div className="detailfade" key={effective.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1, color: colors.headingBright, margin: "0 0 4px" }}>
                {effective.name}
              </h2>
              {effective.active ? <StatusChip kind="ontime" label="Active" /> : <StatusChip kind="off" label="Inactive" />}
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, margin: "4px 0 16px" }}>
              {stopTypeLabel(effective.stopType)} · {stopAddressLine(effective) || "no address on file"}
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
              <Panel>
                <SectionLabel>Address</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="Street" value={effective.street || "—"} />
                  <DetailRow label="City" value={effective.city} />
                  <DetailRow label="Province / state" value={effective.province} />
                  <DetailRow label="Postal code" value={effective.postalCode || "—"} valueStyle={{ fontFamily: fonts.mono }} />
                  <DetailRow label="Country" value={effective.country} />
                </div>
              </Panel>
              <Panel>
                <SectionLabel>Coordinates &amp; type</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="Latitude" value={String(effective.latitude)} valueStyle={{ fontFamily: fonts.mono }} />
                  <DetailRow label="Longitude" value={String(effective.longitude)} valueStyle={{ fontFamily: fonts.mono }} />
                  <DetailRow label="Stop type" value={stopTypeLabel(effective.stopType)} />
                </div>
              </Panel>
            </div>

            {effective.notes && (
              <Panel style={{ marginBottom: 12 }}>
                <SectionLabel>Notes</SectionLabel>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                  {effective.notes}
                </div>
              </Panel>
            )}

            <div
              style={{
                padding: "12px 15px",
                background: "rgba(31,111,178,.07)",
                border: "1px solid rgba(31,111,178,.25)",
                borderRadius: 10,
                fontFamily: fonts.body,
                fontSize: 12,
                lineHeight: 1.55,
                color: colors.textMuted,
                marginBottom: 16,
              }}
            >
              Stops are shared building blocks — routes reference them and snapshot the name + coordinates. Deactivating a stop hides it
              from route pickers without altering routes already built from it.
            </div>

            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton variant="primary" onClick={() => setModal("edit")}>
                EDIT STOP
              </ActionButton>
              <ActionButton
                variant={effective.active ? "destructive" : "success"}
                onClick={() => toggleActive(effective)}
                disabled={busy}
              >
                {busy ? "WORKING…" : effective.active ? "DEACTIVATE" : "ACTIVATE"}
              </ActionButton>
            </div>
          </div>
        ) : (
          <Panel>
            <SectionLabel>Nothing selected</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
              Create a stop with a Google Places address — its coordinates make it reusable across routes and, later, plottable on the Live Map.
            </div>
          </Panel>
        )}
      </div>
    </div>,
  );
}
