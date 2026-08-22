"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createStop,
  listStops,
  refetchUntil,
  setStopActive,
  sortStops,
  stopAddressLine,
  stopTypeLabel,
  updateStop,
  type StopInput,
  type StopRecord,
} from "@/lib/api/stops";
import { PageHeader, Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { StopFormModal } from "@/components/StopFormModal";

// Stops — the reusable catalog of rich, geocoded stops (GET/POST/PUT
// /api/trips/stops). Each stop carries a structured address + lat/lng captured
// from Google Places autocomplete; routes are built by selecting stops from
// this catalog, and the coordinates feed the Live Map later. Styled to match
// Routes & Schedules; mutations use the same eventual-consistency refetch.

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
