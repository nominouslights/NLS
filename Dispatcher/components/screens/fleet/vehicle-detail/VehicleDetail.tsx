"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import { dtcAlerts, fuelStats, partsInventory } from "@/lib/data";
import { isDisposed, statusKindFor, statusLabelFor } from "@/lib/api";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import VehicleDocuments from "@/components/screens/fleet/VehicleDocuments";
import VehicleServiceHistory from "@/components/screens/fleet/VehicleServiceHistory";
import VehicleWorkOrders from "@/components/screens/fleet/VehicleWorkOrders";
import VehicleInspections from "@/components/screens/fleet/VehicleInspections";
import VehiclePm from "@/components/screens/fleet/VehiclePm";
import OverviewTab, { type OverviewTabProps } from "./OverviewTab";
import { EmptyTabNote, PreviewCaption, TABS, tabIndex, type VehicleOption } from "./shared";

interface VehicleDetailProps extends OverviewTabProps {
  tab: number;
  setTab: (n: number) => void;
  vehicleOptions: VehicleOption[];
}

export default function VehicleDetail({ tab, setTab, vehicleOptions, ...overview }: VehicleDetailProps) {
  const { f } = overview;
  const kind = statusKindFor(f.status);
  const label = statusLabelFor(f.status);
  const readOnly = isDisposed(f.status);

  const unitDtc = dtcAlerts.filter((r) => r.unit === f.unitNumber);
  const unitFuel = fuelStats.filter((r) => r.unit === f.unitNumber);

  return (
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
      <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16, flexWrap: "wrap" }}>
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
              whiteSpace: "nowrap",
            }}
          >
            {t}
          </span>
        ))}
      </div>

      {/* Panels match on tab NAME via tabIndex so an insertion into TABS can
          never silently shift which panel a tab renders. */}
      {tab === tabIndex("Overview") && <OverviewTab {...overview} />}
      {tab === tabIndex("Documents & Compliance") && (
        <VehicleDocuments unit={f.unitNumber} requiresPeriodic={f.requiresPeriodicInspection} />
      )}
      {tab === tabIndex("Service History") && <VehicleServiceHistory unit={f.unitNumber} odometerKm={f.odometerKm} />}
      {tab === tabIndex("Preventive Maintenance") && (
        <VehiclePm vehicle={f} onOpenWorkOrders={() => setTab(tabIndex("Work Orders"))} />
      )}
      {tab === tabIndex("Work Orders") && <VehicleWorkOrders vehicle={f} vehicles={vehicleOptions} />}
      {tab === tabIndex("Inspections") && <VehicleInspections vehicle={f} vehicles={vehicleOptions} />}

      {/* DTC alerts (mock) */}
      {tab === tabIndex("DTC Alerts") && (
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
                <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, textAlign: "right" }}>{r.raised}</span>
              </div>
            ))
          )}
        </div>
      )}

      {/* Parts (mock, fleet-wide) */}
      {tab === tabIndex("Parts") && (
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
                <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>{p.name}</div>
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{p.loc}</div>
              </div>
              <StatusChip kind={p.k} label={`${p.onHand} on hand · min ${p.min}`} />
            </div>
          ))}
        </div>
      )}

      {/* Fuel & route (mock) */}
      {tab === tabIndex("Fuel & Route") && (
        <div>
          <PreviewCaption />
          {unitFuel.length === 0 ? (
            <EmptyTabNote>{`No fuel or route telemetry for ${f.unitNumber}.`}</EmptyTabNote>
          ) : (
            unitFuel.map((r) => (
              <div key={r.unit} style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
                <Panel>
                  <SectionLabel>Fuel economy</SectionLabel>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright }}>{r.l100}</div>
                  <div style={{ marginTop: 9 }}>
                    <StatusChip kind={r.k} label={r.k === "over" ? "Above fleet baseline" : r.k === "soon" ? "Watch" : "Within baseline"} />
                  </div>
                </Panel>
                <Panel>
                  <SectionLabel>Idle time</SectionLabel>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright }}>{r.idlePct}</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 9 }}>Share of engine-on time spent idling</div>
                </Panel>
                <Panel>
                  <SectionLabel>Route adherence</SectionLabel>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, color: colors.headingBright }}>{r.routeAdherence}</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 9 }}>Corridor plan vs actual GPS trace</div>
                </Panel>
              </div>
            ))
          )}
        </div>
      )}
    </div>
  );
}
