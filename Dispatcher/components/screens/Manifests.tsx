"use client";

import { useState } from "react";
import { fonts, rowSurface } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";
import { ServiceChip } from "@/components/ui/Chip";

interface ManifestPassenger {
  name: string;
  status: "Confirmed" | "Pending";
}

interface ManifestRecord {
  id: string;
  svc: "community" | "nihb" | "alamos";
  svcLabel: string;
  corridor: string;
  win: string;
  km: number;
  vehicle: string;
  listBadge: { label: string; bg: string; bd: string; tx: string; glyph: string };
  capacitySeats: number;
  passengers: ManifestPassenger[];
  demand: { confirmed: number; needed: number } | null;
}

const manifestRecords: ManifestRecord[] = [
  {
    id: "TR-4823",
    svc: "community",
    svcLabel: "Community",
    corridor: "Leaf Rapids → Thompson",
    win: "Fri Jul 7 · 11:00 → 14:20",
    km: 174,
    vehicle: "Transit T-150 (7-seat)",
    listBadge: { label: "Not yet viable · 2 / 4", bg: "rgba(225,176,0,.12)", bd: "rgba(225,176,0,.35)", tx: "#ecc94b", glyph: "◐" },
    capacitySeats: 7,
    passengers: [
      { name: "Marcel Dumas · Leaf Rapids", status: "Confirmed" },
      { name: "Priya Sandhu · Thompson", status: "Pending" },
    ],
    demand: { confirmed: 2, needed: 4 },
  },
  {
    id: "TR-4822",
    svc: "nihb",
    svcLabel: "NIHB Medical",
    corridor: "South Indian Lake → Thompson",
    win: "Fri Jul 7 · 07:15 → 09:40",
    km: 220,
    vehicle: "Transit T-150 (7-seat)",
    listBadge: { label: "Confirmed · voucher", bg: "rgba(20,184,138,.13)", bd: "rgba(20,184,138,.35)", tx: "#38d3a6", glyph: "✓" },
    capacitySeats: 7,
    passengers: [
      { name: "Eleanor Bighetty · South Indian Lake", status: "Confirmed" },
      { name: "Escort · authorized", status: "Confirmed" },
    ],
    demand: null,
  },
  {
    id: "TR-4821",
    svc: "alamos",
    svcLabel: "Alamos",
    corridor: "Thompson → Lynn Lake",
    win: "Tue Jul 7 · 06:30 → 09:55",
    km: 198,
    vehicle: "International 3000 (24-seat)",
    listBadge: { label: "Crew · 18 / 24", bg: "rgba(59,141,212,.12)", bd: "rgba(59,141,212,.4)", tx: "#7EC8F0", glyph: "●" },
    capacitySeats: 24,
    passengers: [],
    demand: null,
  },
];

export default function Manifests() {
  const [sel, setSel] = useState(0);
  const [giftShown, setGiftShown] = useState(false);
  const m = manifestRecords[sel];

  function select(i: number) {
    setSel(i);
    setGiftShown(false);
  }

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Operations · Manifests, demand activation & run economics" title="Manifests & Demand" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "32% 1fr", borderTop: "1px solid #1E3350" }}>
        {/* master */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: "1px solid #1E3350" }}>
          {manifestRecords.map((rec, i) => {
            const active = i === sel;
            return (
              <div
                key={rec.id}
                onClick={() => select(i)}
                style={{ padding: "12px 14px", marginBottom: 5, ...rowSurface(active, "#3B8DD4") }}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 6 }}>
                  <ServiceChip svc={rec.svc} label={rec.svcLabel} />
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: "#7EC8F0" }}>{rec.id}</span>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: "#E8EEF5" }}>{rec.corridor}</div>
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
                      background: rec.listBadge.bg,
                      border: `1px solid ${rec.listBadge.bd}`,
                      color: rec.listBadge.tx,
                    }}
                  >
                    {rec.listBadge.glyph} {rec.listBadge.label}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        {/* detail */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: "#0C1A2C" }}>
          <div className="detailfade" key={m.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 4 }}>
              <ServiceChip svc={m.svc} label={m.svcLabel} />
              <span style={{ fontFamily: fonts.mono, fontSize: 13, color: "#7EC8F0", marginLeft: "auto" }}>{m.id}</span>
            </div>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1.05, color: "#F2F6FB", margin: "6px 0 4px" }}>
              {m.corridor}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: "#9fb2c8", marginBottom: 16 }}>
              {m.win} · {m.km} km · {m.vehicle}
            </div>

            {/* demand meter — community runs only */}
            {m.demand && (
              <div style={{ padding: "16px 18px", background: "#0F1E33", border: "1px solid rgba(225,176,0,.3)", borderRadius: 12, marginBottom: 14 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 11 }}>
                  <div
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontSize: 9.5,
                      letterSpacing: ".14em",
                      textTransform: "uppercase",
                      color: "#8fa6c0",
                    }}
                  >
                    Demand meter · {m.demand.needed}-passenger minimum
                  </div>
                  <span
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 6,
                      padding: "3px 10px",
                      borderRadius: 6,
                      fontFamily: fonts.body,
                      fontWeight: 600,
                      fontSize: 12,
                      background: "rgba(225,176,0,.13)",
                      border: "1px solid rgba(225,176,0,.35)",
                      color: "#ecc94b",
                    }}
                  >
                    <span
                      style={{
                        width: 16,
                        height: 16,
                        borderRadius: 4,
                        background: "#E1B000",
                        color: "#2e2400",
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontSize: 10,
                        fontWeight: 800,
                      }}
                    >
                      ◐
                    </span>
                    Not yet viable
                  </span>
                </div>
                <div style={{ display: "flex", gap: 6, marginBottom: 9 }}>
                  {Array.from({ length: m.demand.needed }).map((_, i) => (
                    <div
                      key={i}
                      style={{
                        flex: 1,
                        height: 14,
                        borderRadius: 4,
                        background: i < m.demand!.confirmed ? "#14B88A" : "#0A1729",
                        border: i < m.demand!.confirmed ? undefined : "1px dashed #3B5573",
                      }}
                    />
                  ))}
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8" }}>
                  <strong style={{ color: "#E8EEF5" }}>{m.demand.confirmed} confirmed</strong> · {m.demand.needed - m.demand.confirmed}{" "}
                  more needed to activate this run
                </div>
              </div>
            )}

            {/* passenger list */}
            {m.passengers.length > 0 && (
              <div style={{ padding: "15px 16px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 11, marginBottom: 14 }}>
                <div
                  style={{
                    fontFamily: fonts.semiCondensed,
                    fontSize: 9.5,
                    letterSpacing: ".14em",
                    textTransform: "uppercase",
                    color: "#8fa6c0",
                    marginBottom: 11,
                  }}
                >
                  Passenger manifest · {m.passengers.length} / {m.capacitySeats} seats
                </div>
                {m.passengers.map((p, i) => (
                  <div
                    key={p.name}
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      padding: "9px 0",
                      borderBottom: i < m.passengers.length - 1 ? "1px solid #152941" : undefined,
                    }}
                  >
                    <span style={{ fontFamily: fonts.body, fontSize: 13, color: "#E8EEF5" }}>{p.name}</span>
                    <span
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 5,
                        fontFamily: fonts.body,
                        fontWeight: 600,
                        fontSize: 11,
                        color: p.status === "Confirmed" ? "#38d3a6" : "#ecc94b",
                      }}
                    >
                      <span
                        style={{
                          width: 14,
                          height: 14,
                          borderRadius: 4,
                          background: p.status === "Confirmed" ? "#14B88A" : "#E1B000",
                          color: p.status === "Confirmed" ? "#04231a" : "#2e2400",
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          fontSize: 9,
                          fontWeight: 800,
                        }}
                      >
                        {p.status === "Confirmed" ? "✓" : "◐"}
                      </span>
                      {p.status}
                    </span>
                  </div>
                ))}
              </div>
            )}

            {/* GIFT-A-SEAT — community only */}
            {m.svc === "community" && (
              <div
                style={{
                  padding: "16px 18px",
                  background: "linear-gradient(180deg,rgba(59,141,212,.08),transparent)",
                  border: "1px solid #2f557d",
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
                      background: "#3B8DD4",
                      color: "#04121f",
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      fontSize: 13,
                      fontWeight: 800,
                    }}
                  >
                    ◆
                  </span>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 17, color: "#7EC8F0" }}>Gift-a-Seat guarantee</div>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, lineHeight: 1.55, color: "#9fb2c8", marginBottom: 13 }}>
                  A passenger can cover the remaining seats to guarantee this run departs. The{" "}
                  <strong style={{ color: "#E8EEF5" }}>exact total is shown before any commitment</strong>.
                </div>
                {giftShown ? (
                  <>
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        padding: "13px 16px",
                        background: "#0A1729",
                        border: "1px solid #2f557d",
                        borderRadius: 10,
                        marginBottom: 12,
                      }}
                    >
                      <div>
                        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#6B8099", marginBottom: 3 }}>
                          2 seats × $67.00 community fare (CAD)
                        </div>
                        <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: "#38d3a6", fontVariantNumeric: "tabular-nums" }}>
                          $134.00
                        </div>
                      </div>
                      <span
                        style={{
                          fontFamily: fonts.condensed,
                          fontWeight: 700,
                          fontSize: 13,
                          letterSpacing: ".03em",
                          padding: "10px 18px",
                          borderRadius: 9,
                          background: "#14B88A",
                          color: "#04231a",
                          cursor: "pointer",
                        }}
                      >
                        CONFIRM GUARANTEE
                      </span>
                    </div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#6B8099" }}>
                      Total shown above — guarantee cannot be confirmed until this figure is displayed.
                    </div>
                  </>
                ) : (
                  <span
                    onClick={() => setGiftShown(true)}
                    style={{
                      display: "inline-flex",
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 13,
                      letterSpacing: ".03em",
                      padding: "10px 18px",
                      borderRadius: 9,
                      background: "#3B8DD4",
                      color: "#04121f",
                      cursor: "pointer",
                    }}
                  >
                    CALCULATE GUARANTEE COST
                  </span>
                )}
              </div>
            )}

            {/* NIHB + mixing */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
              <div style={{ padding: "14px 16px", background: "#0F1E33", border: "1px solid rgba(126,200,240,.28)", borderRadius: 11 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 7, marginBottom: 8 }}>
                  <span style={{ color: "#7EC8F0", fontWeight: 800 }}>✚</span>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 15, color: "#7EC8F0" }}>NIHB voucher</div>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12, lineHeight: 1.55, color: "#9fb2c8" }}>
                  A prior-approved voucher = confirmed demand. Suppresses the minimum,{" "}
                  <strong style={{ color: "#38d3a6" }}>$0 to patient</strong>, authorized escorts free.
                </div>
              </div>
              <div style={{ padding: "14px 16px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 11 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 7, marginBottom: 8 }}>
                  <span style={{ color: "#38d3a6", fontWeight: 800 }}>✓</span>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 15, color: "#E8EEF5" }}>Mixing rule</div>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12, lineHeight: 1.55, color: "#9fb2c8" }}>
                  Community passengers blocked while mine crew aboard. Empty legs surfaced as available.
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
