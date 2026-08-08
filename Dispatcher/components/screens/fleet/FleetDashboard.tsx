"use client";

import { useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { dtcAlerts, partsInventory, pmReminders } from "@/lib/data";
import {
  ApiError,
  formatCad,
  formatKm,
  formatUtcDate,
  isDisposed,
  lifeKindFor,
  statusKindFor,
  statusLabelFor,
  type Vehicle,
} from "@/lib/api";
import { listAllWorkOrders, type WorkOrderPriorityWire, type WorkOrderWire } from "@/lib/api/maintenance";
import { OPEN_WIRE_STATUSES, WO_SOURCE_LABEL, WO_STATUS_LABEL, workOrderKindWire } from "@/lib/workOrderDisplay";
import { documents, useMaintenanceStore } from "@/lib/maintenanceStore";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";

// Fleet-wide Fleet & Maintenance dashboard — shown in the detail pane until a
// vehicle is selected. Work orders + end-of-life are real Fleet API data;
// compliance is the merged prototype (mock store); PM / DTC / parts are mock
// previews.

const PRIORITY_ORDER: Record<WorkOrderPriorityWire, number> = { Critical: 0, High: 1, Medium: 2, Low: 3 };

function Caption({ mock, children }: { mock: boolean; children: string }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
      <MonoTag color={mock ? statusMeta("soon").t : statusMeta("ontime").t}>{mock ? "MOCK" : "LIVE"}</MonoTag>
      <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{children}</span>
    </div>
  );
}

export default function FleetDashboard({
  vehicles,
  woVersion,
  onOpenVehicle,
}: {
  vehicles: Vehicle[];
  woVersion: number;
  onOpenVehicle: (id: string, tab?: string) => void;
}) {
  useMaintenanceStore();

  const [wos, setWos] = useState<WorkOrderWire[] | null>(null);
  const [woError, setWoError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listAllWorkOrders().then(
      (fresh) => {
        if (active) {
          setWos(fresh);
          setWoError(null);
        }
      },
      (e) => {
        if (active) setWoError(e instanceof ApiError ? e.message : "Failed to load work orders.");
      },
    );
    return () => {
      active = false;
    };
  }, [woVersion]);

  const unitToId = new Map(vehicles.map((v) => [v.unitNumber, v.id]));
  const idToUnit = new Map(vehicles.map((v) => [v.id, v.unitNumber]));
  function openUnit(unit: string, tab?: string) {
    const id = unitToId.get(unit);
    if (id) onOpenVehicle(id, tab);
  }

  const pmSoon = pmReminders.filter((r) => r.k === "soon").length;
  const pmOverdue = pmReminders.filter((r) => r.k === "over").length;
  const openDtc = dtcAlerts.filter((r) => r.k !== "ontime").length;
  const lowStock = partsInventory.filter((p) => p.k !== "ontime");

  const openWos = [...(wos ?? []).filter((w) => OPEN_WIRE_STATUSES.includes(w.status))].sort(
    (a, b) => PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority],
  );
  const flaggedDocs = documents.filter((d) => d.k !== "ontime");

  const eolFleet = vehicles.filter((v) => !isDisposed(v.status));
  const nearingEol = eolFleet.filter((v) => v.lifeUsedPct >= 75).length;
  const eolSorted = [...eolFleet].sort((a, b) => b.lifeUsedPct - a.lifeUsedPct);

  return (
    <div className="detailfade">
      <div style={{ marginBottom: 6 }}>
        <SectionLabel>Fleet overview</SectionLabel>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginBottom: 16 }}>
          Select a vehicle from the list to register documents, log service, raise work orders, or enter a DVIR.
        </div>
      </div>

      {/* KPI row */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12, marginBottom: 16 }}>
        <MetricTile icon="◐" iconBg="rgba(225,176,0,.16)" iconColor={statusMeta("soon").t} label="PM due soon" value={pmSoon} valueColor={statusMeta("soon").t} />
        <MetricTile icon="▲" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="PM overdue" value={pmOverdue} valueColor={statusMeta("over").t} borderColor="rgba(213,94,0,.35)" />
        <MetricTile icon="▪" iconBg="rgba(31,111,178,.16)" iconColor={colors.skyBlue} label="Open work orders" value={openWos.length} valueColor={colors.headingBright} />
        <MetricTile icon="▲" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="Open DTC alerts" value={openDtc} valueColor={colors.headingBright} />
        <MetricTile icon="●" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="Docs expiring / expired" value={flaggedDocs.length} valueColor={colors.headingBright} />
        <MetricTile icon="●" iconBg="rgba(31,111,178,.16)" iconColor={colors.skyBlue} label="Nearing end-of-life" value={nearingEol} valueColor={colors.headingBright} />
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
        {/* Open work orders queue */}
        <Panel>
          <SectionLabel>Open work orders</SectionLabel>
          <Caption mock={false}>Fleet API · work orders</Caption>
          {woError ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600 }}>
              ▲ {woError}
            </div>
          ) : wos === null ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading work orders…</div>
          ) : openWos.length === 0 ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No open work orders.</div>
          ) : (
            openWos.map((w) => (
              <div
                key={w.id}
                onClick={() => onOpenVehicle(w.vehicleId, "Work Orders")}
                style={{ display: "grid", gridTemplateColumns: "48px 1fr 150px", gap: 10, alignItems: "center", padding: "9px 11px", marginBottom: 5, ...rowSurface(false) }}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>
                  {idToUnit.get(w.vehicleId) ?? "—"}
                </span>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {w.title}
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                    {w.source === "Manual" ? w.number : `${w.number} · from ${WO_SOURCE_LABEL[w.source]}`}
                  </div>
                </div>
                <StatusChip kind={workOrderKindWire(w)} label={`${WO_STATUS_LABEL[w.status]} · ${w.priority}`} />
              </div>
            ))
          )}
        </Panel>

        {/* Compliance watch */}
        <Panel>
          <SectionLabel>Compliance watch · documents</SectionLabel>
          <Caption mock>Prototype — expiry-driven compliance.</Caption>
          {flaggedDocs.length === 0 ? (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
              All uploaded documents are valid.
            </div>
          ) : (
            flaggedDocs.map((d) => (
              <div
                key={d.id}
                onClick={() => openUnit(d.unit, "Documents & Compliance")}
                style={{ display: "grid", gridTemplateColumns: "48px 1fr 150px", gap: 10, alignItems: "center", padding: "9px 11px", marginBottom: 5, ...rowSurface(false) }}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{d.unit}</span>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>{d.type}</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                    {d.expiry ? `Expires ${formatUtcDate(d.expiry)}` : "No expiry"}
                  </div>
                </div>
                <StatusChip kind={d.k} label={d.k === "over" ? "Expired" : "Expiring soon"} />
              </div>
            ))
          )}
        </Panel>
      </div>

      {/* PM reminders + DTC alerts */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginBottom: 14 }}>
        <Panel>
          <SectionLabel>Preventive maintenance reminders</SectionLabel>
          <Caption mock>Preview — not yet wired to backend.</Caption>
          {pmReminders.map((r) => (
            <div
              key={`${r.unit}-${r.task}`}
              onClick={() => openUnit(r.unit, "Overview")}
              style={{ display: "grid", gridTemplateColumns: "48px 1fr 140px", gap: 10, alignItems: "center", padding: "9px 11px", marginBottom: 5, ...rowSurface(false) }}
            >
              <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{r.unit}</span>
              <div style={{ minWidth: 0 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
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

        <Panel>
          <SectionLabel>Open DTC alerts</SectionLabel>
          <Caption mock>Preview — not yet wired to backend.</Caption>
          {dtcAlerts.map((r) => (
            <div
              key={`${r.unit}-${r.code}`}
              onClick={() => openUnit(r.unit, "DTC Alerts")}
              style={{ display: "grid", gridTemplateColumns: "48px 64px 1fr 92px", gap: 10, alignItems: "center", padding: "9px 11px", marginBottom: 5, ...rowSurface(false) }}
            >
              <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{r.unit}</span>
              <span style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>{r.code}</span>
              <div style={{ minWidth: 0 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                  {r.desc}
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>raised {r.raised}</div>
              </div>
              <StatusChip kind={r.k} label={r.severity} />
            </div>
          ))}
        </Panel>
      </div>

      {/* Low-stock parts */}
      <Panel style={{ marginBottom: 14 }}>
        <SectionLabel>Low-stock parts</SectionLabel>
        <Caption mock>Preview — not yet wired to backend.</Caption>
        {lowStock.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            All tracked parts are at or above minimum stock.
          </div>
        ) : (
          lowStock.map((p) => (
            <div
              key={p.sku}
              style={{ display: "grid", gridTemplateColumns: "130px 1fr 200px 170px", gap: 10, alignItems: "center", padding: "9px 11px", marginBottom: 5, ...rowSurface(false), cursor: "default" }}
            >
              <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{p.sku}</span>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>{p.name}</div>
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{p.loc}</span>
              <StatusChip kind={p.k} label={`${p.onHand} on hand · min ${p.min}`} />
            </div>
          ))
        )}
      </Panel>

      {/* End-of-life planning — REAL data */}
      <Panel>
        <SectionLabel>End-of-life planning · km depreciation</SectionLabel>
        <Caption mock={false}>Fleet API · vehicle registry</Caption>
        {eolSorted.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            No vehicles in service.
          </div>
        ) : (
          eolSorted.map((v) => {
            const lifeKind = lifeKindFor(v.lifeUsedPct);
            const meta = statusMeta(lifeKind);
            const pct = Math.min(100, Math.max(0, v.lifeUsedPct));
            return (
              <div
                key={v.id}
                onClick={() => onOpenVehicle(v.id, "Overview")}
                style={{ display: "grid", gridTemplateColumns: "48px 1fr 170px 140px 160px 150px", gap: 12, alignItems: "center", padding: "11px 13px", marginBottom: 5, ...rowSurface(false) }}
              >
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{v.unitNumber}</span>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                    {v.year} {v.make} {v.model}
                  </div>
                  <StatusChip kind={statusKindFor(v.status)} label={statusLabelFor(v.status)} />
                </div>
                <div>
                  <div style={{ height: 8, borderRadius: 5, background: colors.inputBg, overflow: "hidden", border: `1px solid ${colors.borderSubtle}`, marginBottom: 4 }}>
                    <div style={{ height: "100%", width: `${pct}%`, background: meta.c, borderRadius: 5 }} />
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                    {formatKm(v.odometerKm)} of {formatKm(v.endOfLifeKm)}
                  </div>
                </div>
                <StatusChip kind={lifeKind} label={v.lifeUsedPct >= 100 ? "Life exhausted" : `${Math.round(v.lifeUsedPct)}% used`} />
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
  );
}
