"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta, svcMeta } from "@/lib/theme";
import { ApiError, getTripManifest, type TripManifest } from "@/lib/api";
import {
  assignTrip,
  changeTripStatus,
  corridorLabel,
  getTrip,
  hasClearanceFor,
  hhmm,
  isOpenTrip,
  isoDaysFromToday,
  listTrips,
  refetchUntil,
  seatsLabel,
  shortDateLabel,
  sortTrips,
  stopNames,
  svcForTrip,
  tripChip,
  tripWindowLabel,
  updateTrip,
  type TripRecord,
  type TripUpdateInput,
} from "@/lib/api/trips";
import {
  listDrivers,
  listDriverClearances,
  type DriverClearanceRecord,
  type DriverRecord,
} from "@/lib/api/drivers";
import { contractRateLabel, getClient, type ActiveContractSummary } from "@/lib/api/clients";
import { printTripManifest } from "@/lib/documents/tripManifestPdf";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { CorridorStepper } from "@/components/ui/CorridorStepper";
import { Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, TextAreaField, TextField, TimeField } from "@/components/ui/Field";

// Trips — master list + detail from the real Trips API (GET /api/trips over a
// ±1-week window). "Open — needs coverage" / "Empty leg available" are frontend
// derivations (lib/api/trips.ts), never persisted statuses.

const FILTERS = ["All trips", "Open only", "Assigned"] as const;
const WINDOW_DAYS_BACK = 7;
const WINDOW_DAYS_AHEAD = 7;

function fmtUtcDateTime(iso: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("en-CA", {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

// ---------------------------------------------------------------------------
// Pre-data states (Drivers.tsx conventions)
// ---------------------------------------------------------------------------

function LoadError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div style={{ padding: "26px", maxWidth: 560 }}>
      <Panel borderColor="rgba(213,94,0,.4)">
        <div style={{ display: "flex", gap: 11, alignItems: "center", marginBottom: 12 }}>
          <span
            style={{
              width: 20,
              height: 20,
              flex: "none",
              borderRadius: 5,
              background: "#D55E00",
              color: "#fff",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 11,
              fontWeight: 800,
            }}
          >
            ▲
          </span>
          <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: statusMeta("over").t }}>
            Trips unavailable
          </span>
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
          {message}
        </div>
        <ActionButton variant="primary" onClick={onRetry}>
          RETRY
        </ActionButton>
      </Panel>
    </div>
  );
}

function LoadingSkeleton() {
  return (
    <div style={{ padding: "16px 26px" }}>
      {[0, 1, 2, 3, 4].map((i) => (
        <div
          key={i}
          style={{
            height: 78,
            borderRadius: 9,
            border: `1px solid ${colors.borderSubtle}`,
            background: colors.cardBg,
            marginBottom: 6,
            opacity: 0.55 - i * 0.08,
          }}
        />
      ))}
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
        Loading trips from API…
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Assign / reassign modal — Active drivers from the Drivers API, with a
// UI-side clearance warning (gold chip, never a hard block) when the driver
// lacks an unexpired clearance for the trip's client.
// ---------------------------------------------------------------------------

function AssignModal({
  trip,
  onClose,
  onSaved,
}: {
  trip: TripRecord;
  onClose: () => void;
  onSaved: (driverId: string | null, vehicleUnit: string | null) => Promise<void>;
}) {
  const [roster, setRoster] = useState<{ driver: DriverRecord; cleared: boolean }[] | null>(null);
  const [rosterError, setRosterError] = useState<string | null>(null);
  const [driverId, setDriverId] = useState<string | null>(trip.driverId);
  const [vehicleUnit, setVehicleUnit] = useState(trip.vehicleUnit ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    (async () => {
      try {
        const drivers = (await listDrivers()).filter((d) => d.status === "Active");
        // Clearance check needs each driver's clearances — small roster, so a
        // parallel fan-out is fine. A failed clearance fetch degrades to
        // "no clearances on file" (warn) rather than blocking the modal.
        const clearances = await Promise.all(
          drivers.map((d) => listDriverClearances(d.id).catch(() => [] as DriverClearanceRecord[])),
        );
        if (active) {
          setRoster(
            drivers.map((driver, i) => ({
              driver,
              cleared: hasClearanceFor(clearances[i], trip.clientName),
            })),
          );
        }
      } catch (e) {
        if (active) setRosterError(e instanceof ApiError ? e.message : "Failed to load drivers.");
      }
    })();
    return () => {
      active = false;
    };
  }, [trip.clientName]);

  async function submit() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await onSaved(driverId, vehicleUnit.trim() || null);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Assignment failed — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · Assignment`}
      title="Assign Driver & Vehicle"
      onClose={onClose}
      error={error ?? rosterError}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "SAVE ASSIGNMENT"}
          </ActionButton>
        </>
      }
    >
      <div style={{ marginBottom: 16 }}>
        <TextField
          label="Vehicle unit"
          value={vehicleUnit}
          onChange={setVehicleUnit}
          mono
          placeholder="U-02"
          hint={<span style={{ color: colors.textFaint }}>· free text; blank clears the vehicle</span>}
        />
      </div>
      <SectionLabel>Driver · Active roster</SectionLabel>
      {roster === null && !rosterError && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading drivers…</div>
      )}
      {roster !== null && (
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <div
            onClick={() => setDriverId(null)}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 10,
              padding: "10px 13px",
              ...rowSurface(driverId === null, colors.amber),
            }}
          >
            <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.amberText }}>
              OPEN — needs coverage
            </span>
            <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              unassign the driver
            </span>
          </div>
          {roster.map(({ driver, cleared }) => (
            <div
              key={driver.id}
              onClick={() => setDriverId(driver.id)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 10,
                padding: "10px 13px",
                ...rowSurface(driverId === driver.id, colors.blue),
              }}
            >
              <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 500, color: colors.textPrimary }}>
                {driver.name}
              </span>
              <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{driver.licenceClass}</span>
              <span style={{ marginLeft: "auto" }}>
                {trip.clientName ? (
                  cleared ? (
                    <StatusChip kind="ontime" label={`${trip.clientName} clearance`} />
                  ) : (
                    <StatusChip kind="soon" label={`No ${trip.clientName} clearance`} />
                  )
                ) : (
                  <StatusChip kind="ontime" label="Eligible" />
                )}
              </span>
            </div>
          ))}
          {roster.length === 0 && (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
              No Active drivers on the roster.
            </div>
          )}
        </div>
      )}
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Edit modal — PUT /api/trips/{id}, editable only while Scheduled. Corridor,
// service type, and client identity pass through unchanged; the schedule,
// billing snapshot, and seat fields are editable here.
// ---------------------------------------------------------------------------

function EditTripModal({
  trip,
  onClose,
  onSaved,
}: {
  trip: TripRecord;
  onClose: () => void;
  onSaved: (input: TripUpdateInput) => Promise<void>;
}) {
  const [serviceDate, setServiceDate] = useState(trip.serviceDate);
  const [windowStart, setWindowStart] = useState(hhmm(trip.windowStart));
  const [windowEnd, setWindowEnd] = useState(trip.windowEnd ? hhmm(trip.windowEnd) : "");
  const [distanceKm, setDistanceKm] = useState(String(trip.distanceKm));
  const [poNumber, setPoNumber] = useState(trip.poNumber ?? "");
  const [seatsCapacity, setSeatsCapacity] = useState(trip.seatsCapacity != null ? String(trip.seatsCapacity) : "");
  const [seatsMinimum, setSeatsMinimum] = useState(trip.seatsMinimum != null ? String(trip.seatsMinimum) : "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!serviceDate) return setError("Enter the service date.");
    if (!windowStart) return setError("Enter the departure window start.");
    const km = Number(distanceKm);
    if (!Number.isInteger(km) || km < 0) return setError("Distance must be a whole number of km.");
    const cap = seatsCapacity === "" ? null : Number(seatsCapacity);
    const min = seatsMinimum === "" ? null : Number(seatsMinimum);
    if (cap !== null && (!Number.isInteger(cap) || cap < 0)) return setError("Seats capacity must be a whole number.");
    if (min !== null && (!Number.isInteger(min) || min < 0)) return setError("Seats minimum must be a whole number.");

    const input: TripUpdateInput = {
      serviceDate,
      windowStart,
      windowEnd: windowEnd || null,
      serviceType: trip.serviceType,
      routeId: trip.routeId,
      routeName: trip.routeName,
      origin: trip.origin,
      destination: trip.destination,
      stops: trip.stops,
      distanceKm: km,
      isEmptyLeg: trip.isEmptyLeg,
      clientId: trip.clientId,
      clientName: trip.clientName,
      poNumber: poNumber.trim() || null,
      seatsCapacity: cap,
      seatsMinimum: min,
    };

    setBusy(true);
    setError(null);
    try {
      await onSaved(input);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the trip — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · ${corridorLabel(trip)}`}
      title="Edit Trip"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "SAVE TRIP"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <DateField label="Service date" value={serviceDate} onChange={setServiceDate} />
        <NumberField label="Distance (km)" value={distanceKm} onChange={setDistanceKm} min={0} step={1} />
        <TimeField label="Window start" value={windowStart} onChange={setWindowStart} />
        <TimeField label="Window end (optional)" value={windowEnd} onChange={setWindowEnd} />
        <TextField label="PO number (optional)" value={poNumber} onChange={setPoNumber} mono placeholder="PO-AG-2261" />
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
          <NumberField label="Seats capacity" value={seatsCapacity} onChange={setSeatsCapacity} min={0} step={1} />
          <NumberField label="Seats minimum" value={seatsMinimum} onChange={setSeatsMinimum} min={0} step={1} />
        </div>
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 14, lineHeight: 1.5 }}>
        Corridor, service type, and client come from the trip&rsquo;s route/client snapshot and are not editable here.
        Trips are editable only while Scheduled — the backend rejects edits after departure.
      </div>
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Cancel modal — POST /status with an optional reason.
// ---------------------------------------------------------------------------

function CancelTripModal({
  trip,
  onClose,
  onConfirmed,
}: {
  trip: TripRecord;
  onClose: () => void;
  onConfirmed: (reason: string | null) => Promise<void>;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await onConfirmed(reason.trim() || null);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to cancel the trip — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · ${corridorLabel(trip)}`}
      title="Cancel Trip"
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>KEEP TRIP</ActionButton>
          <ActionButton variant="destructive" onClick={submit} disabled={busy}>
            {busy ? "CANCELLING…" : "CANCEL TRIP"}
          </ActionButton>
        </>
      }
    >
      <TextAreaField
        label="Reason (recorded on the trip)"
        value={reason}
        onChange={setReason}
        rows={3}
        placeholder="Road closure PR-391 · rescheduled to tomorrow"
      />
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------

export default function Trips({
  selectedId,
  setSelectedId,
  onNewTrip,
}: {
  selectedId: string | null;
  setSelectedId: (id: string | null) => void;
  onNewTrip: () => void;
}) {
  const [filter, setFilter] = useState(0);
  const [rows, setRows] = useState<TripRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);

  // A selected trip that isn't in the list window yet (e.g. just created and
  // the projection is trailing) is fetched directly.
  const [extraTrip, setExtraTrip] = useState<TripRecord | null>(null);

  // Per-selected-trip auxiliary data, keyed by trip id (Drivers.tsx pattern).
  const [clearanceState, setClearanceState] = useState<{ tripId: string; cleared: boolean } | null>(null);
  const [contractState, setContractState] = useState<{ tripId: string; contract: ActiveContractSummary | null } | null>(null);
  const [manifestState, setManifestState] = useState<{ tripId: string; manifest: TripManifest | null } | null>(null);

  const [modal, setModal] = useState<null | "assign" | "edit" | "cancel">(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const openOnly = filter === 1;

  const fetchList = useCallback(() => {
    return listTrips({
      from: isoDaysFromToday(-WINDOW_DAYS_BACK),
      to: isoDaysFromToday(WINDOW_DAYS_AHEAD),
      openOnly: openOnly || undefined,
    });
  }, [openOnly]);

  const load = useCallback(async () => {
    try {
      const fresh = await fetchList();
      setRows(sortTrips(fresh));
      setLoadError(null);
    } catch (e) {
      setRows(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load trips.");
    }
  }, [fetchList]);

  useEffect(() => {
    let active = true;
    fetchList().then(
      (fresh) => {
        if (active) {
          setRows(sortTrips(fresh));
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRows(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load trips.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [fetchList]);

  // "Assigned" is a client-side driver-presence filter; "Open only" already
  // came filtered from the API (openOnly=true).
  const visible = rows === null ? null : filter === 2 ? rows.filter((r) => r.driverId !== null) : rows;

  const fromList = rows?.find((r) => r.id === selectedId) ?? null;
  const t = fromList ?? (extraTrip && extraTrip.id === selectedId ? extraTrip : null) ?? visible?.[0] ?? null;

  // Selected trip missing from the window — fetch it directly (tolerating the
  // projection lag right after creation).
  useEffect(() => {
    if (!selectedId || rows === null || fromList) return;
    let active = true;
    refetchUntil(
      () => getTrip(selectedId).catch(() => null),
      (v) => v !== null,
    ).then((trip) => {
      if (active && trip) setExtraTrip(trip);
    });
    return () => {
      active = false;
    };
  }, [selectedId, rows, fromList]);

  // Auxiliary detail fetches for the selected trip.
  const tripId = t?.id ?? null;
  const tripDriverId = t?.driverId ?? null;
  const tripClientId = t?.clientId ?? null;
  const tripClientName = t?.clientName ?? null;
  const tripManifestId = t?.manifestId ?? null;
  // No synchronous setState here: the aux states are keyed by tripId, so a
  // stale entry from a previous trip is simply ignored by the render.
  useEffect(() => {
    if (!tripId) return;
    let active = true;

    if (tripDriverId && tripClientName) {
      listDriverClearances(tripDriverId).then(
        (rows2) => {
          if (active) setClearanceState({ tripId, cleared: hasClearanceFor(rows2, tripClientName) });
        },
        () => undefined, // clearance check is best-effort — render falls back to "checking…"/"—"
      );
    }

    if (tripClientId) {
      getClient(tripClientId).then(
        (client) => {
          if (active) setContractState({ tripId, contract: client.activeContract });
        },
        () => {
          if (active) setContractState({ tripId, contract: null });
        },
      );
    }

    if (tripManifestId) {
      getTripManifest(tripManifestId).then(
        (m) => {
          if (active) setManifestState({ tripId, manifest: m });
        },
        () => {
          if (active) setManifestState({ tripId, manifest: null });
        },
      );
    }

    return () => {
      active = false;
    };
  }, [tripId, tripDriverId, tripClientId, tripClientName, tripManifestId]);

  async function runAction(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await fn();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Action failed — please try again.");
    } finally {
      setBusy(false);
    }
  }

  /** Refetch the list until the selected trip satisfies a predicate (reads are
   *  eventually consistent projections). */
  async function reloadUntil(id: string, satisfied: (trip: TripRecord | undefined) => boolean) {
    const fresh = await refetchUntil(fetchList, (list) => satisfied(list.find((r) => r.id === id)));
    setRows(sortTrips(fresh));
    const updated = fresh.find((r) => r.id === id);
    if (updated) setExtraTrip(updated);
  }

  async function onAssignSaved(id: string, driverId: string | null, vehicleUnit: string | null) {
    await assignTrip(id, driverId, vehicleUnit);
    await reloadUntil(id, (trip) => trip !== undefined && trip.driverId === driverId && trip.vehicleUnit === vehicleUnit);
  }

  async function onEditSaved(id: string, input: TripUpdateInput) {
    await updateTrip(id, input);
    await reloadUntil(
      id,
      (trip) => trip !== undefined && trip.serviceDate === input.serviceDate && (trip.poNumber ?? null) === (input.poNumber ?? null),
    );
  }

  function onChangeStatus(id: string, status: TripRecord["status"], reason?: string | null) {
    return runAction(async () => {
      await changeTripStatus(id, status, reason);
      await reloadUntil(id, (trip) => trip !== undefined && trip.status === status);
    });
  }

  // ---- header (shared across pre-data states) ----

  const header = (
    <div style={{ flex: "none", padding: "20px 26px 12px" }}>
      <div style={{ display: "flex", alignItems: "flex-end", justifyContent: "space-between", marginBottom: 14 }}>
        <div>
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 10.5,
              letterSpacing: ".16em",
              textTransform: "uppercase",
              color: colors.textFaint,
              marginBottom: 3,
            }}
          >
            Operations · Bookings &amp; Reservations
          </div>
          <h1 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: colors.headingBright, margin: 0 }}>
            Trips
          </h1>
        </div>
        <div
          onClick={onNewTrip}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 7,
            padding: "8px 15px",
            borderRadius: 8,
            background: colors.blue,
            color: "#FFFFFF",
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 13.5,
            letterSpacing: ".04em",
            cursor: "pointer",
          }}
        >
          <span style={{ fontSize: 15, lineHeight: 1 }}>+</span> NEW TRIP
        </div>
      </div>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
        {FILTERS.map((label, i) => (
          <span
            key={label}
            onClick={() => setFilter(i)}
            style={{
              fontFamily: fonts.body,
              fontWeight: filter === i ? 600 : 500,
              fontSize: 12,
              padding: "5px 12px",
              borderRadius: 7,
              background: filter === i ? colors.cardBgActive : colors.cardBg,
              border: `1px solid ${filter === i ? colors.borderActive : colors.border}`,
              color: filter === i ? colors.headingBright : colors.textMuted,
              cursor: "pointer",
            }}
          >
            {label}
          </span>
        ))}
      </div>
    </div>
  );

  if (loadError) {
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
        {header}
        <LoadError message={loadError} onRetry={load} />
      </div>
    );
  }

  if (visible === null) {
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
        {header}
        <LoadingSkeleton />
      </div>
    );
  }

  // ---- loaded ----

  const chip = t ? tripChip(t) : null;
  const clearance = t && clearanceState?.tripId === t.id ? clearanceState.cleared : null;
  const contract = t && contractState?.tripId === t.id ? contractState.contract : null;
  const manifest = t && manifestState?.tripId === t.id ? manifestState.manifest : null;

  const timeline = t
    ? [
        { label: "Created", time: fmtUtcDateTime(t.createdAtUtc), state: "done" as const },
        {
          label: `Scheduled · ${shortDateLabel(t.serviceDate)}`,
          time: tripWindowLabel(t),
          state: t.status === "Scheduled" ? ("active" as const) : ("done" as const),
        },
        ...(t.status === "InProgress"
          ? [{ label: "Trip started", time: "in progress", state: "active" as const }]
          : []),
        ...(t.status === "Completed"
          ? [{ label: "Completed", time: fmtUtcDateTime(t.completedAtUtc), state: "done" as const }]
          : []),
        ...(t.status === "Cancelled"
          ? [
              {
                label: t.cancelledReason ? `Cancelled — ${t.cancelledReason}` : "Cancelled",
                time: fmtUtcDateTime(t.updatedAtUtc),
                state: "done" as const,
              },
            ]
          : []),
      ]
    : [];

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      {header}

      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "42% 1fr", gap: 0, borderTop: `1px solid ${colors.border}` }}>
        {/* MASTER */}
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
            {visible.length} trip{visible.length === 1 ? "" : "s"} · {shortDateLabel(isoDaysFromToday(-WINDOW_DAYS_BACK))} →{" "}
            {shortDateLabel(isoDaysFromToday(WINDOW_DAYS_AHEAD))}
          </div>
          {visible.length === 0 && (
            <Panel>
              <SectionLabel>No trips in this window</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                Nothing matches the current filter. New trips come from the Create Trip wizard or are generated from
                active schedule templates (Routes &amp; Schedules).
              </div>
            </Panel>
          )}
          {visible.map((row) => {
            const svc = svcForTrip(row.serviceType);
            const rsc = svcMeta(svc);
            const rowChip = tripChip(row);
            const open = isOpenTrip(row);
            const active = t !== null && row.id === t.id;
            return (
              <div
                key={row.id}
                onClick={() => setSelectedId(row.id)}
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: 7,
                  padding: "12px 14px",
                  marginBottom: 5,
                  ...rowSurface(active, rsc.accent),
                }}
              >
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
                  <ServiceChip svc={svc} />
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{row.tripNumber}</span>
                </div>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 10 }}>
                  <div style={{ minWidth: 0 }}>
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 13,
                        fontWeight: 600,
                        color: colors.textPrimary,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {corridorLabel(row)}
                    </div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim, marginTop: 2 }}>
                      {shortDateLabel(row.serviceDate)} · {tripWindowLabel(row)} · {row.distanceKm} km
                    </div>
                  </div>
                  <StatusChip kind={rowChip.kind} label={rowChip.label} />
                </div>
                <div
                  style={{
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    fontWeight: open ? 600 : 500,
                    color: open ? colors.amberText : colors.textSecondary,
                  }}
                >
                  {row.driverName ?? "OPEN — needs coverage"} ·{" "}
                  <span style={{ color: colors.textDim, fontWeight: 400 }}>{row.vehicleUnit ?? "unassigned"}</span>
                </div>
              </div>
            );
          })}
        </div>

        {/* DETAIL */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {t === null ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
              Select a trip to see its detail.
            </div>
          ) : (
            <div className="detailfade" key={t.id}>
              {/* header */}
              <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 6 }}>
                <ServiceChip svc={svcForTrip(t.serviceType)} />
                {chip && <StatusChip kind={chip.kind} label={chip.label} />}
                <span style={{ marginLeft: "auto", fontFamily: fonts.mono, fontSize: 13, color: colors.skyBlue }}>{t.tripNumber}</span>
              </div>
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1.05, color: colors.headingBright, margin: "6px 0 4px" }}>
                {corridorLabel(t)}
              </h2>
              <div style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.textMuted, marginBottom: 16 }}>
                {shortDateLabel(t.serviceDate)} · {tripWindowLabel(t)} · {t.distanceKm} km
                {t.clientName ? ` · ${t.clientName}` : ""}
              </div>

              <CorridorStepper stops={stopNames(t)} />

              {actionError && (
                <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
                  <StatusChip kind="over" label={actionError} />
                </Panel>
              )}

              {/* two column blocks */}
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
                <Panel>
                  <SectionLabel>Assignment</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow
                      label="Driver"
                      value={t.driverName ?? "OPEN — needs coverage"}
                      valueStyle={
                        isOpenTrip(t)
                          ? { color: colors.amberText, fontWeight: 600 }
                          : { color: colors.textSecondary, fontWeight: 500 }
                      }
                    />
                    <DetailRow label="Vehicle" value={t.vehicleUnit ?? "unassigned"} />
                    <DetailRow
                      label="Clearance"
                      value={
                        !t.driverId || !t.clientName ? (
                          "—"
                        ) : clearance === null ? (
                          "checking…"
                        ) : clearance ? (
                          <StatusChip kind="ontime" label={`${t.clientName} clearance on file`} />
                        ) : (
                          <StatusChip kind="soon" label={`No ${t.clientName} clearance`} />
                        )
                      }
                    />
                  </div>
                </Panel>
                <Panel>
                  <SectionLabel>Manifest &amp; demand</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow label="Seats confirmed" value={seatsLabel(t)} valueStyle={{ fontFamily: fonts.mono }} />
                    <DetailRow
                      label="Demand"
                      value={
                        t.demandGuaranteed ? (
                          <StatusChip kind="ontime" label="Guaranteed" />
                        ) : t.seatsMinimum != null ? (
                          t.seatsConfirmed >= t.seatsMinimum ? (
                            <StatusChip kind="ontime" label="Viable" />
                          ) : (
                            <StatusChip kind="soon" label={`Needs ${t.seatsMinimum - t.seatsConfirmed} more`} />
                          )
                        ) : (
                          "No minimum"
                        )
                      }
                    />
                    <DetailRow
                      label="NL-TM-01"
                      value={
                        t.manifestId ? <StatusChip kind="ontime" label="Manifest on file" /> : "Not yet submitted"
                      }
                    />
                  </div>
                </Panel>
              </div>

              {/* billing */}
              <Panel style={{ marginBottom: 12 }}>
                <SectionLabel>Billing</SectionLabel>
                <div style={{ display: "grid", gridTemplateColumns: "repeat(4,1fr)", gap: 12 }}>
                  <div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Rate basis</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, fontWeight: 500 }}>
                      {contract ? contractRateLabel(contract) : t.clientId ? "No active contract" : "—"}
                    </div>
                  </div>
                  <div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>PO</div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>{t.poNumber ?? "—"}</div>
                  </div>
                  <div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Budget code</div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>
                      {contract?.budgetCode ?? "—"}
                    </div>
                  </div>
                  <div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Invoice</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("soon").t, fontWeight: 500 }}>
                      Not yet drafted
                    </div>
                  </div>
                </div>
              </Panel>

              {/* timeline */}
              <Panel style={{ marginBottom: 16 }}>
                <SectionLabel>Timeline &amp; audit</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
                  {timeline.map((ev, i) => (
                    <div
                      key={`${ev.label}-${i}`}
                      style={{
                        display: "flex",
                        gap: 11,
                        alignItems: "flex-start",
                        paddingBottom: i < timeline.length - 1 ? 11 : 0,
                        borderLeft: i < timeline.length - 1 ? `1.5px solid ${colors.border}` : "1.5px solid transparent",
                        marginLeft: 5,
                        paddingLeft: 14,
                        position: "relative",
                      }}
                    >
                      <span
                        style={{
                          position: "absolute",
                          left: -5,
                          top: 1,
                          width: 9,
                          height: 9,
                          borderRadius: "50%",
                          background: ev.state === "active" ? colors.blue : statusMeta("ontime").c,
                        }}
                      />
                      <div style={{ flex: 1, display: "flex", justifyContent: "space-between" }}>
                        <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>{ev.label}</span>
                        <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{ev.time}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </Panel>

              {/* actions */}
              <div style={{ display: "flex", flexWrap: "wrap", gap: 9 }}>
                {t.status === "Scheduled" && (
                  <ActionButton variant="primary" onClick={() => setModal("edit")}>
                    EDIT TRIP
                  </ActionButton>
                )}
                {(t.status === "Scheduled" || t.status === "InProgress") && (
                  <ActionButton onClick={() => setModal("assign")}>REASSIGN</ActionButton>
                )}
                {t.status === "Scheduled" && t.driverId && (
                  <ActionButton
                    variant="success"
                    onClick={() => onChangeStatus(t.id, "InProgress")}
                    disabled={busy}
                  >
                    {busy ? "WORKING…" : "START TRIP"}
                  </ActionButton>
                )}
                {t.status === "InProgress" && (
                  <ActionButton
                    variant="success"
                    onClick={() => onChangeStatus(t.id, "Completed")}
                    disabled={busy}
                  >
                    {busy ? "WORKING…" : "COMPLETE TRIP"}
                  </ActionButton>
                )}
                {manifest && (
                  <ActionButton onClick={() => printTripManifest(manifest)}>PRINT TRIP MANIFEST</ActionButton>
                )}
                {(t.status === "Scheduled" || t.status === "InProgress") && (
                  <ActionButton variant="destructive" onClick={() => setModal("cancel")}>
                    CANCEL
                  </ActionButton>
                )}
              </div>

              {modal === "assign" && (
                <AssignModal
                  trip={t}
                  onClose={() => setModal(null)}
                  onSaved={(driverId, vehicleUnit) => onAssignSaved(t.id, driverId, vehicleUnit)}
                />
              )}
              {modal === "edit" && (
                <EditTripModal trip={t} onClose={() => setModal(null)} onSaved={(input) => onEditSaved(t.id, input)} />
              )}
              {modal === "cancel" && (
                <CancelTripModal
                  trip={t}
                  onClose={() => setModal(null)}
                  onConfirmed={async (reason) => {
                    await changeTripStatus(t.id, "Cancelled", reason);
                    await reloadUntil(t.id, (trip) => trip !== undefined && trip.status === "Cancelled");
                  }}
                />
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
