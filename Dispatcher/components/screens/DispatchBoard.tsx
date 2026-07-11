"use client";

import { fonts, rowSurface, svcMeta, SERVICE_SHORT, statusMeta } from "@/lib/theme";
import { trips } from "@/lib/data";
import { statusShort } from "@/lib/format";
import { MetricTile } from "@/components/ui/MetricTile";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";

const exceptions = [
  {
    kind: "over" as const,
    title: "Open trip · no eligible driver",
    body: "TR-4827 Cargo → Leaf Rapids. All eligible drivers at HOS limit.",
  },
  {
    kind: "over" as const,
    title: "DVIR failed · U-01",
    body: "International 3000 out of service — steering fault. Removed from eligibility.",
  },
  {
    kind: "soon" as const,
    title: "HOS at limit · D. Chartrand",
    body: "4h 20m remaining. Not eligible for a second corridor run today.",
  },
  {
    kind: "soon" as const,
    title: "Clearance expiring · R. Flett",
    body: "Class 4 medical lapses in 12 days. Renewal reminder queued.",
  },
  {
    kind: "soon" as const,
    title: "Contract renewal · Alamos Gold",
    body: "Term ends in 21 days. Brendan D'Allaire flagged for discussion.",
  },
  {
    kind: "info" as const,
    title: "Empty leg available",
    body: "TR-4824 Alamos return leg Lynn Lake → Thompson open for community / grocery fill.",
  },
];

export default function DispatchBoard({ onOpenTrip }: { onOpenTrip: (i: number) => void }) {
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
              color: "#4d688a",
              marginBottom: 3,
            }}
          >
            Operations · Tuesday, July 7 2026
          </div>
          <h1 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: "#F2F6FB", margin: 0 }}>
            Dispatch Board
          </h1>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 9, fontFamily: fonts.body, fontSize: 12, color: "#6B8099" }}>
          <span style={{ width: 7, height: 7, borderRadius: "50%", background: "#14B88A", boxShadow: "0 0 0 3px rgba(20,184,138,.16)" }} />
          Live · updated 8s ago
        </div>
      </div>

      {/* scroll body */}
      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "0 26px 26px" }}>
        {/* STATUS STRIP */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6,1fr)", gap: 12, marginBottom: 18 }}>
          <MetricTile icon="●" iconBg="#3B8DD4" iconColor="#04121f" label="Trips today" value={8} valueColor="#E8EEF5" />
          <MetricTile icon="●" iconBg="#3B8DD4" iconColor="#04121f" label="In progress" value={2} valueColor="#7EC8F0" />
          <MetricTile
            icon="◐"
            iconBg="#E1B000"
            iconColor="#2e2400"
            label="Open · unclaimed"
            value={3}
            valueColor="#ecc94b"
            borderColor="rgba(225,176,0,.3)"
          />
          <MetricTile icon="✓" iconBg="#14B88A" iconColor="#04231a" label="Drivers on duty" value={5} valueColor="#38d3a6" />
          <MetricTile
            icon="▲"
            iconBg="#D55E00"
            iconColor="#fff"
            label="Compliance alerts"
            value={2}
            valueColor="#f0803f"
            borderColor="rgba(213,94,0,.3)"
          />
          <MetricTile
            icon="▲"
            iconBg="#D55E00"
            iconColor="#fff"
            label="Overdue invoices"
            value={1}
            valueColor="#f0803f"
            borderColor="rgba(213,94,0,.3)"
          />
        </div>

        {/* BOARD + EXCEPTIONS */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 320px", gap: 16 }}>
          {/* trip board */}
          <div>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 11 }}>
              <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: "#c2d0e0" }}>
                TODAY&rsquo;S TRIPS
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#6B8099" }}>
                Thompson · Leaf Rapids · Lynn Lake · South Indian Lake · Black Sturgeon Falls
              </div>
            </div>
            {/* column head */}
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "74px 112px minmax(0,1fr) 116px 120px",
                gap: 10,
                padding: "0 14px 8px",
                fontFamily: fonts.semiCondensed,
                fontSize: 9.5,
                letterSpacing: ".12em",
                textTransform: "uppercase",
                color: "#4d688a",
              }}
            >
              <div>Trip</div>
              <div>Service</div>
              <div>Corridor</div>
              <div>Driver</div>
              <div>Status</div>
            </div>
            {trips.map((t, i) => {
              const sc = svcMeta(t.svc);
              return (
                <div
                  key={t.id}
                  onClick={() => onOpenTrip(i)}
                  style={{
                    display: "grid",
                    gridTemplateColumns: "74px 112px minmax(0,1fr) 116px 120px",
                    gap: 10,
                    alignItems: "center",
                    padding: "11px 14px",
                    marginBottom: 5,
                    ...rowSurface(false, sc.accent),
                  }}
                >
                  <div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 11, color: "#7EC8F0" }}>{t.id}</div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 10, color: "#6B8099", marginTop: 2 }}>{t.win}</div>
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <ServiceChip svc={t.svc} label={SERVICE_SHORT[t.svc]} />
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12.5,
                        color: "#c2d0e0",
                        fontWeight: 500,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {t.stops.join("  →  ")}
                    </div>
                    <div
                      style={{
                        fontFamily: fonts.mono,
                        fontSize: 10,
                        color: "#6B8099",
                        marginTop: 2,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {t.km} km · {t.vehicle}
                    </div>
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      lineHeight: 1.3,
                      fontWeight: t.open ? 600 : 500,
                      color: t.open ? "#E8A020" : "#c2d0e0",
                    }}
                  >
                    {t.driver ?? "OPEN — needs coverage"}
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <StatusChip kind={t.sk} label={statusShort(t.status)} />
                  </div>
                </div>
              );
            })}
          </div>

          {/* exceptions rail */}
          <div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: "#c2d0e0", marginBottom: 11 }}>
              EXCEPTIONS
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
              {exceptions.map((ex, i) => {
                const m = statusMeta(ex.kind);
                return (
                  <div
                    key={i}
                    style={{
                      padding: "12px 14px",
                      borderRadius: 10,
                      background: "#0F1E33",
                      border: `1px solid ${m.bd}`,
                      borderLeft: `3px solid ${m.c}`,
                    }}
                  >
                    <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 5 }}>
                      <span
                        style={{
                          width: 15,
                          height: 15,
                          borderRadius: 4,
                          background: m.c,
                          color: ex.kind === "info" ? "#04121f" : "#fff",
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          fontSize: 9,
                          fontWeight: 800,
                        }}
                      >
                        {m.g}
                      </span>
                      <span style={{ fontFamily: fonts.body, fontWeight: 700, fontSize: 12.5, color: m.t }}>{ex.title}</span>
                    </div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, lineHeight: 1.45, color: "#9fb2c8" }}>{ex.body}</div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
