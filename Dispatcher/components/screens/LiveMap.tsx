"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";

const vehicles = [
  {
    unit: "INT-3000 · U-02",
    color: colors.amber,
    active: true,
    driver: "D. Chartrand · Alamos crew",
    next: "Next: Leaf Rapids · ETA 08:05",
  },
  {
    unit: "Transit · U-04",
    color: colors.blue,
    active: false,
    driver: "M. Okimaw · NIHB medical",
    next: "Next: Thompson · ETA 09:40",
  },
  {
    unit: "Transit · U-05",
    color: "#7A8899",
    active: false,
    driver: "J. Spence · Community",
    next: "Position stale · last ping 14m ago",
  },
];

const communityNodes = [
  { name: "South Indian Lake", left: "17.5%", top: "21.4%", hub: false },
  { name: "Lynn Lake", left: "37.5%", top: "39.3%", hub: false },
  { name: "Thompson · hub", left: "53.75%", top: "53.6%", hub: true },
  { name: "Leaf Rapids", left: "70%", top: "44.6%", hub: false },
  { name: "Black Sturgeon Falls", left: "82.5%", top: "76.8%", hub: false },
];

const vehicleMarkers = [
  { left: "45%", top: "47%", size: 20, color: colors.amber },
  { left: "28%", top: "30%", size: 20, color: colors.blue },
  { left: "76%", top: "60%", size: 18, color: "#7A8899" },
];

export default function LiveMap({ onOpenTrip }: { onOpenTrip: (i: number) => void }) {
  return (
    <div style={{ display: "flex", height: "100%" }} className="detailfade">
      {/* vehicle rail */}
      <div
        style={{
          width: 280,
          flex: "none",
          background: colors.railBg,
          borderRight: `1px solid ${colors.border}`,
          display: "flex",
          flexDirection: "column",
          overflow: "hidden",
        }}
      >
        <div style={{ flex: "none", padding: "18px 18px 12px", borderBottom: `1px solid ${colors.border}` }}>
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
            Live Dispatch Map
          </div>
          <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: colors.headingBright }}>3 active vehicles</div>
        </div>
        <div style={{ flex: 1, overflowY: "auto", padding: 12 }}>
          {vehicles.map((v) => (
            <div
              key={v.unit}
              style={{
                padding: "12px 13px",
                marginBottom: 7,
                borderRadius: 9,
                background: v.active ? colors.cardBgActive : colors.cardBg,
                border: `1px solid ${v.active ? colors.borderActive : colors.borderSubtle}`,
                boxShadow: v.active ? `inset 3px 0 0 ${v.color}, ${colors.shadowCard}` : colors.shadowCard,
                cursor: "pointer",
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 5 }}>
                <span
                  style={{
                    width: 10,
                    height: 10,
                    borderRadius: "50%",
                    background: v.color,
                    boxShadow: v.active ? `0 0 0 3px ${v.color}33` : undefined,
                  }}
                />
                <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{v.unit}</span>
              </div>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textPrimary, fontWeight: 500 }}>{v.driver}</div>
              <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textMuted, marginTop: 3 }}>{v.next}</div>
            </div>
          ))}
        </div>
        <div
          style={{
            flex: "none",
            padding: "12px 16px",
            borderTop: `1px solid ${colors.border}`,
            fontFamily: fonts.body,
            fontSize: 11,
            color: colors.textDim,
            display: "flex",
            flexDirection: "column",
            gap: 6,
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ width: 9, height: 9, borderRadius: "50%", background: colors.amber }} />
            Corporate / Alamos
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ width: 9, height: 9, borderRadius: "50%", background: colors.blue }} />
            Community / NIHB
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <span style={{ width: 9, height: 9, borderRadius: "50%", background: "#7A8899" }} />
            Offline / stale
          </div>
        </div>
      </div>

      {/* map canvas */}
      <div style={{ flex: 1, position: "relative", background: "radial-gradient(circle at 40% 35%, #FDFEFF, #DFE8F1 70%)", overflow: "hidden" }}>
        {/* stale banner */}
        <div
          style={{
            position: "absolute",
            top: 16,
            left: "50%",
            transform: "translateX(-50%)",
            zIndex: 5,
            display: "flex",
            alignItems: "center",
            gap: 9,
            padding: "8px 16px",
            borderRadius: 9,
            background: "rgba(225,176,0,.12)",
            border: "1px solid rgba(225,176,0,.35)",
            backdropFilter: "blur(4px)",
          }}
        >
          <span
            style={{
              width: 14,
              height: 14,
              borderRadius: 4,
              background: "#E1B000",
              color: colors.navy,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 9,
              fontWeight: 800,
            }}
          >
            ◐
          </span>
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: statusMeta("soon").t, fontWeight: 500 }}>
            1 vehicle position stale — Starlink connectivity intermittent near South Indian Lake
          </span>
        </div>

        <svg viewBox="0 0 800 560" preserveAspectRatio="xMidYMid meet" style={{ position: "absolute", inset: 0, width: "100%", height: "100%" }}>
          <polyline
            points="140,120 300,220 430,300 560,250 660,430"
            fill="none"
            stroke={colors.borderStrong}
            strokeWidth={3}
            strokeDasharray="2 8"
            strokeLinecap="round"
          />
          <polyline points="140,120 300,220 430,300" fill="none" stroke={colors.blue} strokeWidth={3} strokeLinecap="round" opacity={0.7} />
        </svg>

        {communityNodes.map((n) => (
          <div key={n.name} style={{ position: "absolute", left: n.left, top: n.top, transform: "translate(-50%,-50%)", textAlign: "center" }}>
            <span
              style={{
                display: "block",
                width: n.hub ? 14 : 12,
                height: n.hub ? 14 : 12,
                borderRadius: "50%",
                background: "#FFFFFF",
                border: `2px solid ${n.hub ? colors.amber : colors.skyBlue}`,
                margin: "0 auto",
              }}
            />
            <span
              style={{
                fontFamily: n.hub ? fonts.body : fonts.semiCondensed,
                fontWeight: n.hub ? 700 : undefined,
                fontSize: n.hub ? 12 : 11,
                letterSpacing: n.hub ? undefined : ".06em",
                color: n.hub ? colors.headingBright : colors.textSecondary,
                whiteSpace: "nowrap",
                marginTop: 5,
                display: "block",
              }}
            >
              {n.name}
            </span>
          </div>
        ))}

        {vehicleMarkers.map((v, i) => (
          <div
            key={i}
            style={{
              position: "absolute",
              left: v.left,
              top: v.top,
              transform: "translate(-50%,-50%)",
              width: v.size,
              height: v.size,
              borderRadius: "50%",
              background: v.color,
              border: "2px solid #FFFFFF",
              boxShadow: v.color !== "#7A8899" ? `0 0 0 4px ${v.color}48` : undefined,
            }}
          />
        ))}

        {/* floating card */}
        <div
          style={{
            position: "absolute",
            right: 22,
            bottom: 22,
            width: 250,
            padding: "15px 16px",
            borderRadius: 12,
            background: "rgba(255,255,255,.95)",
            border: `1px solid ${colors.border}`,
            backdropFilter: "blur(6px)",
            boxShadow: colors.shadowPop,
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
            <span style={{ width: 10, height: 10, borderRadius: "50%", background: colors.amber }} />
            <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>INT-3000 · U-02</span>
            <span style={{ marginLeft: "auto", fontFamily: fonts.body, fontSize: 11, color: statusMeta("ontime").t, fontWeight: 600 }}>On route</span>
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary, marginBottom: 2 }}>D. Chartrand · TR-4821</div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textMuted, marginBottom: 11 }}>
            Next stop Leaf Rapids · ETA 08:05 · 18/24 crew
          </div>
          <div style={{ display: "flex", gap: 8 }}>
            <span
              onClick={() => onOpenTrip(0)}
              style={{
                flex: 1,
                textAlign: "center",
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 12,
                letterSpacing: ".03em",
                padding: 7,
                borderRadius: 7,
                background: colors.blue,
                color: "#FFFFFF",
                cursor: "pointer",
              }}
            >
              OPEN TRIP
            </span>
            <span
              style={{
                flex: 1,
                textAlign: "center",
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 12,
                letterSpacing: ".03em",
                padding: 7,
                borderRadius: 7,
                background: "transparent",
                border: `1px solid ${colors.borderActive}`,
                color: colors.textSecondary,
                cursor: "pointer",
              }}
            >
              MESSAGE
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
