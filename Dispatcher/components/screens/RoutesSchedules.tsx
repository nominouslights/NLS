"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, statusMeta, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createRoute,
  createScheduleTemplate,
  DAY_SHORT,
  hhmm,
  listRoutes,
  listScheduleTemplates,
  refetchUntil,
  setScheduleTemplateActive,
  svcForTrip,
  updateRoute,
  updateScheduleTemplate,
  WEEK_DAYS,
  type DayName,
  type RouteInput,
  type RouteRecord,
  type ScheduleTemplateInput,
  type ScheduleTemplateRecord,
  type TripServiceType,
} from "@/lib/api/trips";
import { listClients, SERVICE_TYPE_LABELS, type ClientRecord } from "@/lib/api/clients";
import { listDrivers, type DriverRecord } from "@/lib/api/drivers";
import { PageHeader, Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { CorridorStepper } from "@/components/ui/CorridorStepper";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField, TextField, TimeField } from "@/components/ui/Field";

// Routes & Schedules — corridor routes and recurring schedule templates from
// the real Trips API (GET /api/trips/routes, /api/trips/schedule-templates).
// The weekly grid is derived client-side from template daysOfWeek + departure
// times; a backend worker materializes trips from ACTIVE templates ~every
// 30 min, GenerationHorizonDays ahead.

type Selection = { kind: "route" | "template"; id: string };

function durationLabel(minutes: number): string {
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h > 0 ? `~${h}h ${String(m).padStart(2, "0")}m` : `~${m}m`;
}

// ---------------------------------------------------------------------------
// Route form modal — POST /api/trips/routes · PUT /api/trips/routes/{id}
// ---------------------------------------------------------------------------

function RouteFormModal({
  existing,
  onClose,
  onSaved,
}: {
  existing: RouteRecord | null;
  onClose: () => void;
  onSaved: (input: RouteInput & { active: boolean }, existingId: string | null) => Promise<void>;
}) {
  const editing = existing !== null;
  const [name, setName] = useState(existing?.name ?? "");
  const [stopsText, setStopsText] = useState(
    existing ? [...existing.stops].sort((a, b) => a.order - b.order).map((s) => s.name).join("\n") : "",
  );
  const [distanceKm, setDistanceKm] = useState(existing ? String(existing.distanceKm) : "");
  const [durationMin, setDurationMin] = useState(existing ? String(existing.estimatedDurationMinutes) : "");
  const [licenceClass, setLicenceClass] = useState(existing?.requiredLicenceClass ?? "");
  const [active, setActive] = useState(existing?.active ?? true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    const stops = stopsText
      .split("\n")
      .map((s) => s.trim())
      .filter(Boolean)
      .map((s, i) => ({ name: s, order: i + 1 }));
    if (!name.trim()) return setError("Enter the route name.");
    if (stops.length < 2) return setError("Enter at least two stops (one per line, in corridor order).");
    const km = Number(distanceKm);
    if (!Number.isInteger(km) || km <= 0) return setError("Distance must be a whole number of km.");
    const mins = Number(durationMin);
    if (!Number.isInteger(mins) || mins <= 0) return setError("Estimated duration must be whole minutes.");

    setBusy(true);
    setError(null);
    try {
      await onSaved(
        {
          name: name.trim(),
          stops,
          distanceKm: km,
          estimatedDurationMinutes: mins,
          requiredLicenceClass: licenceClass.trim() || null,
          active,
        },
        existing?.id ?? null,
      );
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the route — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Operations · Corridor routes"
      title={editing ? "Edit Route" : "New Route"}
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
            {busy ? "SAVING…" : editing ? "SAVE ROUTE" : "CREATE ROUTE"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Route name" value={name} onChange={setName} placeholder="Thompson ⇄ Lynn Lake" />
        <TextField label="Required licence class (optional)" value={licenceClass} onChange={setLicenceClass} placeholder="Class 4" />
        <NumberField label="Distance (km)" value={distanceKm} onChange={setDistanceKm} min={1} step={1} />
        <NumberField label="Estimated duration (minutes)" value={durationMin} onChange={setDurationMin} min={1} step={5} />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Stops · one per line, corridor order"
          value={stopsText}
          onChange={setStopsText}
          rows={4}
          placeholder={"Thompson\nLeaf Rapids\nLynn Lake"}
          hint={<span style={{ color: colors.textFaint }}>· origin first, destination last</span>}
        />
      </div>
      {editing && (
        <div style={{ marginTop: 14, display: "flex", alignItems: "center", gap: 10 }}>
          <ActionButton variant={active ? "secondary" : "success"} onClick={() => setActive((v) => !v)}>
            {active ? "MARK INACTIVE" : "MARK ACTIVE"}
          </ActionButton>
          {active ? (
            <StatusChip kind="ontime" label="Active — available for templates & trips" />
          ) : (
            <StatusChip kind="off" label="Inactive — hidden from pickers" />
          )}
        </div>
      )}
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Schedule template form modal — POST/PUT /api/trips/schedule-templates.
// Active is toggled separately via /activate and /deactivate.
// ---------------------------------------------------------------------------

const SERVICE_TYPES = Object.keys(SERVICE_TYPE_LABELS) as TripServiceType[];

function TemplateFormModal({
  existing,
  routes,
  onClose,
  onSaved,
}: {
  existing: ScheduleTemplateRecord | null;
  routes: RouteRecord[];
  onClose: () => void;
  onSaved: (input: ScheduleTemplateInput, existingId: string | null) => Promise<void>;
}) {
  const editing = existing !== null;
  const [name, setName] = useState(existing?.name ?? "");
  const [routeId, setRouteId] = useState(existing?.routeId ?? routes[0]?.id ?? "");
  const [serviceType, setServiceType] = useState<TripServiceType>(existing?.serviceType ?? "Community");
  const [clientId, setClientId] = useState(existing?.clientId ?? "");
  const [days, setDays] = useState<DayName[]>(existing?.daysOfWeek ?? []);
  const [departure, setDeparture] = useState(existing ? hhmm(existing.departureTime) : "");
  const [returnDeparture, setReturnDeparture] = useState(
    existing?.returnDepartureTime ? hhmm(existing.returnDepartureTime) : "",
  );
  const [seatsCapacity, setSeatsCapacity] = useState(existing ? String(existing.seatsCapacity) : "");
  const [seatsMinimum, setSeatsMinimum] = useState(existing?.seatsMinimum != null ? String(existing.seatsMinimum) : "");
  const [vehicleUnit, setVehicleUnit] = useState(existing?.defaultVehicleUnit ?? "");
  const [defaultDriverId, setDefaultDriverId] = useState(existing?.defaultDriverId ?? "");
  const [horizonDays, setHorizonDays] = useState(existing ? String(existing.generationHorizonDays) : "7");
  const [cutoffNote, setCutoffNote] = useState(existing?.cutoffNote ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [clients, setClients] = useState<ClientRecord[] | null>(null);
  const [drivers, setDrivers] = useState<DriverRecord[] | null>(null);

  useEffect(() => {
    let active = true;
    listClients().then(
      (rows) => {
        if (active) setClients(rows);
      },
      () => {
        if (active) setClients([]);
      },
    );
    listDrivers().then(
      (rows) => {
        if (active) setDrivers(rows.filter((d) => d.status === "Active"));
      },
      () => {
        if (active) setDrivers([]);
      },
    );
    return () => {
      active = false;
    };
  }, []);

  function toggleDay(day: DayName) {
    setDays((cur) => (cur.includes(day) ? cur.filter((d) => d !== day) : [...cur, day]));
  }

  async function submit() {
    if (busy) return;
    if (!name.trim()) return setError("Enter the template name.");
    if (!routeId) return setError("Pick the route this template runs on.");
    if (days.length === 0) return setError("Pick at least one day of the week.");
    if (!departure) return setError("Enter the departure time.");
    const cap = Number(seatsCapacity);
    if (!Number.isInteger(cap) || cap <= 0) return setError("Seats capacity must be a whole number.");
    const min = seatsMinimum === "" ? null : Number(seatsMinimum);
    if (min !== null && (!Number.isInteger(min) || min < 0)) return setError("Seats minimum must be a whole number.");
    const horizon = Number(horizonDays);
    if (!Number.isInteger(horizon) || horizon <= 0) return setError("Generation horizon must be a whole number of days.");

    const client = clients?.find((c) => c.id === clientId) ?? null;
    const input: ScheduleTemplateInput = {
      name: name.trim(),
      routeId,
      serviceType,
      clientId: client?.id ?? null,
      clientName: client?.name ?? null,
      daysOfWeek: WEEK_DAYS.filter((d) => days.includes(d)),
      departureTime: departure,
      returnDepartureTime: returnDeparture || null,
      seatsCapacity: cap,
      seatsMinimum: min,
      defaultVehicleUnit: vehicleUnit.trim() || null,
      defaultDriverId: defaultDriverId || null,
      generationHorizonDays: horizon,
      cutoffNote: cutoffNote.trim() || null,
    };

    setBusy(true);
    setError(null);
    try {
      await onSaved(input, existing?.id ?? null);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the template — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Operations · Schedule templates"
      title={editing ? "Edit Schedule Template" : "New Schedule Template"}
      onClose={onClose}
      error={error}
      maxWidth={760}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
            {busy ? "SAVING…" : editing ? "SAVE TEMPLATE" : "CREATE TEMPLATE"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="Template name" value={name} onChange={setName} placeholder="Daily Alamos crew shuttle" />
        <SelectField
          label="Route"
          value={routeId}
          onChange={setRouteId}
          options={routes.map((r) => ({ value: r.id, label: r.name }))}
        />
        <SelectField
          label="Service type"
          value={serviceType}
          onChange={(v) => setServiceType(v as TripServiceType)}
          options={SERVICE_TYPES.map((s) => ({ value: s, label: SERVICE_TYPE_LABELS[s] }))}
        />
        <SelectField
          label="Client (optional · name is snapshotted)"
          value={clientId}
          onChange={setClientId}
          options={[
            { value: "", label: "— none —" },
            ...(clients ?? []).map((c) => ({ value: c.id, label: c.name })),
          ]}
        />
      </div>

      <div style={{ marginTop: 14 }}>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 11.5,
            color: colors.textLabel,
            marginBottom: 5,
          }}
        >
          Days of week
        </div>
        <div style={{ display: "flex", gap: 7, flexWrap: "wrap" }}>
          {WEEK_DAYS.map((day) => {
            const on = days.includes(day);
            return (
              <span
                key={day}
                onClick={() => toggleDay(day)}
                style={{
                  fontFamily: fonts.body,
                  fontWeight: on ? 600 : 500,
                  fontSize: 12,
                  padding: "5px 12px",
                  borderRadius: 7,
                  background: on ? colors.cardBgActive : colors.cardBg,
                  border: `1px solid ${on ? colors.borderActive : colors.border}`,
                  color: on ? colors.headingBright : colors.textMuted,
                  cursor: "pointer",
                  userSelect: "none",
                }}
              >
                {on ? "☒" : "☐"} {DAY_SHORT[day]}
              </span>
            );
          })}
        </div>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr 1fr", gap: 14, marginTop: 14 }}>
        <TimeField label="Departure" value={departure} onChange={setDeparture} />
        <TimeField
          label="Return departure"
          value={returnDeparture}
          onChange={setReturnDeparture}
          hint={<span style={{ color: colors.textFaint }}>· set = paired round trip</span>}
        />
        <NumberField label="Seats capacity" value={seatsCapacity} onChange={setSeatsCapacity} min={1} step={1} />
        <NumberField label="Seats minimum" value={seatsMinimum} onChange={setSeatsMinimum} min={0} step={1} />
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 14, marginTop: 14 }}>
        <TextField label="Default vehicle unit (optional)" value={vehicleUnit} onChange={setVehicleUnit} mono placeholder="U-02" />
        <SelectField
          label="Default driver (optional)"
          value={defaultDriverId}
          onChange={setDefaultDriverId}
          options={[
            { value: "", label: "— open, needs coverage —" },
            ...(drivers ?? []).map((d) => ({ value: d.id, label: d.name })),
          ]}
        />
        <NumberField label="Generation horizon (days)" value={horizonDays} onChange={setHorizonDays} min={1} step={1} />
      </div>

      <div style={{ marginTop: 14 }}>
        <TextField label="Cutoff note (optional)" value={cutoffNote} onChange={setCutoffNote} placeholder="Wed noon cutoff → Fri delivery" />
      </div>
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------

export default function RoutesSchedules() {
  const [routes, setRoutes] = useState<RouteRecord[] | null>(null);
  const [templates, setTemplates] = useState<ScheduleTemplateRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [sel, setSel] = useState<Selection | null>(null);
  const [modal, setModal] = useState<null | "newRoute" | "editRoute" | "newTemplate" | "editTemplate">(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const [r, t] = await Promise.all([listRoutes(), listScheduleTemplates()]);
      setRoutes(r);
      setTemplates(t);
      setLoadError(null);
    } catch (e) {
      setRoutes(null);
      setTemplates(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load routes & schedules.");
    }
  }, []);

  useEffect(() => {
    let active = true;
    Promise.all([listRoutes(), listScheduleTemplates()]).then(
      ([r, t]) => {
        if (active) {
          setRoutes(r);
          setTemplates(t);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRoutes(null);
          setTemplates(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load routes & schedules.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const route = sel?.kind === "route" ? routes?.find((r) => r.id === sel.id) ?? null : null;
  const template = sel?.kind === "template" ? templates?.find((t) => t.id === sel.id) ?? null : null;
  const effectiveRoute = route ?? (sel === null ? routes?.[0] ?? null : null);
  const effectiveSel: Selection | null = sel ?? (effectiveRoute ? { kind: "route", id: effectiveRoute.id } : null);

  // ---- mutations (reads are eventually consistent projections) ----

  async function saveRoute(input: RouteInput & { active: boolean }, existingId: string | null) {
    if (existingId) {
      await updateRoute(existingId, input);
      const fresh = await refetchUntil(listRoutes, (rows) => {
        const r = rows.find((x) => x.id === existingId);
        return r !== undefined && r.name === input.name && r.active === input.active && r.distanceKm === input.distanceKm;
      });
      setRoutes(fresh);
    } else {
      const { active: _active, ...createInput } = input;
      void _active; // new routes are always created active backend-side
      const newId = await createRoute(createInput);
      const fresh = await refetchUntil(listRoutes, (rows) => rows.some((x) => x.id === newId));
      setRoutes(fresh);
      setSel({ kind: "route", id: newId });
    }
  }

  async function saveTemplate(input: ScheduleTemplateInput, existingId: string | null) {
    if (existingId) {
      await updateScheduleTemplate(existingId, input);
      const fresh = await refetchUntil(listScheduleTemplates, (rows) => {
        const t = rows.find((x) => x.id === existingId);
        return t !== undefined && t.name === input.name && t.routeId === input.routeId;
      });
      setTemplates(fresh);
    } else {
      const newId = await createScheduleTemplate(input);
      const fresh = await refetchUntil(listScheduleTemplates, (rows) => rows.some((x) => x.id === newId));
      setTemplates(fresh);
      setSel({ kind: "template", id: newId });
    }
  }

  async function toggleTemplateActive(t: ScheduleTemplateRecord) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await setScheduleTemplateActive(t.id, !t.active);
      const fresh = await refetchUntil(
        listScheduleTemplates,
        (rows) => rows.find((x) => x.id === t.id)?.active === !t.active,
      );
      setTemplates(fresh);
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to change the template — please try again.");
    } finally {
      setBusy(false);
    }
  }

  // ---- shared frame ----

  const frame = (children: React.ReactNode) => (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader
          eyebrow="Operations · Corridor routes & recurring schedule templates"
          title="Routes & Schedules"
          right={
            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton onClick={() => setModal("newRoute")}>+ NEW ROUTE</ActionButton>
              <ActionButton variant="primary" onClick={() => setModal("newTemplate")}>
                + NEW TEMPLATE
              </ActionButton>
            </div>
          }
        />
      </div>
      {children}
      {/* modals live in the frame so + NEW works from every state */}
      {modal === "newRoute" && <RouteFormModal existing={null} onClose={() => setModal(null)} onSaved={saveRoute} />}
      {modal === "editRoute" && effectiveRoute && (
        <RouteFormModal existing={effectiveRoute} onClose={() => setModal(null)} onSaved={saveRoute} />
      )}
      {modal === "newTemplate" && routes !== null && (
        <TemplateFormModal
          existing={null}
          routes={routes.filter((r) => r.active)}
          onClose={() => setModal(null)}
          onSaved={saveTemplate}
        />
      )}
      {modal === "editTemplate" && template && routes !== null && (
        <TemplateFormModal existing={template} routes={routes} onClose={() => setModal(null)} onSaved={saveTemplate} />
      )}
    </div>
  );

  if (loadError) {
    return frame(
      <div style={{ padding: "26px", maxWidth: 560 }}>
        <Panel borderColor="rgba(213,94,0,.4)">
          <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
            <StatusChip kind="over" label={`Routes & schedules unavailable — ${loadError}`} />
            <ActionButton variant="primary" onClick={load}>
              RETRY
            </ActionButton>
          </div>
        </Panel>
      </div>,
    );
  }

  if (routes === null || templates === null) {
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
          Loading routes &amp; schedule templates from API…
        </div>
      </div>,
    );
  }

  // Weekly grid for a route: active templates on that route, by day.
  function gridFor(r: RouteRecord) {
    return WEEK_DAYS.map((day) => ({
      day,
      templates: (templates ?? []).filter((t) => t.routeId === r.id && t.active && t.daysOfWeek.includes(day)),
    }));
  }

  return frame(
    <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "34% 1fr", borderTop: `1px solid ${colors.border}` }}>
      {/* LEFT — routes + templates */}
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
          Routes · {routes.length}
        </div>
        {routes.length === 0 && (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginBottom: 10 }}>
            No routes yet — create the first corridor route.
          </div>
        )}
        {routes.map((r) => {
          const active = effectiveSel?.kind === "route" && effectiveSel.id === r.id;
          return (
            <div
              key={r.id}
              onClick={() => setSel({ kind: "route", id: r.id })}
              style={{
                padding: "12px 14px",
                marginBottom: 5,
                borderRadius: 9,
                border: `1px solid ${active ? colors.borderActive : colors.borderSubtle}`,
                background: active ? colors.cardBgActive : colors.cardBg,
                boxShadow: active ? `inset 3px 0 0 ${colors.blue}, ${colors.shadowCard}` : colors.shadowCard,
                cursor: "pointer",
                opacity: r.active ? 1 : 0.62,
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary, minWidth: 0 }}>
                  {r.name}
                </div>
                {!r.active && <StatusChip kind="off" label="Inactive" />}
              </div>
              <div style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim, marginTop: 3 }}>
                {r.stops.length} stops · {r.distanceKm} km · {durationLabel(r.estimatedDurationMinutes)}
                {r.requiredLicenceClass ? ` · ${r.requiredLicenceClass}` : ""}
              </div>
            </div>
          );
        })}

        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".14em",
            textTransform: "uppercase",
            color: colors.textFaint,
            margin: "16px 0 10px",
          }}
        >
          Schedule templates · {templates.length}
        </div>
        {templates.length === 0 && (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            No templates yet — a template + the generation worker keeps the board filled days ahead.
          </div>
        )}
        {templates.map((t) => {
          const active = effectiveSel?.kind === "template" && effectiveSel.id === t.id;
          return (
            <div
              key={t.id}
              onClick={() => setSel({ kind: "template", id: t.id })}
              style={{
                padding: "12px 14px",
                marginBottom: 5,
                borderRadius: 9,
                border: `1px solid ${active ? colors.borderActive : colors.borderSubtle}`,
                background: active ? colors.cardBgActive : colors.cardBg,
                boxShadow: active ? `inset 3px 0 0 ${colors.blue}, ${colors.shadowCard}` : colors.shadowCard,
                cursor: "pointer",
              }}
            >
              <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>{t.name}</div>
              <div
                style={{
                  fontFamily: fonts.body,
                  fontSize: 11,
                  color: t.active ? statusMeta("ontime").t : statusMeta("off").t,
                  marginTop: 3,
                }}
              >
                {t.active
                  ? `${statusMeta("ontime").g} Active · generates ${t.generationHorizonDays} days ahead`
                  : `${statusMeta("off").g} Inactive · not generating trips`}
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

        {effectiveRoute && effectiveSel?.kind === "route" && (
          <div className="detailfade" key={effectiveRoute.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1, color: colors.headingBright, margin: "0 0 4px" }}>
                {effectiveRoute.name}
              </h2>
              {effectiveRoute.active ? <StatusChip kind="ontime" label="Active" /> : <StatusChip kind="off" label="Inactive" />}
            </div>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, margin: "4px 0 16px" }}>
              Ordered stops · {effectiveRoute.distanceKm} km · {durationLabel(effectiveRoute.estimatedDurationMinutes)} typical
              {effectiveRoute.requiredLicenceClass ? ` · ${effectiveRoute.requiredLicenceClass} required` : ""}
            </div>

            <CorridorStepper stops={[...effectiveRoute.stops].sort((a, b) => a.order - b.order).map((s) => s.name)} />

            <div style={{ padding: "15px 16px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 11, marginBottom: 14, boxShadow: colors.shadowCard }}>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".14em",
                  textTransform: "uppercase",
                  color: colors.textLabel,
                  marginBottom: 12,
                }}
              >
                Weekly schedule · derived from active templates
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(7,1fr)", gap: 7 }}>
                {gridFor(effectiveRoute).map(({ day, templates: dayTemplates }) => {
                  const first = dayTemplates[0];
                  const meta = first ? svcMeta(svcForTrip(first.serviceType)) : null;
                  return (
                    <div key={day} style={{ textAlign: "center" }}>
                      <div style={{ fontFamily: fonts.semiCondensed, fontSize: 10, letterSpacing: ".06em", color: colors.textDim, marginBottom: 6 }}>
                        {DAY_SHORT[day]}
                      </div>
                      <div
                        style={{
                          minHeight: 52,
                          borderRadius: 7,
                          background: meta ? meta.chipBg : colors.inputBg,
                          border: `1px solid ${meta ? meta.chipBd : colors.borderSubtle}`,
                          display: "flex",
                          flexDirection: "column",
                          alignItems: "center",
                          justifyContent: "center",
                          fontFamily: fonts.body,
                          fontSize: 10,
                          color: meta ? meta.chipTx : colors.textDim,
                          textAlign: "center",
                          lineHeight: 1.25,
                          padding: "4px 3px",
                          overflow: "hidden",
                        }}
                      >
                        {first ? (
                          <>
                            <span style={{ fontWeight: 600, overflow: "hidden", textOverflow: "ellipsis", maxWidth: "100%", whiteSpace: "nowrap" }}>
                              {first.name}
                            </span>
                            <span style={{ fontFamily: fonts.mono }}>{hhmm(first.departureTime)}</span>
                            {dayTemplates.length > 1 && (
                              <span style={{ fontFamily: fonts.mono, fontSize: 9 }}>+{dayTemplates.length - 1} more</span>
                            )}
                          </>
                        ) : (
                          ""
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>

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
              Editing a template applies to <strong style={{ color: colors.textPrimary }}>future generated trips only</strong> — past and
              already-generated trips retain their original detail. The generation worker materializes trips from active templates roughly
              every 30 minutes.
            </div>

            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton variant="primary" onClick={() => setModal("editRoute")}>
                EDIT ROUTE
              </ActionButton>
            </div>
          </div>
        )}

        {template && effectiveSel?.kind === "template" && (
          <div className="detailfade" key={template.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
              <ServiceChip svc={svcForTrip(template.serviceType)} />
              {template.active ? <StatusChip kind="ontime" label="Active" /> : <StatusChip kind="off" label="Inactive" />}
            </div>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1.05, color: colors.headingBright, margin: "8px 0 4px" }}>
              {template.name}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 16 }}>
              {template.routeName ?? "route"} · departs {hhmm(template.departureTime)}
              {template.returnDepartureTime ? ` · return ${hhmm(template.returnDepartureTime)}` : ""}
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
              <Panel>
                <SectionLabel>Cadence</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="Days" value={template.daysOfWeek.map((d) => DAY_SHORT[d]).join(" · ") || "—"} />
                  <DetailRow label="Departure" value={hhmm(template.departureTime)} valueStyle={{ fontFamily: fonts.mono }} />
                  <DetailRow
                    label="Return leg"
                    value={template.returnDepartureTime ? `${hhmm(template.returnDepartureTime)} · paired round trip` : "one-way"}
                    valueStyle={{ fontFamily: fonts.mono }}
                  />
                  <DetailRow label="Generation horizon" value={`${template.generationHorizonDays} days ahead`} />
                </div>
              </Panel>
              <Panel>
                <SectionLabel>Capacity &amp; defaults</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="Seats capacity" value={String(template.seatsCapacity)} valueStyle={{ fontFamily: fonts.mono }} />
                  <DetailRow
                    label="Seats minimum"
                    value={template.seatsMinimum != null ? String(template.seatsMinimum) : "no minimum"}
                    valueStyle={{ fontFamily: fonts.mono }}
                  />
                  <DetailRow label="Default vehicle" value={template.defaultVehicleUnit ?? "—"} valueStyle={{ fontFamily: fonts.mono }} />
                  <DetailRow label="Client" value={template.clientName ?? "—"} />
                </div>
              </Panel>
            </div>

            {template.cutoffNote && (
              <Panel style={{ marginBottom: 12 }}>
                <SectionLabel>Cutoff note</SectionLabel>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                  {template.cutoffNote}
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
              Edits apply to <strong style={{ color: colors.textPrimary }}>future generated trips only</strong>. Deactivating stops new
              trips from generating — already-generated trips stay on the board.
            </div>

            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton variant="primary" onClick={() => setModal("editTemplate")}>
                EDIT TEMPLATE
              </ActionButton>
              <ActionButton
                variant={template.active ? "destructive" : "success"}
                onClick={() => toggleTemplateActive(template)}
                style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}
              >
                {busy ? "WORKING…" : template.active ? "DEACTIVATE" : "ACTIVATE"}
              </ActionButton>
            </div>
          </div>
        )}

        {!effectiveRoute && !template && (
          <Panel>
            <SectionLabel>Nothing selected</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
              Create a corridor route, then add a schedule template — the generation worker will materialize upcoming
              trips onto the Dispatch Board automatically.
            </div>
          </Panel>
        )}
      </div>
    </div>,
  );
}
