"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { dtcAlerts, fleet as mockFleet, fuelStats, partsInventory, pmReminders } from "@/lib/data";
import {
  ApiError,
  canDispose,
  canTransition,
  changeVehicleStatus,
  disposeVehicle,
  formatCad,
  formatKm,
  formatUtcDate,
  isDisposed,
  lifeKindFor,
  listVehicles,
  recordOdometer,
  statusKindFor,
  statusLabelFor,
  type Vehicle,
} from "@/lib/api";
import { DetailRow, PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { NumberField, TextField } from "@/components/ui/Field";
import VehicleFormModal from "@/components/VehicleFormModal";
import RetirementCertificateModal from "@/components/RetirementCertificateModal";

const TABS = ["Overview", "Maintenance", "DTC Alerts", "Parts", "Fuel & Route"];

type PromptKind = "oos" | "odometer" | "retire" | "sell" | "recycle" | null;

function PreviewCaption({ children }: { children?: string }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
      <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        {children ?? "Preview — not yet wired to backend."}
      </span>
    </div>
  );
}

function EmptyTabNote({ children }: { children: string }) {
  return (
    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
      {children}
    </div>
  );
}

export default function Fleet({
  fleetSelId,
  setFleetSelId,
}: {
  fleetSelId: string | null;
  setFleetSelId: (id: string | null) => void;
}) {
  const [vehicles, setVehicles] = useState<Vehicle[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [tab, setTab] = useState(0);

  const [modal, setModal] = useState<"register" | "edit" | null>(null);
  const [certOpen, setCertOpen] = useState(false);

  const [prompt, setPrompt] = useState<PromptKind>(null);
  const [reasonInput, setReasonInput] = useState("");
  const [odoInput, setOdoInput] = useState("");
  const [priceInput, setPriceInput] = useState("");

  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [autoRetired, setAutoRetired] = useState(false);

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      setVehicles(await listVehicles());
    } catch (e) {
      setVehicles(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load fleet.");
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const sel = vehicles?.find((v) => v.id === fleetSelId) ?? vehicles?.[0] ?? null;

  function selectVehicle(id: string) {
    setFleetSelId(id);
    setPrompt(null);
    setActionError(null);
    setAutoRetired(false);
  }

  async function runAction(fn: () => Promise<void>, opts?: { detectAutoRetire?: boolean }) {
    if (!sel || busy) return;
    const selId = sel.id;
    const prevStatus = sel.status;
    setBusy(true);
    setActionError(null);
    try {
      await fn();
      const fresh = await listVehicles();
      setVehicles(fresh);
      if (opts?.detectAutoRetire) {
        const updated = fresh.find((v) => v.id === selId);
        if (updated && prevStatus !== "Retired" && updated.status === "Retired") {
          setAutoRetired(true);
        }
      }
      setPrompt(null);
      setReasonInput("");
      setOdoInput("");
      setPriceInput("");
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Action failed — please try again.");
    } finally {
      setBusy(false);
    }
  }

  // -------------------------------------------------------------------------
  // Load / error / empty states
  // -------------------------------------------------------------------------

  if (loadError) {
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
        <div style={{ flex: "none", padding: "20px 26px 12px" }}>
          <PageHeader eyebrow="Operations · Vehicle registry · Fleet API" title="Fleet" />
        </div>
        <div style={{ padding: "26px", maxWidth: 560 }}>
          <Panel borderColor="rgba(213,94,0,.4)">
            <div style={{ display: "flex", gap: 11, alignItems: "center", marginBottom: 12 }}>
              <span
                style={{
                  width: 22,
                  height: 22,
                  flex: "none",
                  borderRadius: 6,
                  background: "#D55E00",
                  color: "#fff",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: 12,
                  fontWeight: 800,
                }}
              >
                ▲
              </span>
              <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: statusMeta("over").t }}>
                Fleet registry unavailable
              </span>
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
              {loadError}
            </div>
            <ActionButton variant="primary" onClick={load}>
              RETRY
            </ActionButton>
          </Panel>
        </div>
      </div>
    );
  }

  if (vehicles === null) {
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
        <div style={{ flex: "none", padding: "20px 26px 12px" }}>
          <PageHeader eyebrow="Operations · Vehicle registry · Fleet API" title="Fleet" />
        </div>
        <div style={{ padding: "16px 26px" }}>
          {[0, 1, 2, 3, 4].map((i) => (
            <div
              key={i}
              style={{
                height: 62,
                borderRadius: 9,
                border: `1px solid ${colors.borderSubtle}`,
                background: colors.cardBg,
                marginBottom: 6,
                opacity: 0.55 - i * 0.08,
              }}
            />
          ))}
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
            Loading fleet from API…
          </div>
        </div>
      </div>
    );
  }

  if (vehicles.length === 0) {
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
        <div style={{ flex: "none", padding: "20px 26px 12px" }}>
          <PageHeader eyebrow="Operations · Vehicle registry · Fleet API" title="Fleet" />
        </div>
        <div style={{ padding: "26px", maxWidth: 560 }}>
          <Panel>
            <SectionLabel>No vehicles registered</SectionLabel>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
              The vehicle registry is empty for this tenant. Register the first vehicle to make it
              available for trips and DVIRs.
            </div>
            <ActionButton variant="primary" onClick={() => setModal("register")}>
              + REGISTER VEHICLE
            </ActionButton>
          </Panel>
        </div>
        {modal === "register" && (
          <VehicleFormModal vehicle={null} onClose={() => setModal(null)} onSaved={load} />
        )}
      </div>
    );
  }

  // -------------------------------------------------------------------------
  // Loaded — master list + detail pane
  // -------------------------------------------------------------------------

  const f = sel!;
  const kind = statusKindFor(f.status);
  const label = statusLabelFor(f.status);
  const oos = f.status === "OutOfService";
  const disposed = isDisposed(f.status);
  const retired = f.status === "Retired";
  const readOnly = disposed;
  const lifePct = Math.min(100, Math.max(0, f.lifeUsedPct));
  const lifeKind = lifeKindFor(f.lifeUsedPct);
  const lifeMeta = statusMeta(lifeKind);

  // Mock DVIR/inspection record for this unit (DVIR domain not built yet)
  const mock = mockFleet.find((m) => m.unit === f.unitNumber);
  const periodicText = f.requiresPeriodicInspection
    ? "Six-month NSC Standard 11 periodic inspection (11+ seats)"
    : "Trip-inspection / DVIR only — under 11 seats, no periodic requirement";

  const unitPm = pmReminders.filter((r) => r.unit === f.unitNumber);
  const unitDtc = dtcAlerts.filter((r) => r.unit === f.unitNumber);
  const unitFuel = fuelStats.filter((r) => r.unit === f.unitNumber);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader
          eyebrow="Operations · Vehicle registry · Fleet API"
          title="Fleet"
          right={
            <ActionButton variant="primary" onClick={() => setModal("register")}>
              + REGISTER VEHICLE
            </ActionButton>
          }
        />
      </div>
      <div
        style={{
          flex: 1,
          minHeight: 0,
          display: "grid",
          gridTemplateColumns: "40% 1fr",
          borderTop: `1px solid ${colors.border}`,
        }}
      >
        {/* master list (real API) */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {vehicles.map((row) => {
            const active = row.id === f.id;
            const rowKind = statusKindFor(row.status);
            const rowDim = row.status === "OutOfService" || isDisposed(row.status);
            return (
              <div
                key={row.id}
                onClick={() => selectVehicle(row.id)}
                style={{
                  display: "grid",
                  gridTemplateColumns: "56px 1fr 130px",
                  gap: 11,
                  alignItems: "center",
                  padding: "11px 13px",
                  marginBottom: 5,
                  opacity: rowDim ? 0.72 : 1,
                  ...rowSurface(active, colors.blue),
                }}
              >
                <div
                  style={{
                    width: 56,
                    height: 40,
                    borderRadius: 8,
                    background: colors.inputBg,
                    border: `1px solid ${colors.border}`,
                    display: "flex",
                    flexDirection: "column",
                    alignItems: "center",
                    justifyContent: "center",
                  }}
                >
                  <span style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.skyBlue, fontWeight: 500 }}>
                    {row.unitNumber}
                  </span>
                </div>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                    {row.make} {row.model}
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                    {row.seatingCapacity}-seat · {row.licencePlate}
                  </div>
                </div>
                <div>
                  <StatusChip kind={rowKind} label={statusLabelFor(row.status)} />
                </div>
              </div>
            );
          })}
        </div>

        {/* detail pane */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={f.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 14, marginBottom: 6 }}>
              <StatusChip kind={kind} label={label} />
              {readOnly && <MonoTag>READ-ONLY</MonoTag>}
              <span style={{ fontFamily: fonts.mono, fontSize: 13, color: colors.skyBlue, marginLeft: "auto" }}>
                {f.unitNumber}
              </span>
            </div>
            <h2
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 26,
                lineHeight: 1.05,
                color: colors.headingBright,
                margin: "6px 0 4px",
              }}
            >
              {f.year} {f.make} {f.model}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 14 }}>
              {f.seatingCapacity}-seat · {f.requiredLicenceClass} · {f.licencePlate} · VIN {f.vin}
            </div>

            {/* tab bar */}
            <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16 }}>
              {TABS.map((t, i) => (
                <span
                  key={t}
                  onClick={() => setTab(i)}
                  style={{
                    fontFamily: fonts.body,
                    fontWeight: tab === i ? 600 : 500,
                    fontSize: 12.5,
                    padding: "9px 14px",
                    color: tab === i ? colors.headingBright : colors.textDim,
                    borderBottom: tab === i ? `2px solid ${colors.blue}` : undefined,
                    marginBottom: -1,
                    cursor: "pointer",
                  }}
                >
                  {t}
                </span>
              ))}
            </div>

            {/* ------------------------------ OVERVIEW ------------------------------ */}
            {tab === 0 && (
              <div>
                {autoRetired && (
                  <div
                    style={{
                      padding: "13px 15px",
                      background: "rgba(213,94,0,.1)",
                      border: "1px solid rgba(213,94,0,.4)",
                      borderRadius: 10,
                      marginBottom: 14,
                      display: "flex",
                      gap: 11,
                      alignItems: "center",
                    }}
                  >
                    <span
                      style={{
                        width: 22,
                        height: 22,
                        flex: "none",
                        borderRadius: 6,
                        background: "#D55E00",
                        color: "#fff",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontSize: 12,
                        fontWeight: 800,
                      }}
                    >
                      ▲
                    </span>
                    <div style={{ fontFamily: fonts.body, fontSize: 13, color: statusMeta("over").t, fontWeight: 600 }}>
                      End of service life reached — vehicle retired. A retirement certificate has been issued.
                    </div>
                  </div>
                )}

                {oos && (
                  <div
                    style={{
                      padding: "13px 15px",
                      background: "rgba(213,94,0,.1)",
                      border: "1px solid rgba(213,94,0,.4)",
                      borderRadius: 10,
                      marginBottom: 14,
                      display: "flex",
                      gap: 11,
                      alignItems: "center",
                    }}
                  >
                    <span
                      style={{
                        width: 22,
                        height: 22,
                        flex: "none",
                        borderRadius: 6,
                        background: "#D55E00",
                        color: "#fff",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontSize: 12,
                        fontWeight: 800,
                      }}
                    >
                      ▲
                    </span>
                    <div style={{ fontFamily: fonts.body, fontSize: 13, color: statusMeta("over").t, fontWeight: 600 }}>
                      Out of service — removed from dispatch eligibility until cleared.
                      {f.statusReason ? ` Reason: ${f.statusReason}.` : ""}
                    </div>
                  </div>
                )}

                {actionError && (
                  <div
                    style={{
                      padding: "11px 14px",
                      background: "rgba(213,94,0,.1)",
                      border: "1px solid rgba(213,94,0,.4)",
                      borderRadius: 10,
                      marginBottom: 14,
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      color: statusMeta("over").t,
                      fontWeight: 600,
                    }}
                  >
                    ▲ {actionError}
                  </div>
                )}

                {/* End of Service Life panel — REAL data */}
                <Panel style={{ marginBottom: 12 }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 11 }}>
                    <SectionLabel>End of service life · km depreciation</SectionLabel>
                    <StatusChip
                      kind={lifeKind}
                      label={
                        f.lifeUsedPct >= 100
                          ? "Service life exhausted"
                          : `${Math.round(f.lifeUsedPct)}% life used`
                      }
                    />
                  </div>
                  <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginBottom: 10 }}>
                    <span
                      style={{
                        fontFamily: fonts.condensed,
                        fontWeight: 700,
                        fontSize: 28,
                        color: colors.headingBright,
                        fontVariantNumeric: "tabular-nums",
                      }}
                    >
                      {formatCad(f.currentValueCad)}
                    </span>
                    <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                      current value · acquired {formatCad(f.acquisitionCostCad)}
                    </span>
                  </div>
                  <div
                    style={{
                      height: 10,
                      borderRadius: 6,
                      background: colors.inputBg,
                      overflow: "hidden",
                      border: `1px solid ${colors.borderSubtle}`,
                    }}
                  >
                    <div style={{ height: "100%", width: `${lifePct}%`, background: lifeMeta.c, borderRadius: 6 }} />
                  </div>
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      fontFamily: fonts.body,
                      fontSize: 11,
                      color: colors.textDim,
                      marginTop: 7,
                    }}
                  >
                    <span>{formatKm(f.odometerKm)} of {formatKm(f.endOfLifeKm)}</span>
                    <span>{formatKm(f.remainingKm)} remaining · auto-retires at end-of-life km</span>
                  </div>

                  {(retired || disposed) && (
                    <div
                      style={{
                        marginTop: 13,
                        paddingTop: 12,
                        borderTop: `1px solid ${colors.borderSubtle}`,
                        display: "flex",
                        flexDirection: "column",
                        gap: 7,
                      }}
                    >
                      <DetailRow label="Lifecycle" value={disposed ? `Retired → ${f.status}` : "Retired — awaiting disposal (sell or recycle)"} />
                      {f.statusReason && <DetailRow label="Retirement reason" value={f.statusReason} />}
                      {f.status === "Sold" && (
                        <DetailRow
                          label="Sale price"
                          value={f.salePriceCad != null ? formatCad(f.salePriceCad) : "—"}
                          valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }}
                        />
                      )}
                      {disposed && <DetailRow label="Disposed" value={formatUtcDate(f.disposedAtUtc)} />}
                      <DetailRow label="Retirement certificate" value="On file — view via the actions below" />
                    </div>
                  )}
                </Panel>

                {/* registry details — REAL data */}
                <Panel style={{ marginBottom: 12 }}>
                  <SectionLabel>Registry record</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 7 }}>
                    <DetailRow label="VIN" value={f.vin} valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }} />
                    <DetailRow label="Licence plate" value={f.licencePlate} valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }} />
                    <DetailRow label="Required licence" value={f.requiredLicenceClass} />
                    <DetailRow
                      label="Odometer"
                      value={formatKm(f.odometerKm)}
                      valueStyle={{ fontFamily: fonts.mono, fontSize: 12 }}
                    />
                    <DetailRow label="Registered" value={formatUtcDate(f.registeredAtUtc)} />
                    <DetailRow label="Last updated" value={formatUtcDate(f.updatedAtUtc)} />
                  </div>
                </Panel>

                {/* DVIR / inspection — MOCK (DVIR domain not built) */}
                <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 14 }}>
                  <Panel>
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                      <SectionLabel>Trip inspection · DVIR</SectionLabel>
                      <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
                    </div>
                    {mock ? (
                      <StatusChip kind={mock.dk} label={mock.dvir} />
                    ) : (
                      <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
                        No DVIR record for {f.unitNumber}
                      </span>
                    )}
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 9, lineHeight: 1.5 }}>
                      NSC Standard 13 — per-trip inspection logged from the Driver Field App.
                    </div>
                  </Panel>
                  <Panel>
                    <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                      <SectionLabel>Periodic inspection</SectionLabel>
                      <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
                    </div>
                    {mock ? (
                      <StatusChip kind={mock.ik} label={mock.insp} />
                    ) : (
                      <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
                        No inspection record for {f.unitNumber}
                      </span>
                    )}
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 9, lineHeight: 1.5 }}>
                      {periodicText}.
                    </div>
                  </Panel>
                </div>

                {/* inline prompts */}
                {prompt === "oos" && (
                  <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
                    <SectionLabel>Set out of service — reason required</SectionLabel>
                    <TextField
                      label="Reason"
                      value={reasonInput}
                      onChange={setReasonInput}
                      placeholder="e.g. Steering fault — critical"
                    />
                    <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
                      <ActionButton
                        variant="destructive"
                        onClick={() => runAction(() => changeVehicleStatus(f.id, "OutOfService", reasonInput.trim()))}
                      >
                        {busy ? "SAVING…" : "CONFIRM OUT OF SERVICE"}
                      </ActionButton>
                      <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
                    </div>
                  </Panel>
                )}

                {prompt === "odometer" && (
                  <Panel style={{ marginBottom: 12 }}>
                    <SectionLabel>Update odometer</SectionLabel>
                    <NumberField
                      label="Odometer (km)"
                      value={odoInput}
                      onChange={setOdoInput}
                      min={f.odometerKm}
                      step={1}
                      placeholder={String(f.odometerKm)}
                      hint={
                        <span style={{ color: colors.textFaint }}>
                          · current {formatKm(f.odometerKm)} — readings cannot decrease; crossing{" "}
                          {formatKm(f.endOfLifeKm)} auto-retires the vehicle
                        </span>
                      }
                    />
                    <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
                      <ActionButton
                        variant="primary"
                        onClick={() =>
                          runAction(() => recordOdometer(f.id, parseInt(odoInput, 10)), {
                            detectAutoRetire: true,
                          })
                        }
                      >
                        {busy ? "SAVING…" : "RECORD READING"}
                      </ActionButton>
                      <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
                    </div>
                  </Panel>
                )}

                {prompt === "retire" && (
                  <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
                    <SectionLabel>Retire vehicle — permanent</SectionLabel>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 12 }}>
                      Retiring {f.unitNumber} removes it from service permanently and issues a
                      retirement certificate. A retired vehicle can only be sold or recycled — it
                      cannot return to service.
                    </div>
                    <TextField
                      label="Retirement reason (optional)"
                      value={reasonInput}
                      onChange={setReasonInput}
                      placeholder="e.g. Fleet renewal — replaced by U-08"
                    />
                    <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
                      <ActionButton
                        variant="destructive"
                        onClick={() =>
                          runAction(() => changeVehicleStatus(f.id, "Retired", reasonInput.trim() || undefined))
                        }
                      >
                        {busy ? "SAVING…" : "CONFIRM RETIREMENT"}
                      </ActionButton>
                      <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
                    </div>
                  </Panel>
                )}

                {prompt === "sell" && (
                  <Panel style={{ marginBottom: 12 }}>
                    <SectionLabel>Sell vehicle — terminal</SectionLabel>
                    <NumberField
                      label="Sale price (CAD)"
                      value={priceInput}
                      onChange={setPriceInput}
                      min={0}
                      step={100}
                      placeholder="12500"
                      hint={
                        <span style={{ color: colors.textFaint }}>
                          · recorded on the disposal record; proceeds-to-Budget-Code posting is a
                          Billing follow-up
                        </span>
                      }
                    />
                    <div style={{ display: "flex", gap: 9, marginTop: 12 }}>
                      <ActionButton
                        variant="primary"
                        onClick={() => runAction(() => disposeVehicle(f.id, "Sold", parseFloat(priceInput)))}
                      >
                        {busy ? "SAVING…" : "CONFIRM SALE"}
                      </ActionButton>
                      <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
                    </div>
                  </Panel>
                )}

                {prompt === "recycle" && (
                  <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
                    <SectionLabel>Recycle vehicle — terminal</SectionLabel>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 12 }}>
                      Recycling {f.unitNumber} is permanent. The vehicle record is retained for
                      compliance but the unit leaves the fleet for good.
                    </div>
                    <div style={{ display: "flex", gap: 9 }}>
                      <ActionButton
                        variant="destructive"
                        onClick={() => runAction(() => disposeVehicle(f.id, "Recycled"))}
                      >
                        {busy ? "SAVING…" : "CONFIRM RECYCLE"}
                      </ActionButton>
                      <ActionButton onClick={() => setPrompt(null)}>CANCEL</ActionButton>
                    </div>
                  </Panel>
                )}

                {/* action buttons — driven by the legal transition matrix */}
                {!readOnly && prompt === null && (
                  <div style={{ display: "flex", gap: 9, flexWrap: "wrap" }}>
                    {canTransition(f.status, "Active") && (
                      <ActionButton variant="success" onClick={() => runAction(() => changeVehicleStatus(f.id, "Active"))}>
                        RETURN TO SERVICE
                      </ActionButton>
                    )}
                    {canTransition(f.status, "InMaintenance") && (
                      <ActionButton onClick={() => runAction(() => changeVehicleStatus(f.id, "InMaintenance"))}>
                        SEND TO MAINTENANCE
                      </ActionButton>
                    )}
                    {canTransition(f.status, "OutOfService") && (
                      <ActionButton
                        variant="destructive"
                        onClick={() => {
                          setReasonInput("");
                          setPrompt("oos");
                        }}
                      >
                        SET OUT OF SERVICE
                      </ActionButton>
                    )}
                    {!retired && (
                      <ActionButton
                        onClick={() => {
                          setOdoInput(String(f.odometerKm));
                          setPrompt("odometer");
                        }}
                      >
                        UPDATE ODOMETER
                      </ActionButton>
                    )}
                    {!retired && <ActionButton onClick={() => setModal("edit")}>EDIT</ActionButton>}
                    {canTransition(f.status, "Retired") && (
                      <ActionButton
                        variant="destructive"
                        onClick={() => {
                          setReasonInput("");
                          setPrompt("retire");
                        }}
                      >
                        RETIRE
                      </ActionButton>
                    )}
                    {retired && (
                      <>
                        <ActionButton variant="primary" onClick={() => setCertOpen(true)}>
                          VIEW CERTIFICATE
                        </ActionButton>
                        {canDispose(f.status) && (
                          <>
                            <ActionButton
                              onClick={() => {
                                setPriceInput("");
                                setPrompt("sell");
                              }}
                            >
                              SELL
                            </ActionButton>
                            <ActionButton variant="destructive" onClick={() => setPrompt("recycle")}>
                              RECYCLE
                            </ActionButton>
                          </>
                        )}
                      </>
                    )}
                  </div>
                )}

                {readOnly && (
                  <div style={{ display: "flex", gap: 9, alignItems: "center", flexWrap: "wrap" }}>
                    <ActionButton onClick={() => setCertOpen(true)}>VIEW CERTIFICATE</ActionButton>
                    <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
                      {f.status} — terminal state; record retained for compliance, no further actions.
                    </span>
                  </div>
                )}
              </div>
            )}

            {/* ------------------------------ MAINTENANCE (mock) ------------------------------ */}
            {tab === 1 && (
              <div>
                <PreviewCaption />
                {unitPm.length === 0 ? (
                  <EmptyTabNote>{`No preventive-maintenance reminders on file for ${f.unitNumber}.`}</EmptyTabNote>
                ) : (
                  unitPm.map((r) => (
                    <div
                      key={`${r.unit}-${r.task}`}
                      style={{
                        display: "grid",
                        gridTemplateColumns: "1fr 90px 150px",
                        gap: 11,
                        alignItems: "center",
                        padding: "11px 13px",
                        marginBottom: 5,
                        ...rowSurface(false),
                        cursor: "default",
                      }}
                    >
                      <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                        {r.task}
                      </div>
                      <MonoTag>{r.basis.toUpperCase()}</MonoTag>
                      <StatusChip kind={r.k} label={r.due} />
                    </div>
                  ))
                )}
              </div>
            )}

            {/* ------------------------------ DTC ALERTS (mock) ------------------------------ */}
            {tab === 2 && (
              <div>
                <PreviewCaption />
                {unitDtc.length === 0 ? (
                  <EmptyTabNote>{`No open diagnostic trouble codes for ${f.unitNumber}.`}</EmptyTabNote>
                ) : (
                  unitDtc.map((r) => (
                    <div
                      key={`${r.unit}-${r.code}`}
                      style={{
                        display: "grid",
                        gridTemplateColumns: "70px 1fr 110px 70px",
                        gap: 11,
                        alignItems: "center",
                        padding: "11px 13px",
                        marginBottom: 5,
                        ...rowSurface(false),
                        cursor: "default",
                      }}
                    >
                      <span style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.skyBlue }}>{r.code}</span>
                      <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textPrimary }}>{r.desc}</div>
                      <StatusChip kind={r.k} label={r.severity} />
                      <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, textAlign: "right" }}>
                        {r.raised}
                      </span>
                    </div>
                  ))
                )}
              </div>
            )}

            {/* ------------------------------ PARTS (mock, fleet-wide stock) ------------------------------ */}
            {tab === 3 && (
              <div>
                <PreviewCaption>Preview — not yet wired to backend. Parts stock is fleet-wide, not per-unit.</PreviewCaption>
                {partsInventory.map((p) => (
                  <div
                    key={p.sku}
                    style={{
                      display: "grid",
                      gridTemplateColumns: "130px 1fr 150px",
                      gap: 11,
                      alignItems: "center",
                      padding: "11px 13px",
                      marginBottom: 5,
                      ...rowSurface(false),
                      cursor: "default",
                    }}
                  >
                    <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{p.sku}</span>
                    <div style={{ minWidth: 0 }}>
                      <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                        {p.name}
                      </div>
                      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{p.loc}</div>
                    </div>
                    <StatusChip kind={p.k} label={`${p.onHand} on hand · min ${p.min}`} />
                  </div>
                ))}
              </div>
            )}

            {/* ------------------------------ FUEL & ROUTE (mock) ------------------------------ */}
            {tab === 4 && (
              <div>
                <PreviewCaption />
                {unitFuel.length === 0 ? (
                  <EmptyTabNote>{`No fuel or route telemetry for ${f.unitNumber}.`}</EmptyTabNote>
                ) : (
                  unitFuel.map((r) => (
                    <div key={r.unit} style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
                      <Panel>
                        <SectionLabel>Fuel economy</SectionLabel>
                        <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright }}>
                          {r.l100}
                        </div>
                        <div style={{ marginTop: 9 }}>
                          <StatusChip kind={r.k} label={r.k === "over" ? "Above fleet baseline" : r.k === "soon" ? "Watch" : "Within baseline"} />
                        </div>
                      </Panel>
                      <Panel>
                        <SectionLabel>Idle time</SectionLabel>
                        <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright }}>
                          {r.idlePct}
                        </div>
                        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 9 }}>
                          Share of engine-on time spent idling
                        </div>
                      </Panel>
                      <Panel>
                        <SectionLabel>Route adherence</SectionLabel>
                        <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright }}>
                          {r.routeAdherence}
                        </div>
                        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 9 }}>
                          Corridor plan vs actual GPS trace
                        </div>
                      </Panel>
                    </div>
                  ))
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {modal === "register" && (
        <VehicleFormModal vehicle={null} onClose={() => setModal(null)} onSaved={load} />
      )}
      {modal === "edit" && sel && (
        <VehicleFormModal vehicle={sel} onClose={() => setModal(null)} onSaved={load} />
      )}
      {certOpen && sel && (
        <RetirementCertificateModal vehicleId={sel.id} onClose={() => setCertOpen(false)} />
      )}
    </div>
  );
}
