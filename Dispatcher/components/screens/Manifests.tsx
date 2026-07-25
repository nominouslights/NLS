"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta, type StatusKind } from "@/lib/theme";
import { ApiError, getTripManifest } from "@/lib/api";
import {
  corridorLabel,
  isoDaysFromToday,
  listTrips,
  recordTripDemand,
  refetchUntil,
  shortDateLabel,
  sortTrips,
  svcForTrip,
  tripWindowLabel,
  type TripRecord,
} from "@/lib/api/trips";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { printTripManifest } from "@/lib/documents/tripManifestPdf";

// Manifests & Demand — trips with demand semantics (community / NIHB /
// contract crew) for today ± a few days, from the real Trips API. The demand
// meter reads seatsConfirmed / seatsMinimum off the trip; demand controls
// write POST /api/trips/{id}/demand. Passenger name rosters have no backend —
// they arrive with the driver's NL-TM-01 manifest after the trip.

const WINDOW_DAYS = 3;
const DEMAND_SERVICE_TYPES = new Set(["Community", "Nihb", "ContractCrew"]);

function demandBadge(t: TripRecord): { kind: StatusKind; label: string } {
  if (t.demandGuaranteed) return { kind: "ontime", label: "Guaranteed · Gift-a-Seat" };
  if (t.seatsMinimum != null) {
    return t.seatsConfirmed >= t.seatsMinimum
      ? { kind: "ontime", label: `Viable · ${t.seatsConfirmed} / ${t.seatsMinimum}` }
      : { kind: "soon", label: `Not yet viable · ${t.seatsConfirmed} / ${t.seatsMinimum}` };
  }
  if (t.serviceType === "Nihb") return { kind: "ontime", label: "Confirmed · voucher" };
  return {
    kind: "info",
    label: t.seatsCapacity != null ? `Crew · ${t.seatsConfirmed} / ${t.seatsCapacity}` : `Crew · ${t.seatsConfirmed}`,
  };
}

export default function Manifests() {
  const [rows, setRows] = useState<TripRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [giftShown, setGiftShown] = useState(false);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const fetchList = useCallback(async () => {
    const all = await listTrips({
      from: isoDaysFromToday(-WINDOW_DAYS),
      to: isoDaysFromToday(WINDOW_DAYS),
    });
    return sortTrips(all.filter((t) => DEMAND_SERVICE_TYPES.has(t.serviceType) && t.status !== "Cancelled"));
  }, []);

  const load = useCallback(async () => {
    try {
      const fresh = await fetchList();
      setRows(fresh);
      setLoadError(null);
    } catch (e) {
      setRows(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load demand trips.");
    }
  }, [fetchList]);

  useEffect(() => {
    let active = true;
    fetchList().then(
      (fresh) => {
        if (active) {
          setRows(fresh);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRows(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load demand trips.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [fetchList]);

  const m = rows?.find((t) => t.id === selectedId) ?? rows?.[0] ?? null;

  function select(id: string) {
    setSelectedId(id);
    setGiftShown(false);
    setActionError(null);
  }

  async function submitDemand(t: TripRecord, seatsConfirmed: number, demandGuaranteed: boolean) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await recordTripDemand(t.id, seatsConfirmed, demandGuaranteed);
      const fresh = await refetchUntil(fetchList, (list) => {
        const row = list.find((x) => x.id === t.id);
        return row !== undefined && row.seatsConfirmed === seatsConfirmed && row.demandGuaranteed === demandGuaranteed;
      });
      setRows(fresh);
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to record demand — please try again.");
    } finally {
      setBusy(false);
    }
  }

  async function printManifest(t: TripRecord) {
    if (!t.manifestId) return;
    try {
      const manifest = await getTripManifest(t.manifestId);
      printTripManifest(manifest);
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to load the manifest.");
    }
  }

  const header = (
    <div style={{ flex: "none", padding: "20px 26px 12px", display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: 16 }}>
      <PageHeader eyebrow="Operations · Manifests, demand activation & run economics" title="Manifests & Demand" />
      <ActionButton variant="secondary" onClick={() => printTripManifest(null)} style={{ marginTop: 6 }}>
        PRINT BLANK NL-TM-01
      </ActionButton>
    </div>
  );

  if (loadError) {
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
        {header}
        <div style={{ padding: "26px", maxWidth: 560 }}>
          <Panel borderColor="rgba(213,94,0,.4)">
            <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
              <StatusChip kind="over" label={`Demand trips unavailable — ${loadError}`} />
              <ActionButton variant="primary" onClick={load}>
                RETRY
              </ActionButton>
            </div>
          </Panel>
        </div>
      </div>
    );
  }

  if (rows === null) {
    return (
      <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
        {header}
        <div style={{ padding: "16px 26px" }}>
          {[0, 1, 2].map((i) => (
            <div
              key={i}
              style={{
                height: 74,
                borderRadius: 9,
                border: `1px solid ${colors.borderSubtle}`,
                background: colors.cardBg,
                marginBottom: 6,
                opacity: 0.55 - i * 0.12,
              }}
            />
          ))}
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
            Loading demand trips from API…
          </div>
        </div>
      </div>
    );
  }

  const seatsNeeded = m && m.seatsMinimum != null ? Math.max(0, m.seatsMinimum - m.seatsConfirmed) : 0;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      {header}
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "32% 1fr", borderTop: `1px solid ${colors.border}` }}>
        {/* master */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {rows.length === 0 && (
            <Panel>
              <SectionLabel>No demand runs in window</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                No community, NIHB, or contract-crew trips within ±{WINDOW_DAYS} days. Trips appear here as the wizard
                or the schedule generator creates them.
              </div>
            </Panel>
          )}
          {rows.map((rec) => {
            const active = m !== null && rec.id === m.id;
            const badge = demandBadge(rec);
            const bm = statusMeta(badge.kind);
            return (
              <div
                key={rec.id}
                onClick={() => select(rec.id)}
                style={{ padding: "12px 14px", marginBottom: 5, ...rowSurface(active, colors.blue) }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 6 }}>
                  <ServiceChip svc={svcForTrip(rec.serviceType)} />
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{rec.tripNumber}</span>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                  {corridorLabel(rec)}
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim, marginTop: 3 }}>
                  {shortDateLabel(rec.serviceDate)} · {tripWindowLabel(rec)}
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: 6, marginTop: 6 }}>
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 5,
                      padding: "2px 8px",
                      borderRadius: 5,
                      fontFamily: fonts.body,
                      fontWeight: 600,
                      fontSize: 10.5,
                      background: bm.bg,
                      border: `1px solid ${bm.bd}`,
                      color: bm.t,
                    }}
                  >
                    {bm.g} {badge.label}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        {/* detail */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {m === null ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
              Select a run to manage its demand.
            </div>
          ) : (
            <div className="detailfade" key={m.id}>
              <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 4 }}>
                <ServiceChip svc={svcForTrip(m.serviceType)} />
                <span style={{ fontFamily: fonts.mono, fontSize: 13, color: colors.skyBlue, marginLeft: "auto" }}>{m.tripNumber}</span>
              </div>
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1.05, color: colors.headingBright, margin: "6px 0 4px" }}>
                {corridorLabel(m)}
              </h2>
              <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 16 }}>
                {shortDateLabel(m.serviceDate)} · {tripWindowLabel(m)} · {m.distanceKm} km
                {m.vehicleUnit ? ` · ${m.vehicleUnit}` : ""}
              </div>

              {actionError && (
                <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
                  <StatusChip kind="over" label={actionError} />
                </Panel>
              )}

              {/* demand meter — runs with a viability minimum */}
              {m.seatsMinimum != null && (
                <div style={{ padding: "16px 18px", background: colors.cardBg, border: "1px solid rgba(225,176,0,.3)", borderRadius: 12, marginBottom: 14, boxShadow: colors.shadowCard }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 11 }}>
                    <div
                      style={{
                        fontFamily: fonts.semiCondensed,
                        fontSize: 9.5,
                        letterSpacing: ".14em",
                        textTransform: "uppercase",
                        color: colors.textLabel,
                      }}
                    >
                      Demand meter · {m.seatsMinimum}-passenger minimum
                    </div>
                    {m.demandGuaranteed ? (
                      <StatusChip kind="ontime" label="Guaranteed" />
                    ) : m.seatsConfirmed >= m.seatsMinimum ? (
                      <StatusChip kind="ontime" label="Viable" />
                    ) : (
                      <StatusChip kind="soon" label="Not yet viable" />
                    )}
                  </div>
                  <div style={{ display: "flex", gap: 6, marginBottom: 9 }}>
                    {Array.from({ length: Math.max(m.seatsMinimum, m.seatsConfirmed) }).map((_, i) => (
                      <div
                        key={i}
                        style={{
                          flex: 1,
                          height: 14,
                          borderRadius: 4,
                          background: i < m.seatsConfirmed ? "#009E73" : colors.inputBg,
                          border: i < m.seatsConfirmed ? undefined : `1px dashed ${colors.textFaint}`,
                        }}
                      />
                    ))}
                  </div>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
                    <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>
                      <strong style={{ color: colors.textPrimary }}>{m.seatsConfirmed} confirmed</strong>
                      {m.demandGuaranteed
                        ? " · run guaranteed to depart"
                        : seatsNeeded > 0
                          ? ` · ${seatsNeeded} more needed to activate this run`
                          : " · minimum met"}
                    </div>
                    <div style={{ display: "flex", gap: 8 }}>
                      <ActionButton
                        onClick={() => submitDemand(m, Math.max(0, m.seatsConfirmed - 1), m.demandGuaranteed)}
                        style={busy || m.seatsConfirmed === 0 ? { opacity: 0.5, cursor: busy ? "wait" : "not-allowed" } : undefined}
                      >
                        − REMOVE SEAT
                      </ActionButton>
                      <ActionButton
                        variant="success"
                        onClick={() => submitDemand(m, m.seatsConfirmed + 1, m.demandGuaranteed)}
                        disabled={busy}
                      >
                        {busy ? "SAVING…" : "+ CONFIRM SEAT"}
                      </ActionButton>
                    </div>
                  </div>
                </div>
              )}

              {/* crew / NIHB seat confirmation for runs without a minimum */}
              {m.seatsMinimum == null && (
                <Panel style={{ marginBottom: 14 }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
                      <strong style={{ color: colors.textPrimary }}>{m.seatsConfirmed} confirmed</strong>
                      {m.seatsCapacity != null ? ` of ${m.seatsCapacity} seats` : ""} — no viability minimum on this run.
                    </div>
                    <div style={{ display: "flex", gap: 8 }}>
                      <ActionButton
                        onClick={() => submitDemand(m, Math.max(0, m.seatsConfirmed - 1), m.demandGuaranteed)}
                        style={busy || m.seatsConfirmed === 0 ? { opacity: 0.5, cursor: busy ? "wait" : "not-allowed" } : undefined}
                      >
                        − REMOVE SEAT
                      </ActionButton>
                      <ActionButton
                        variant="success"
                        onClick={() => submitDemand(m, m.seatsConfirmed + 1, m.demandGuaranteed)}
                        disabled={busy}
                      >
                        {busy ? "SAVING…" : "+ CONFIRM SEAT"}
                      </ActionButton>
                    </div>
                  </div>
                </Panel>
              )}

              {/* passenger roster — no backend yet */}
              <Panel style={{ marginBottom: 14 }}>
                <SectionLabel>
                  Passenger roster{m.seatsCapacity != null ? ` · ${m.seatsConfirmed} / ${m.seatsCapacity} seats` : ""}
                </SectionLabel>
                <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, flex: 1, minWidth: 220 }}>
                    Passenger names come from the driver&rsquo;s NL-TM-01 trip manifest after the run — seat counts here
                    track demand only.
                  </div>
                  {m.manifestId ? (
                    <div style={{ display: "flex", alignItems: "center", gap: 9 }}>
                      <StatusChip kind="ontime" label="Manifest on file" />
                      <ActionButton onClick={() => printManifest(m)}>PRINT MANIFEST</ActionButton>
                    </div>
                  ) : (
                    <StatusChip kind="off" label="No manifest yet" />
                  )}
                </div>
              </Panel>

              {/* GIFT-A-SEAT — community runs below minimum */}
              {m.serviceType === "Community" && m.seatsMinimum != null && !m.demandGuaranteed && (
                <div
                  style={{
                    padding: "16px 18px",
                    background: "linear-gradient(180deg,rgba(31,111,178,.08),transparent)",
                    border: `1px solid ${colors.borderActive}`,
                    borderRadius: 12,
                    marginBottom: 14,
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 8 }}>
                    <span
                      style={{
                        width: 22,
                        height: 22,
                        borderRadius: 6,
                        background: colors.blue,
                        color: "#FFFFFF",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontSize: 13,
                        fontWeight: 800,
                      }}
                    >
                      ◆
                    </span>
                    <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 17, color: colors.skyBlue }}>Gift-a-Seat guarantee</div>
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, lineHeight: 1.55, color: colors.textMuted, marginBottom: 13 }}>
                    A passenger can cover the remaining seats to guarantee this run departs. The{" "}
                    <strong style={{ color: colors.textPrimary }}>exact commitment is shown before confirming</strong>.
                  </div>
                  {giftShown ? (
                    <>
                      <div
                        style={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "space-between",
                          padding: "13px 16px",
                          background: colors.inputBg,
                          border: `1px solid ${colors.borderActive}`,
                          borderRadius: 10,
                          marginBottom: 12,
                          gap: 12,
                          flexWrap: "wrap",
                        }}
                      >
                        <div>
                          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginBottom: 3 }}>
                            Seats to cover · community fare per seat is quoted by dispatch
                          </div>
                          <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: statusMeta("ontime").t, fontVariantNumeric: "tabular-nums" }}>
                            {seatsNeeded} seat{seatsNeeded === 1 ? "" : "s"}
                          </div>
                        </div>
                        <ActionButton
                          variant="success"
                          onClick={() => submitDemand(m, m.seatsConfirmed, true)}
                          disabled={busy}
                        >
                          {busy ? "SAVING…" : "CONFIRM GUARANTEE"}
                        </ActionButton>
                      </div>
                      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                        Confirming marks the run <strong style={{ color: colors.textPrimary }}>guaranteed to depart</strong> regardless of
                        further confirmations.
                      </div>
                    </>
                  ) : (
                    <ActionButton variant="primary" onClick={() => setGiftShown(true)}>
                      CALCULATE GUARANTEE
                    </ActionButton>
                  )}
                </div>
              )}

              {/* NIHB + mixing */}
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <div style={{ padding: "14px 16px", background: colors.cardBg, border: "1px solid rgba(23,119,158,.28)", borderRadius: 11, boxShadow: colors.shadowCard }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 7, marginBottom: 8 }}>
                    <span style={{ color: colors.skyBlue, fontWeight: 800 }}>✚</span>
                    <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 15, color: colors.skyBlue }}>NIHB voucher</div>
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 12, lineHeight: 1.55, color: colors.textMuted }}>
                    A prior-approved voucher = confirmed demand. Suppresses the minimum,{" "}
                    <strong style={{ color: statusMeta("ontime").t }}>$0 to patient</strong>, authorized escorts free.
                  </div>
                </div>
                <div style={{ padding: "14px 16px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 11, boxShadow: colors.shadowCard }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 7, marginBottom: 8 }}>
                    <span style={{ color: statusMeta("ontime").t, fontWeight: 800 }}>✓</span>
                    <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 15, color: colors.textPrimary }}>Mixing rule</div>
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 12, lineHeight: 1.55, color: colors.textMuted }}>
                    Community passengers blocked while mine crew aboard. Empty legs surfaced as available.
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
