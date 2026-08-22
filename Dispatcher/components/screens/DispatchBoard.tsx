"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, svcMeta, SERVICE_SHORT, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  corridorLabel,
  isOpenTrip,
  listTrips,
  sortTrips,
  svcForTrip,
  todayIso,
  tripChip,
  tripWindowLabel,
  type TripRecord,
} from "@/lib/api/trips";
import { listInvoices } from "@/lib/api/billing";
import { statusShort } from "@/lib/format";
import { MetricTile } from "@/components/ui/MetricTile";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";

// Dispatch Board — today's trips from the real Trips API (GET /api/trips?date=today)
// with the metric tiles derived from the same rows.

function todayHeading(): string {
  return new Date().toLocaleDateString("en-CA", {
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}

export default function DispatchBoard({ onOpenTrip }: { onOpenTrip: (id: string) => void }) {
  const [rows, setRows] = useState<TripRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  // Draft invoices tile — real count from the Billing API of drafts still to be
  // keyed into QuickBooks (Draft status). Billing prepares invoices for manual
  // QBO entry; there is no overdue/AR concept anymore. A billing fetch failure
  // only greys the tile — it never blocks the board.
  const [draftInvoices, setDraftInvoices] = useState<number | null>(null);

  useEffect(() => {
    let active = true;
    listInvoices().then(
      (invs) => {
        if (active) setDraftInvoices(invs.filter((i) => i.status === "Draft").length);
      },
      () => {
        if (active) setDraftInvoices(null);
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const load = useCallback(async () => {
    try {
      const fresh = await listTrips({ date: todayIso() });
      setRows(sortTrips(fresh.items));
      setLoadError(null);
    } catch (e) {
      setRows(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load today's trips.");
    }
  }, []);

  useEffect(() => {
    let active = true;
    listTrips({ date: todayIso() }).then(
      (fresh) => {
        if (active) {
          setRows(sortTrips(fresh.items));
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRows(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load today's trips.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  // ---- derived metrics (from today's rows) ----
  const total = rows?.length ?? null;
  const inProgress = rows?.filter((t) => t.status === "InProgress").length ?? null;
  const openUnclaimed = rows?.filter(isOpenTrip).length ?? null;
  const driversOnDuty =
    rows === null
      ? null
      : new Set(rows.filter((t) => t.status !== "Cancelled" && t.driverId !== null).map((t) => t.driverId)).size;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      {/* header */}
      <div
        style={{
          flex: "none",
          display: "flex",
          alignItems: "flex-end",
          justifyContent: "space-between",
          padding: "20px 26px 14px",
        }}
      >
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
            Operations · {todayHeading()}
          </div>
          <h1 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: colors.headingBright, margin: 0 }}>
            Dispatch Board
          </h1>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 9, fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          <span style={{ width: 7, height: 7, borderRadius: "50%", background: "#009E73", boxShadow: "0 0 0 3px rgba(0,158,115,.16)" }} />
          Live · Trips API
        </div>
      </div>

      {/* scroll body */}
      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "0 26px 26px" }}>
        {/* STATUS STRIP */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6,1fr)", gap: 12, marginBottom: 18 }}>
          <MetricTile icon="●" iconBg={colors.blue} iconColor="#FFFFFF" label="Trips today" value={total ?? "—"} valueColor={colors.textPrimary} />
          <MetricTile icon="●" iconBg={colors.blue} iconColor="#FFFFFF" label="In progress" value={inProgress ?? "—"} valueColor={colors.skyBlue} />
          <MetricTile
            icon="◐"
            iconBg="#E1B000"
            iconColor={colors.navy}
            label="Open · unclaimed"
            value={openUnclaimed ?? "—"}
            valueColor={statusMeta("soon").t}
            borderColor={openUnclaimed ? "rgba(225,176,0,.4)" : undefined}
          />
          <MetricTile
            icon="✓"
            iconBg="#009E73"
            iconColor="#FFFFFF"
            label="Drivers on duty"
            value={driversOnDuty ?? "—"}
            valueColor={statusMeta("ontime").t}
          />
          {/* Compliance alerts were a pure mock figure — "—" until a real
              compliance rollup exists (HOS has no backend domain yet). */}
          <MetricTile icon="▲" iconBg="#D55E00" iconColor="#fff" label="Compliance alerts" value="—" valueColor={colors.textDim} />
          <MetricTile
            icon="◷"
            iconBg="#9C6500"
            iconColor="#fff"
            label="Drafts to enter in QBO"
            value={draftInvoices ?? "—"}
            valueColor={
              draftInvoices === null
                ? colors.textDim
                : draftInvoices > 0
                  ? statusMeta("soon").t
                  : statusMeta("ontime").t
            }
            borderColor={draftInvoices ? "rgba(225,176,0,.4)" : undefined}
          />
        </div>

        {/* BOARD */}
        <div>
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 11 }}>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: colors.textSecondary }}>
              TODAY&rsquo;S TRIPS
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
              Thompson · Leaf Rapids · Lynn Lake · South Indian Lake · Black Sturgeon Falls
            </div>
          </div>

          {loadError && (
            <Panel borderColor="rgba(213,94,0,.4)">
              <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <StatusChip kind="over" label={`Trips unavailable — ${loadError}`} />
                <ActionButton variant="primary" onClick={load}>
                  RETRY
                </ActionButton>
              </div>
            </Panel>
          )}

          {rows === null && !loadError && (
            <div>
              {[0, 1, 2, 3].map((i) => (
                <div
                  key={i}
                  style={{
                    height: 58,
                    borderRadius: 9,
                    border: `1px solid ${colors.borderSubtle}`,
                    background: colors.cardBg,
                    marginBottom: 5,
                    opacity: 0.55 - i * 0.1,
                  }}
                />
              ))}
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
                Loading today&rsquo;s trips from API…
              </div>
            </div>
          )}

          {rows !== null && rows.length === 0 && (
            <Panel>
              <SectionLabel>No trips scheduled today</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                Nothing on the board for today. Trips come from the Create Trip wizard or are generated from active
                schedule templates (Routes &amp; Schedules).
              </div>
            </Panel>
          )}

          {rows !== null && rows.length > 0 && (
            <>
              {/* column head */}
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "74px 112px minmax(0,1fr) 116px 140px",
                  gap: 10,
                  padding: "0 14px 8px",
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".12em",
                  textTransform: "uppercase",
                  color: colors.textFaint,
                }}
              >
                <div>Trip</div>
                <div>Service</div>
                <div>Corridor</div>
                <div>Driver</div>
                <div>Status</div>
              </div>
              {rows.map((t) => {
                const svc = svcForTrip(t.serviceType);
                const sc = svcMeta(svc);
                const chip = tripChip(t);
                const open = isOpenTrip(t);
                return (
                  <div
                    key={t.id}
                    onClick={() => onOpenTrip(t.id)}
                    style={{
                      display: "grid",
                      gridTemplateColumns: "74px 112px minmax(0,1fr) 116px 140px",
                      gap: 10,
                      alignItems: "center",
                      padding: "11px 14px",
                      marginBottom: 5,
                      ...rowSurface(false, sc.accent),
                    }}
                  >
                    <div>
                      <div style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{t.tripNumber}</div>
                      <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 2 }}>
                        {tripWindowLabel(t)}
                      </div>
                    </div>
                    <div style={{ minWidth: 0 }}>
                      <ServiceChip svc={svc} label={SERVICE_SHORT[svc]} />
                    </div>
                    <div style={{ minWidth: 0 }}>
                      <div
                        style={{
                          fontFamily: fonts.body,
                          fontSize: 12.5,
                          color: colors.textSecondary,
                          fontWeight: 500,
                          whiteSpace: "nowrap",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                        }}
                      >
                        {corridorLabel(t)}
                      </div>
                      <div
                        style={{
                          fontFamily: fonts.mono,
                          fontSize: 10,
                          color: colors.textDim,
                          marginTop: 2,
                          whiteSpace: "nowrap",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                        }}
                      >
                        {t.distanceKm} km · {t.vehicleUnit ?? "unassigned"}
                      </div>
                    </div>
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12.5,
                        lineHeight: 1.3,
                        fontWeight: open ? 600 : 500,
                        color: open ? colors.amberText : colors.textSecondary,
                      }}
                    >
                      {t.driverName ?? "OPEN — needs coverage"}
                    </div>
                    <div style={{ minWidth: 0 }}>
                      <StatusChip kind={chip.kind} label={statusShort(chip.label)} />
                    </div>
                  </div>
                );
              })}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
