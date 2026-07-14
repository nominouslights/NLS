"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";

const routes = [
  { name: "Thompson ⇄ Lynn Lake", stops: 3, km: 198, duration: "~3h 25m" },
  { name: "Thompson ⇄ South Indian Lake", stops: 2, km: 220, duration: "~3h 40m" },
  { name: "Thompson ⇄ Black Sturgeon Falls", stops: 2, km: 142, duration: "~2h 35m" },
];

const templates = [
  { name: "Daily Alamos crew shuttle", status: "Active · generates 7 days ahead", color: statusMeta("ontime").t },
  { name: "Weekly grocery delivery", status: "◐ Wed noon cutoff → Fri delivery", color: statusMeta("soon").t },
];

const weekDays = [
  { day: "MON", labelColor: colors.textDim, bg: "rgba(232,160,32,.12)", bd: "rgba(232,160,32,.3)", tx: colors.amberText, content: "Alamos\n06:30" },
  { day: "TUE", labelColor: colors.textDim, bg: "rgba(232,160,32,.12)", bd: "rgba(232,160,32,.3)", tx: colors.amberText, content: "Alamos\n06:30" },
  { day: "WED", labelColor: statusMeta("soon").t, bg: "rgba(225,176,0,.14)", bd: "rgba(225,176,0,.4)", tx: statusMeta("soon").t, content: "◐ Grocery\ncutoff 12:00" },
  { day: "THU", labelColor: colors.textDim, bg: "rgba(232,160,32,.12)", bd: "rgba(232,160,32,.3)", tx: colors.amberText, content: "Alamos\n06:30" },
  { day: "FRI", labelColor: colors.skyBlue, bg: "rgba(31,111,178,.14)", bd: "rgba(31,111,178,.4)", tx: colors.skyBlue, content: "Grocery\ndelivery" },
  { day: "SAT", labelColor: colors.textDim, bg: colors.inputBg, bd: colors.borderSubtle, tx: "", content: "" },
  { day: "SUN", labelColor: colors.textDim, bg: colors.inputBg, bd: colors.borderSubtle, tx: "", content: "" },
];

export default function RoutesSchedules() {
  const [sel, setSel] = useState(0);
  const route = routes[sel];

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Operations · Corridor routes & recurring schedule templates" title="Routes & Schedules" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "34% 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".14em",
              textTransform: "uppercase",
              color: colors.textFaint,
              marginBottom: 10,
            }}
          >
            Routes
          </div>
          {routes.map((r, i) => {
            const active = i === sel;
            return (
              <div
                key={r.name}
                onClick={() => setSel(i)}
                style={{
                  padding: "12px 14px",
                  marginBottom: 5,
                  borderRadius: 9,
                  border: `1px solid ${active ? colors.borderActive : colors.borderSubtle}`,
                  background: active ? colors.cardBgActive : colors.cardBg,
                  boxShadow: active ? `inset 3px 0 0 ${colors.blue}, ${colors.shadowCard}` : colors.shadowCard,
                  cursor: "pointer",
                }}
              >
                <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>{r.name}</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim, marginTop: 3 }}>
                  {r.stops} stops · {r.km} km · {r.duration}
                  {i === 0 ? " · Class 4" : ""}
                </div>
              </div>
            );
          })}
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".14em",
              textTransform: "uppercase",
              color: colors.textFaint,
              margin: "16px 0 10px",
            }}
          >
            Schedule templates
          </div>
          {templates.map((t) => (
            <div
              key={t.name}
              style={{
                padding: "12px 14px",
                marginBottom: 5,
                borderRadius: 9,
                border: `1px solid ${colors.borderSubtle}`,
                background: colors.cardBg,
                boxShadow: colors.shadowCard,
                cursor: "pointer",
              }}
            >
              <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>{t.name}</div>
              <div style={{ fontFamily: fonts.body, fontSize: 11, color: t.color, marginTop: 3 }}>{t.status}</div>
            </div>
          ))}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={route.name}>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1, color: colors.headingBright, margin: "0 0 4px" }}>
              {route.name}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 16 }}>
              Ordered stops · {route.km} km · {route.duration} typical · Class 4 implied
            </div>
            <div style={{ padding: "15px 16px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 11, marginBottom: 14, boxShadow: colors.shadowCard }}>
              <div
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".14em",
                  textTransform: "uppercase",
                  color: colors.textLabel,
                  marginBottom: 12,
                }}
              >
                Weekly schedule
              </div>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(7,1fr)", gap: 7 }}>
                {weekDays.map((wd) => (
                  <div key={wd.day} style={{ textAlign: "center" }}>
                    <div style={{ fontFamily: fonts.semiCondensed, fontSize: 10, letterSpacing: ".06em", color: wd.labelColor, marginBottom: 6 }}>
                      {wd.day}
                    </div>
                    <div
                      style={{
                        height: 52,
                        borderRadius: 7,
                        background: wd.bg,
                        border: `1px solid ${wd.bd}`,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontFamily: fonts.body,
                        fontSize: wd.day === "WED" ? 9.5 : 10,
                        color: wd.tx,
                        textAlign: "center",
                        lineHeight: 1.2,
                        whiteSpace: "pre-line",
                      }}
                    >
                      {wd.content}
                    </div>
                  </div>
                ))}
              </div>
            </div>
            <div
              style={{
                padding: "12px 15px",
                background: "rgba(31,111,178,.07)",
                border: "1px solid rgba(31,111,178,.25)",
                borderRadius: 10,
                fontFamily: fonts.body,
                fontSize: 12,
                lineHeight: 1.55,
                color: colors.textMuted,
              }}
            >
              Editing a template applies to <strong style={{ color: colors.textPrimary }}>future generated trips only</strong> — past and assigned
              trips retain their original detail. Distance &amp; duration feed the HOS feasibility checks used at trip creation.
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
