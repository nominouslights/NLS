"use client";

import { useEffect, useState } from "react";
import { colors, fonts, statusMeta, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createTrip,
  hasClearanceFor,
  hhmm,
  stopNames,
  svcForTrip,
  todayIso,
  type TripInput,
  type TripServiceType,
  listRoutes,
  type RouteRecord,
} from "@/lib/api/trips";
import {
  listClients,
  SERVICE_TYPE_LABELS,
  contractRateLabel,
  type ClientRecord,
} from "@/lib/api/clients";
import {
  listDrivers,
  listDriverClearances,
  type DriverClearanceRecord,
  type DriverRecord,
} from "@/lib/api/drivers";
import { ModalError } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextField, TimeField } from "@/components/ui/Field";

// Create Trip — the 6-step wizard, now a real form submitting POST /api/trips.
// Client/rate lookups come from the Clients API (active-contract summary),
// route snapshots from the Routes API, and the driver picker carries a UI-side
// clearance warning (never a hard block). The trip number is generated
// server-side; on success the new trip opens in the Trips screen.

const STEP_LABELS = [
  "Service & client",
  "Corridor & schedule",
  "Vehicle & driver",
  "Passengers / demand",
  "Billing",
  "Review & create",
];

const SERVICE_TYPES = Object.keys(SERVICE_TYPE_LABELS) as TripServiceType[];

const rateFmt = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export default function CreateTripWizard({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: (tripId: string) => void;
}) {
  const [step, setStep] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // ---- reference data ----
  const [clients, setClients] = useState<ClientRecord[] | null>(null);
  const [routes, setRoutes] = useState<RouteRecord[] | null>(null);
  const [drivers, setDrivers] = useState<DriverRecord[] | null>(null);
  const [refError, setRefError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([listClients(), listRoutes(), listDrivers()]).then(
      ([c, r, d]) => {
        if (active) {
          setClients(c);
          setRoutes(r.filter((x) => x.active));
          setDrivers(d.filter((x) => x.status === "Active"));
        }
      },
      (e) => {
        if (active) setRefError(e instanceof ApiError ? e.message : "Failed to load reference data.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  // ---- form state ----
  const [serviceType, setServiceType] = useState<TripServiceType>("ContractCrew");
  const [clientId, setClientId] = useState("");
  const [poNumber, setPoNumber] = useState("");
  const [poTouched, setPoTouched] = useState(false);

  const [routeId, setRouteId] = useState(""); // "" = free-form corridor
  const [origin, setOrigin] = useState("");
  const [destination, setDestination] = useState("");
  const [distanceKm, setDistanceKm] = useState("");
  const [serviceDate, setServiceDate] = useState(todayIso());
  const [windowStart, setWindowStart] = useState("");
  const [windowEnd, setWindowEnd] = useState("");

  const [vehicleUnit, setVehicleUnit] = useState("");
  const [driverId, setDriverId] = useState<string | null>(null);
  const [clearances, setClearances] = useState<{ driverId: string; rows: DriverClearanceRecord[] } | null>(null);

  const [seatsCapacity, setSeatsCapacity] = useState("");
  const [seatsMinimum, setSeatsMinimum] = useState(serviceType === "Community" ? "4" : "");

  const client = clients?.find((c) => c.id === clientId) ?? null;
  const route = routes?.find((r) => r.id === routeId) ?? null;
  const driver = drivers?.find((d) => d.id === driverId) ?? null;
  const contract = client?.activeContract ?? null;

  // The PO defaults from the client's active contract until the dispatcher
  // edits it — a derived value, not an effect.
  const effectivePo = poTouched ? poNumber : contract?.defaultPoNumber ?? "";

  // Clearances for the picked driver (UI-side warning only).
  useEffect(() => {
    if (!driverId) return;
    let active = true;
    listDriverClearances(driverId).then(
      (rows) => {
        if (active) setClearances({ driverId, rows });
      },
      () => {
        if (active) setClearances({ driverId, rows: [] });
      },
    );
    return () => {
      active = false;
    };
  }, [driverId]);

  const driverCleared =
    driver && client && clearances?.driverId === driver.id
      ? hasClearanceFor(clearances.rows, client.name)
      : null;

  const corridor = route ? stopNames(route) : [origin, destination].filter((s) => s.trim());
  const km = route ? route.distanceKm : Number(distanceKm) || 0;

  const estBillable =
    contract?.billingModel === "RoundTripRate" && contract.ratePerRoundTripCad != null
      ? rateFmt.format(contract.ratePerRoundTripCad) // 1 round trip, display only
      : null;

  // ---- step validation ----
  function validateStep(n: number): string | null {
    if (n === 1) {
      if (serviceType === "ContractCrew" && !clientId) return "Contract crew runs need a client.";
      return null;
    }
    if (n === 2) {
      if (!routeId) {
        if (!origin.trim() || !destination.trim()) return "Pick a route or enter the free-form origin and destination.";
        const d = Number(distanceKm);
        if (!Number.isInteger(d) || d <= 0) return "Enter the corridor distance in whole km.";
      }
      if (!serviceDate) return "Enter the service date.";
      if (!windowStart) return "Enter the departure window start.";
      return null;
    }
    if (n === 4) {
      const cap = seatsCapacity === "" ? null : Number(seatsCapacity);
      if (cap !== null && (!Number.isInteger(cap) || cap <= 0)) return "Seats capacity must be a whole number.";
      const min = seatsMinimum === "" ? null : Number(seatsMinimum);
      if (min !== null && (!Number.isInteger(min) || min < 0)) return "Seats minimum must be a whole number.";
      return null;
    }
    return null;
  }

  function next() {
    const problem = validateStep(step);
    if (problem) {
      setError(problem);
      return;
    }
    setError(null);
    setStep((s) => Math.min(6, s + 1));
  }

  async function submit() {
    if (busy) return;
    for (const n of [1, 2, 4]) {
      const problem = validateStep(n);
      if (problem) {
        setError(problem);
        return;
      }
    }
    const input: TripInput = {
      serviceDate,
      windowStart,
      windowEnd: windowEnd || null,
      serviceType,
      routeId: route?.id ?? null,
      // Free-form corridor (ignored when routeId is set — the backend
      // snapshots the route's fields instead).
      routeName: route ? null : `${origin.trim()} → ${destination.trim()}`,
      origin: route ? null : origin.trim(),
      destination: route ? null : destination.trim(),
      stops: route
        ? null
        : [
            { name: origin.trim(), order: 1 },
            { name: destination.trim(), order: 2 },
          ],
      distanceKm: route ? route.distanceKm : Number(distanceKm),
      direction: null,
      isEmptyLeg: false,
      clientId: client?.id ?? null,
      clientName: client?.name ?? null,
      poNumber: effectivePo.trim() || null,
      driverId,
      vehicleUnit: vehicleUnit.trim() || null,
      seatsCapacity: seatsCapacity === "" ? null : Number(seatsCapacity),
      seatsMinimum: seatsMinimum === "" ? null : Number(seatsMinimum),
    };

    setBusy(true);
    setError(null);
    try {
      const id = await createTrip(input);
      onCreated(id);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to create the trip — please try again.");
      setBusy(false);
    }
  }

  const reviewRows: [string, string][] = [
    ["Service", SERVICE_TYPE_LABELS[serviceType] + (client ? ` · ${client.name}` : "")],
    ["Corridor", corridor.length >= 2 ? corridor.join(" → ") : "—"],
    ["Schedule", `${serviceDate} · ${windowStart || "—"}${windowEnd ? ` → ${windowEnd}` : ""}`],
    ["Vehicle", vehicleUnit.trim() || "unassigned"],
    ["Driver", driver ? driver.name : "OPEN — claimable"],
    ["Billing", `${effectivePo.trim() || "no PO"}${contract?.budgetCode ? ` · ${contract.budgetCode}` : ""}`],
  ];

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 100,
        background: colors.scrim,
        backdropFilter: "blur(3px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 26,
      }}
      className="detailfade"
    >
      <div
        style={{
          width: "100%",
          maxWidth: 1080,
          height: "88vh",
          background: colors.mainBg,
          border: `1px solid ${colors.borderStrong}`,
          borderRadius: 16,
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
          boxShadow: colors.shadowPop,
        }}
      >
        {/* header */}
        <div style={{ flex: "none", display: "flex", alignItems: "center", padding: "18px 24px", borderBottom: `1px solid ${colors.border}` }}>
          <div>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 10,
                letterSpacing: ".16em",
                textTransform: "uppercase",
                color: colors.textFaint,
                marginBottom: 2,
              }}
            >
              Operations · POST /api/trips
            </div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.headingBright, lineHeight: 1 }}>Create Trip</div>
          </div>
          <div
            onClick={onClose}
            style={{
              marginLeft: "auto",
              width: 34,
              height: 34,
              borderRadius: 8,
              border: `1px solid ${colors.borderStrong}`,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: colors.textMuted,
              cursor: "pointer",
              fontSize: 18,
            }}
          >
            ✕
          </div>
        </div>

        {/* progress */}
        <div style={{ flex: "none", display: "flex", alignItems: "center", gap: 0, padding: "16px 24px", borderBottom: `1px solid ${colors.border}`, overflowX: "auto" }}>
          {STEP_LABELS.map((label, i) => {
            const n = i + 1;
            return (
              <div key={label} style={{ display: "flex", alignItems: "center", gap: 9, flex: "none" }}>
                {n > 1 && <span style={{ width: 26, height: 1.5, background: colors.border, margin: "0 4px" }} />}
                <div onClick={() => setStep(n)} style={{ display: "flex", alignItems: "center", gap: 8, cursor: "pointer", paddingRight: 14 }}>
                  <span
                    style={{
                      width: 26,
                      height: 26,
                      flex: "none",
                      borderRadius: "50%",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 12,
                      background: n < step ? "#009E73" : n === step ? colors.blue : colors.cardBg,
                      color: n < step ? "#FFFFFF" : n === step ? "#FFFFFF" : colors.textDim,
                      border: n > step ? `1px solid ${colors.border}` : undefined,
                    }}
                  >
                    {n}
                  </span>
                  <span
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 12,
                      fontWeight: n === step ? 600 : 500,
                      color: n === step ? colors.headingBright : n < step ? colors.textMuted : colors.textDim,
                      whiteSpace: "nowrap",
                    }}
                  >
                    {label}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        {/* body */}
        <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "1fr 300px" }}>
          <div style={{ minHeight: 0, overflowY: "auto", padding: "26px 30px" }}>
            {refError && <ModalError message={`Reference data unavailable — ${refError}`} />}
            {error && <ModalError message={error} />}

            {step === 1 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.headingBright, margin: "0 0 4px" }}>
                  Service type &amp; client
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted, margin: "0 0 18px" }}>
                  Contract crew runs require a client; the PO defaults from the client&rsquo;s active contract.
                </p>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 10, marginBottom: 18 }}>
                  {SERVICE_TYPES.map((st) => {
                    const meta = svcMeta(svcForTrip(st));
                    const active = st === serviceType;
                    return (
                      <div
                        key={st}
                        onClick={() => setServiceType(st)}
                        style={{
                          padding: 14,
                          borderRadius: 10,
                          background: active ? meta.chipBg : colors.cardBg,
                          border: `1px solid ${active ? meta.chipBd : colors.border}`,
                          boxShadow: colors.shadowCard,
                          cursor: "pointer",
                        }}
                      >
                        <div style={{ color: meta.accent, fontWeight: 800, marginBottom: 6 }}>{meta.glyph}</div>
                        <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 13, color: active ? colors.textPrimary : colors.textSecondary }}>
                          {meta.label}
                        </div>
                      </div>
                    );
                  })}
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
                  <SelectField
                    label={serviceType === "ContractCrew" ? "Client · required for contract crew" : "Client (optional)"}
                    value={clientId}
                    onChange={setClientId}
                    options={[
                      { value: "", label: clients === null ? "Loading clients…" : "— none —" },
                      ...(clients ?? []).map((c) => ({ value: c.id, label: c.name })),
                    ]}
                  />
                  <TextField
                    label="PO number"
                    value={effectivePo}
                    onChange={(v) => {
                      setPoTouched(true);
                      setPoNumber(v);
                    }}
                    mono
                    placeholder="PO-AG-2261"
                    hint={
                      contract?.defaultPoNumber && !poTouched ? (
                        <span style={{ color: colors.textFaint }}>· defaulted from the active contract</span>
                      ) : undefined
                    }
                  />
                </div>
              </div>
            )}

            {step === 2 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.headingBright, margin: "0 0 4px" }}>
                  Corridor &amp; schedule
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted, margin: "0 0 18px" }}>
                  Picking a route snapshots its corridor onto the trip; free-form is for one-off runs.
                </p>
                <div style={{ marginBottom: 14 }}>
                  <SelectField
                    label="Route"
                    value={routeId}
                    onChange={setRouteId}
                    options={[
                      { value: "", label: "— free-form corridor —" },
                      ...(routes ?? []).map((r) => ({
                        value: r.id,
                        label: `${r.name} · ${r.distanceKm} km`,
                      })),
                    ]}
                  />
                </div>
                {route ? (
                  <div
                    style={{
                      padding: "12px 15px",
                      background: colors.cardBg,
                      border: `1px solid ${colors.border}`,
                      borderRadius: 10,
                      boxShadow: colors.shadowCard,
                      marginBottom: 14,
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      color: colors.textSecondary,
                    }}
                  >
                    {stopNames(route).join("  →  ")}
                    <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim, marginLeft: 8 }}>
                      {route.distanceKm} km{route.requiredLicenceClass ? ` · ${route.requiredLicenceClass} required` : ""}
                    </span>
                  </div>
                ) : (
                  <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12, marginBottom: 14 }}>
                    <TextField label="Origin" value={origin} onChange={setOrigin} placeholder="Thompson" />
                    <TextField label="Destination" value={destination} onChange={setDestination} placeholder="Lynn Lake" />
                    <NumberField label="Distance (km)" value={distanceKm} onChange={setDistanceKm} min={1} step={1} />
                  </div>
                )}
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
                  <DateField label="Service date" value={serviceDate} onChange={setServiceDate} />
                  <TimeField label="Window start" value={windowStart} onChange={setWindowStart} />
                  <TimeField label="Window end (optional)" value={windowEnd} onChange={setWindowEnd} />
                </div>
              </div>
            )}

            {step === 3 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.headingBright, margin: "0 0 4px" }}>
                  Vehicle &amp; driver
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted, margin: "0 0 18px" }}>
                  Leave the driver open (claimable) or lock to a named driver — clearance mismatches warn, never block.
                </p>
                <div style={{ marginBottom: 18, maxWidth: 320 }}>
                  <TextField label="Vehicle unit" value={vehicleUnit} onChange={setVehicleUnit} mono placeholder="U-02" />
                </div>
                <div
                  style={{
                    fontFamily: fonts.body,
                    fontSize: 11.5,
                    color: colors.textLabel,
                    marginBottom: 5,
                  }}
                >
                  Driver · Active roster
                </div>
                <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <div
                    onClick={() => setDriverId(null)}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 10,
                      padding: "10px 13px",
                      borderRadius: 9,
                      background: driverId === null ? "rgba(232,160,32,.1)" : colors.cardBg,
                      border: `1px solid ${driverId === null ? colors.amber : colors.border}`,
                      boxShadow: colors.shadowCard,
                      cursor: "pointer",
                    }}
                  >
                    <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 700, color: colors.amberText }}>Open</span>
                    <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textMuted }}>
                      claimable by any eligible driver
                    </span>
                  </div>
                  {drivers === null && (
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading drivers…</div>
                  )}
                  {(drivers ?? []).map((d) => {
                    const active = driverId === d.id;
                    const cleared = active ? driverCleared : null;
                    return (
                      <div
                        key={d.id}
                        onClick={() => setDriverId(d.id)}
                        style={{
                          display: "flex",
                          alignItems: "center",
                          gap: 10,
                          padding: "10px 13px",
                          borderRadius: 9,
                          background: active ? colors.cardBgActive : colors.cardBg,
                          border: `1px solid ${active ? colors.borderActive : colors.border}`,
                          boxShadow: colors.shadowCard,
                          cursor: "pointer",
                        }}
                      >
                        <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textPrimary, fontWeight: 500 }}>{d.name}</span>
                        <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{d.licenceClass}</span>
                        {active && client && cleared !== null && (
                          <span
                            style={{
                              marginLeft: "auto",
                              fontFamily: fonts.body,
                              fontSize: 11,
                              fontWeight: 600,
                              color: cleared ? statusMeta("ontime").t : statusMeta("soon").t,
                            }}
                          >
                            {cleared ? `✓ ${client.name} clearance` : `◐ no ${client.name} clearance`}
                          </span>
                        )}
                      </div>
                    );
                  })}
                </div>
              </div>
            )}

            {step === 4 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.headingBright, margin: "0 0 4px" }}>
                  Passengers / demand
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted, margin: "0 0 18px" }}>
                  Capacity and (for community runs) the viability minimum. Seat confirmations happen on the Manifests
                  &amp; Demand screen once the trip exists.
                </p>
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, maxWidth: 480, marginBottom: 14 }}>
                  <NumberField label="Seats capacity" value={seatsCapacity} onChange={setSeatsCapacity} min={1} step={1} placeholder="24" />
                  <NumberField
                    label="Seats minimum (viability)"
                    value={seatsMinimum}
                    onChange={setSeatsMinimum}
                    min={0}
                    step={1}
                    placeholder={serviceType === "Community" ? "4" : ""}
                    hint={
                      serviceType === "Community" ? (
                        <span style={{ color: colors.amberText }}>· community runs use a 4-passenger minimum</span>
                      ) : undefined
                    }
                  />
                </div>
                <div
                  style={{
                    padding: "12px 15px",
                    background: "rgba(31,111,178,.07)",
                    border: "1px solid rgba(31,111,178,.25)",
                    borderRadius: 10,
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    color: colors.textMuted,
                    lineHeight: 1.5,
                  }}
                >
                  Gift-a-Seat guarantees and NIHB vouchers are recorded against the trip&rsquo;s demand after creation.
                  Mixing rule: community passengers cannot be added while mine crew are aboard.
                </div>
              </div>
            )}

            {step === 5 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.headingBright, margin: "0 0 4px" }}>Billing</h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted, margin: "0 0 18px" }}>
                  Read from the client&rsquo;s active contract — invoices draft from completed trips at these terms.
                </p>
                <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
                  <div style={{ display: "flex", justifyContent: "space-between", padding: "12px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted }}>Rate basis</span>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textPrimary, fontWeight: 500 }}>
                      {contract ? contractRateLabel(contract) : client ? "No active contract" : "No client — manual billing"}
                    </span>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", padding: "12px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted }}>PO number</span>
                    <span style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.textPrimary }}>{effectivePo.trim() || "—"}</span>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", padding: "12px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted }}>Budget code (ZBB)</span>
                    <span style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.textPrimary }}>{contract?.budgetCode ?? "—"}</span>
                  </div>
                  <div style={{ display: "flex", justifyContent: "space-between", padding: "12px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted }}>GST</span>
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: contract?.gstApplicable ? statusMeta("ontime").t : colors.textPrimary, fontWeight: 500 }}>
                      {contract ? (contract.gstApplicable ? "5% · no PST on transportation" : "Not applicable") : "—"}
                    </span>
                  </div>
                </div>
              </div>
            )}

            {step === 6 && (
              <div className="detailfade">
                <h3 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.headingBright, margin: "0 0 4px" }}>
                  Review &amp; create
                </h3>
                <p style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textMuted, margin: "0 0 18px" }}>
                  Created as Scheduled; the trip number is generated by the backend.
                </p>
                <div style={{ padding: 18, background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 12, boxShadow: colors.shadowCard, display: "flex", flexDirection: "column", gap: 11 }}>
                  {reviewRows.map(([label, value], i) => (
                    <div key={label} style={{ display: "flex", justifyContent: "space-between", fontFamily: fonts.body, fontSize: 13, gap: 12 }}>
                      <span style={{ color: colors.textDim }}>{label}</span>
                      <span
                        style={{
                          color: colors.textPrimary,
                          fontWeight: 500,
                          fontFamily: i === 2 || i === 5 ? fonts.mono : fonts.body,
                          fontSize: i === 2 || i === 5 ? 12 : 13,
                          textAlign: "right",
                        }}
                      >
                        {value}
                      </span>
                    </div>
                  ))}
                </div>
                {driver && client && driverCleared === false && (
                  <div
                    style={{
                      marginTop: 12,
                      padding: "11px 14px",
                      background: statusMeta("soon").bg,
                      border: `1px solid ${statusMeta("soon").bd}`,
                      borderRadius: 10,
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      fontWeight: 600,
                      color: statusMeta("soon").t,
                    }}
                  >
                    ◐ {driver.name} has no {client.name} clearance on file — the trip will still be created.
                  </div>
                )}
              </div>
            )}
          </div>

          {/* summary rail */}
          <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 20px", background: colors.railBg, borderLeft: `1px solid ${colors.border}` }}>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 9.5,
                letterSpacing: ".14em",
                textTransform: "uppercase",
                color: colors.textFaint,
                marginBottom: 12,
              }}
            >
              Live summary
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Service</div>
                <div style={{ fontFamily: fonts.body, fontSize: 13, color: svcMeta(svcForTrip(serviceType)).chipTx, fontWeight: 600 }}>
                  {SERVICE_TYPE_LABELS[serviceType]}
                </div>
              </div>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Client</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary }}>{client?.name ?? "—"}</div>
              </div>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Corridor</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary }}>
                  {corridor.length >= 2 ? `${corridor[0]} → ${corridor[corridor.length - 1]} · ${km} km` : "—"}
                </div>
              </div>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Schedule</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textPrimary }}>
                  {serviceDate}
                  {windowStart ? ` · ${hhmm(windowStart)}` : ""}
                </div>
              </div>
              <div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Vehicle · driver</div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: driver ? colors.textPrimary : colors.amberText }}>
                  {vehicleUnit.trim() || "no unit"} · {driver ? driver.name : "OPEN"}
                </div>
              </div>
              <div style={{ paddingTop: 12, borderTop: `1px solid ${colors.border}` }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>
                  Est. billable (CAD) · 1 round trip
                </div>
                <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright, fontVariantNumeric: "tabular-nums" }}>
                  {estBillable ?? "—"}
                </div>
                {!estBillable && (
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim, marginTop: 3 }}>
                    No per-round-trip rate on the client&rsquo;s contract — billed via manual lines.
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* footer */}
        <div style={{ flex: "none", display: "flex", alignItems: "center", justifyContent: "space-between", padding: "16px 24px", borderTop: `1px solid ${colors.border}` }}>
          <span
            onClick={() => setStep((s) => Math.max(1, s - 1))}
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 13,
              letterSpacing: ".04em",
              padding: "9px 18px",
              borderRadius: 9,
              background: "transparent",
              border: `1px solid ${colors.borderActive}`,
              color: colors.textSecondary,
              cursor: "pointer",
            }}
          >
            BACK
          </span>
          <div style={{ display: "flex", gap: 10 }}>
            <span
              onClick={onClose}
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 13,
                letterSpacing: ".04em",
                padding: "9px 18px",
                borderRadius: 9,
                background: "transparent",
                color: colors.textDim,
                cursor: "pointer",
              }}
            >
              CANCEL
            </span>
            {step < 6 ? (
              <span
                onClick={next}
                style={{
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 13,
                  letterSpacing: ".04em",
                  padding: "9px 22px",
                  borderRadius: 9,
                  background: colors.blue,
                  color: "#FFFFFF",
                  cursor: "pointer",
                }}
              >
                CONTINUE →
              </span>
            ) : (
              <span
                onClick={submit}
                style={{
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 13,
                  letterSpacing: ".04em",
                  padding: "9px 22px",
                  borderRadius: 9,
                  background: busy ? colors.borderStrong : "#007A59",
                  color: "#FFFFFF",
                  cursor: busy ? "wait" : "pointer",
                }}
              >
                {busy ? "CREATING…" : "CREATE TRIP"}
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
