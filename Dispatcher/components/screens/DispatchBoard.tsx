"use client";

import { colors, fonts, rowSurface, svcMeta, SERVICE_SHORT, statusMeta } from "@/lib/theme";
import { trips } from "@/lib/data";
import { statusShort } from "@/lib/format";
import { MetricTile } from "@/components/ui/MetricTile";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";

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
              color: colors.textFaint,
              marginBottom: 3,
            }}
          >
            Operations · Tuesday, July 7 2026
          </div>
          <h1 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: colors.headingBright, margin: 0 }}>
            Dispatch Board
          </h1>
        </div>
        <div style={{ display: "flex", alignItems: "center", gap: 9, fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
          <span style={{ width: 7, height: 7, borderRadius: "50%", background: "#009E73", boxShadow: "0 0 0 3px rgba(0,158,115,.16)" }} />
          Live · updated 8s ago
        </div>
      </div>

      {/* scroll body */}
      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "0 26px 26px" }}>
        {/* STATUS STRIP */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(6,1fr)", gap: 12, marginBottom: 18 }}>
          <MetricTile icon="●" iconBg={colors.blue} iconColor="#FFFFFF" label="Trips today" value={8} valueColor={colors.textPrimary} />
          <MetricTile icon="●" iconBg={colors.blue} iconColor="#FFFFFF" label="In progress" value={2} valueColor={colors.skyBlue} />
          <MetricTile
            icon="◐"
            iconBg="#E1B000"
            iconColor={colors.navy}
            label="Open · unclaimed"
            value={3}
            valueColor={statusMeta("soon").t}
            borderColor="rgba(225,176,0,.4)"
          />
          <MetricTile icon="✓" iconBg="#009E73" iconColor="#FFFFFF" label="Drivers on duty" value={5} valueColor={statusMeta("ontime").t} />
          <MetricTile
            icon="▲"
            iconBg="#D55E00"
            iconColor="#fff"
            label="Compliance alerts"
            value={2}
            valueColor={statusMeta("over").t}
            borderColor="rgba(213,94,0,.3)"
          />
          <MetricTile
            icon="▲"
            iconBg="#D55E00"
            iconColor="#fff"
            label="Overdue invoices"
            value={1}
            valueColor={statusMeta("over").t}
            borderColor="rgba(213,94,0,.3)"
          />
        </div>

        {/* BOARD */}
        <div>
          {/* trip board */}
          <div>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 11 }}>
              <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: colors.textSecondary }}>
                TODAY&rsquo;S TRIPS
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
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
                color: colors.textFaint,
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
                    <div style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{t.id}</div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 2 }}>{t.win}</div>
                  </div>
                  <div style={{ minWidth: 0 }}>
                    <ServiceChip svc={t.svc} label={SERVICE_SHORT[t.svc]} />
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
                      {t.stops.join("  →  ")}
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
                      {t.km} km · {t.vehicle}
                    </div>
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 12.5,
                      lineHeight: 1.3,
                      fontWeight: t.open ? 600 : 500,
                      color: t.open ? colors.amberText : colors.textSecondary,
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
        </div>
      </div>
    </div>
  );
}
