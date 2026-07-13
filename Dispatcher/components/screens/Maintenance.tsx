"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { dtcAlerts, partsInventory, pmReminders } from "@/lib/data";
import {
  ApiError,
  formatCad,
  formatKm,
  isDisposed,
  lifeKindFor,
  listVehicles,
  statusKindFor,
  statusLabelFor,
  type Vehicle,
} from "@/lib/api";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { ActionButton } from "@/components/ui/Button";

// Fleet-wide Maintenance & Asset Management dashboard.
// PM reminders / DTC alerts / parts are MOCK previews (no backend domain yet);
// End-of-Life Planning is REAL data from the Fleet API.

function SectionCaption({ mock }: { mock: boolean }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
      <MonoTag color={mock ? "#E1B000" : "#38d3a6"}>{mock ? "MOCK" : "LIVE"}</MonoTag>
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        {mock ? "Preview — not yet wired to backend." : "Fleet API · vehicle registry"}
      </span>
    </div>
  );
}

export default function Maintenance({ onOpenVehicle }: { onOpenVehicle: (id: string) => void }) {
  const [vehicles, setVehicles] = useState<Vehicle[] | null>(null);
  const [eolError, setEolError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setEolError(null);
    try {
      setVehicles(await listVehicles());
    } catch (e) {
      setVehicles(null);
      setEolError(e instanceof ApiError ? e.message : "Failed to load fleet.");
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const pmSoon = pmReminders.filter((r) => r.k === "soon").length;
  const pmOverdue = pmReminders.filter((r) => r.k === "over").length;
  const openDtc = dtcAlerts.filter((r) => r.k !== "ontime").length;
  const lowStock = partsInventory.filter((p) => p.k !== "ontime");

  const eolFleet = (vehicles ?? []).filter((v) => !isDisposed(v.status));
  const nearingEol = eolFleet.filter((v) => v.lifeUsedPct >= 75).length;
  const eolSorted = [...eolFleet].sort((a, b) => b.lifeUsedPct - a.lifeUsedPct);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Operations · PM scheduling, diagnostics, parts & asset planning" title="Maintenance" />
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "16px 26px 26px", borderTop: `1px solid ${colors.border}` }}>
        {/* KPI row */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(5, 1fr)", gap: 12, marginBottom: 18 }}>
          <MetricTile
            icon="◐"
            iconBg="rgba(225,176,0,.16)"
            iconColor="#ecc94b"
            label="PM due soon"
            value={pmSoon}
            valueColor="#ecc94b"
          />
          <MetricTile
            icon="▲"
            iconBg="rgba(213,94,0,.16)"
            iconColor="#f0803f"
            label="PM overdue"
            value={pmOverdue}
            valueColor="#f0803f"
            borderColor="rgba(213,94,0,.35)"
          />
          <MetricTile
            icon="▲"
            iconBg="rgba(213,94,0,.16)"
            iconColor="#f0803f"
            label="Open DTC alerts"
            value={openDtc}
            valueColor={colors.headingBright}
          />
          <MetricTile
            icon="▪"
            iconBg="rgba(225,176,0,.16)"
            iconColor="#ecc94b"
            label="Low-stock parts"
            value={lowStock.length}
            valueColor={colors.headingBright}
          />
          <MetricTile
            icon="●"
            iconBg="rgba(59,141,212,.16)"
            iconColor={colors.skyBlue}
            label="Nearing end-of-life"
            value={vehicles === null ? "—" : nearingEol}
            valueColor={colors.headingBright}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
          {/* PM reminders — mock */}
          <Panel>
            <SectionLabel>Preventive maintenance reminders</SectionLabel>
            <SectionCaption mock />
            {pmReminders.map((r) => (
              <div
                key={`${r.unit}-${r.task}`}
                style={{
                  display: "grid",
                  gridTemplateColumns: "48px 1fr 140px",
                  gap: 10,
                  alignItems: "center",
                  padding: "9px 11px",
                  marginBottom: 5,
                  ...rowSurface(false),
                  cursor: "default",
                }}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{r.unit}</span>
                <div style={{ minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      fontWeight: 600,
                      color: colors.textPrimary,
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {r.task}
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim, textTransform: "uppercase", letterSpacing: ".06em" }}>
                    {r.basis}-based
                  </div>
                </div>
                <StatusChip kind={r.k} label={r.due} />
              </div>
            ))}
          </Panel>

          {/* DTC alerts — mock */}
          <Panel>
            <SectionLabel>Open DTC alerts</SectionLabel>
            <SectionCaption mock />
            {dtcAlerts.map((r) => (
              <div
                key={`${r.unit}-${r.code}`}
                style={{
                  display: "grid",
                  gridTemplateColumns: "48px 64px 1fr 92px",
                  gap: 10,
                  alignItems: "center",
                  padding: "9px 11px",
                  marginBottom: 5,
                  ...rowSurface(false),
                  cursor: "default",
                }}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{r.unit}</span>
                <span style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>{r.code}</span>
                <div style={{ minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      color: colors.textPrimary,
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {r.desc}
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>raised {r.raised}</div>
                </div>
                <StatusChip kind={r.k} label={r.severity} />
              </div>
            ))}
          </Panel>
        </div>

        {/* Low-stock parts — mock */}
        <Panel style={{ marginBottom: 14 }}>
          <SectionLabel>Low-stock parts</SectionLabel>
          <SectionCaption mock />
          {lowStock.length === 0 ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
              All tracked parts are at or above minimum stock.
            </div>
          ) : (
            lowStock.map((p) => (
              <div
                key={p.sku}
                style={{
                  display: "grid",
                  gridTemplateColumns: "130px 1fr 200px 170px",
                  gap: 10,
                  alignItems: "center",
                  padding: "9px 11px",
                  marginBottom: 5,
                  ...rowSurface(false),
                  cursor: "default",
                }}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{p.sku}</span>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>{p.name}</div>
                <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{p.loc}</span>
                <StatusChip kind={p.k} label={`${p.onHand} on hand · min ${p.min}`} />
              </div>
            ))
          )}
        </Panel>

        {/* End-of-Life Planning — REAL data */}
        <Panel>
          <SectionLabel>End-of-life planning · km depreciation</SectionLabel>
          <SectionCaption mock={false} />
          {eolError ? (
            <div>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#f0803f", fontWeight: 600, marginBottom: 12 }}>
                ▲ {eolError}
              </div>
              <ActionButton variant="primary" onClick={load}>
                RETRY
              </ActionButton>
            </div>
          ) : vehicles === null ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading fleet from API…</div>
          ) : eolSorted.length === 0 ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
              No vehicles in service — register a vehicle in Fleet to begin asset planning.
            </div>
          ) : (
            eolSorted.map((v) => {
              const lifeKind = lifeKindFor(v.lifeUsedPct);
              const meta = statusMeta(lifeKind);
              const pct = Math.min(100, Math.max(0, v.lifeUsedPct));
              return (
                <div
                  key={v.id}
                  onClick={() => onOpenVehicle(v.id)}
                  style={{
                    display: "grid",
                    gridTemplateColumns: "48px 1fr 170px 140px 160px 150px",
                    gap: 12,
                    alignItems: "center",
                    padding: "11px 13px",
                    marginBottom: 5,
                    ...rowSurface(false),
                  }}
                >
                  <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{v.unitNumber}</span>
                  <div style={{ minWidth: 0 }}>
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12.5,
                        fontWeight: 600,
                        color: colors.textPrimary,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {v.year} {v.make} {v.model}
                    </div>
                    <StatusChip kind={statusKindFor(v.status)} label={statusLabelFor(v.status)} />
                  </div>
                  <div>
                    <div
                      style={{
                        height: 8,
                        borderRadius: 5,
                        background: colors.inputBg,
                        overflow: "hidden",
                        border: `1px solid ${colors.borderSubtle}`,
                        marginBottom: 4,
                      }}
                    >
                      <div style={{ height: "100%", width: `${pct}%`, background: meta.c, borderRadius: 5 }} />
                    </div>
                    <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                      {formatKm(v.odometerKm)} of {formatKm(v.endOfLifeKm)}
                    </div>
                  </div>
                  <StatusChip
                    kind={lifeKind}
                    label={v.lifeUsedPct >= 100 ? "Life exhausted" : `${Math.round(v.lifeUsedPct)}% used`}
                  />
                  <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary, textAlign: "right" }}>
                    {formatCad(v.currentValueCad)}
                    <span style={{ color: colors.textDim, fontFamily: fonts.body, fontSize: 10.5 }}> value</span>
                  </div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary, textAlign: "right" }}>
                    {formatKm(v.remainingKm)}
                    <span style={{ color: colors.textDim, fontFamily: fonts.body, fontSize: 10.5 }}> to EOL</span>
                  </div>
                </div>
              );
            })
          )}
        </Panel>
      </div>
    </div>
  );
}
