"use client";

import { fonts } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";

const messages = [
  {
    title: "Driver dispatch · D. Chartrand",
    status: "Sent",
    ok: true,
    body: "TR-4821 assigned. Depart Thompson 06:30, Alamos crew shuttle to Lynn Lake.",
    time: "Jul 6 · 14:40",
  },
  {
    title: "Passenger · Eleanor Bighetty",
    status: "Sent",
    ok: true,
    body: "NIHB voucher confirmed. Your medical trip Jul 7 is booked at no cost. Escort included.",
    time: "Jul 6 · 09:12",
  },
  {
    title: "Passenger · Priya Sandhu",
    status: "Delivery failed · retry",
    ok: false,
    body: "Community run not yet viable — 2 more seats needed for Fri departure.",
    time: "Jul 7 · 07:50",
  },
];

const templates = [
  "Driver assignment notification",
  "Trip change / cancellation",
  "NIHB voucher confirmation",
  "Gift-a-Seat total offer",
  "Client incident disclosure",
  "Contract renewal discussion",
];

export default function Communications() {
  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Business · Outbound messaging & templates" title="Communications" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "1fr 380px", borderTop: "1px solid #1E3350" }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "18px 22px", borderRight: "1px solid #1E3350" }}>
          <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: "#c2d0e0", marginBottom: 12 }}>
            MESSAGE LOG
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {messages.map((m) => (
              <div
                key={m.title}
                style={{
                  padding: "12px 15px",
                  borderRadius: 10,
                  background: "#0F1E33",
                  border: `1px solid ${m.ok ? "#152941" : "rgba(213,94,0,.28)"}`,
                }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", marginBottom: 4 }}>
                  <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: "#E8EEF5" }}>{m.title}</span>
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 5,
                      fontFamily: fonts.body,
                      fontWeight: 600,
                      fontSize: 10.5,
                      color: m.ok ? "#38d3a6" : "#f0803f",
                    }}
                  >
                    <span
                      style={{
                        width: 13,
                        height: 13,
                        borderRadius: 4,
                        background: m.ok ? "#14B88A" : "#D55E00",
                        color: m.ok ? "#04231a" : "#fff",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontSize: 8,
                        fontWeight: 800,
                      }}
                    >
                      {m.ok ? "✓" : "▲"}
                    </span>
                    {m.status}
                  </span>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8", lineHeight: 1.5 }}>{m.body}</div>
                <div style={{ fontFamily: fonts.mono, fontSize: 10, color: "#6B8099", marginTop: 5 }}>{m.time}</div>
              </div>
            ))}
          </div>
        </div>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "18px 22px" }}>
          <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: "#c2d0e0", marginBottom: 12 }}>
            TEMPLATE LIBRARY
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 7, marginBottom: 18 }}>
            {templates.map((t) => (
              <div
                key={t}
                style={{
                  padding: "10px 13px",
                  borderRadius: 9,
                  background: "#0F1E33",
                  border: "1px solid #152941",
                  fontFamily: fonts.body,
                  fontSize: 12.5,
                  color: "#c2d0e0",
                  cursor: "pointer",
                }}
              >
                {t}
              </div>
            ))}
          </div>
          <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: "#c2d0e0", marginBottom: 11 }}>
            COMPOSE
          </div>
          <div style={{ padding: "14px 15px", borderRadius: 11, background: "#0F1E33", border: "1px solid #1E3350" }}>
            <div
              style={{
                height: 120,
                borderRadius: 8,
                background: "#0A1729",
                border: "1px solid #152941",
                padding: "11px 13px",
                fontFamily: fonts.body,
                fontSize: 12.5,
                color: "#6B8099",
                lineHeight: 1.5,
                marginBottom: 11,
              }}
            >
              Professional, safety-first, transparent tone. Canadian placeholders throughout…
            </div>
            <span
              style={{
                display: "inline-flex",
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 13,
                letterSpacing: ".03em",
                padding: "8px 16px",
                borderRadius: 8,
                background: "#3B8DD4",
                color: "#04121f",
                cursor: "pointer",
              }}
            >
              QUEUE MESSAGE
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
