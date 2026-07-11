"use client";

import { useState } from "react";
import { fonts } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";

const groceryOrders = [
  {
    id: "GR-0912",
    where: "Leaf Rapids · town office freezer",
    items: "14 items · chilled",
    batch: "Fri · TR-4824",
    status: "Batched",
    kind: "ontime" as const,
  },
  {
    id: "GR-0913",
    where: "Black Sturgeon Falls · band office",
    surcharge: true,
    items: "9 items · dry",
    batch: "Fri · pending",
    status: "Awaiting batch",
    kind: "soon" as const,
  },
  {
    id: "GR-0914",
    where: "Lynn Lake · community hall",
    items: "6 items · dry",
    batch: "Next week",
    status: "After cutoff",
    kind: "off" as const,
    faded: true,
  },
];

const parcelOrders = [
  {
    id: "PC-1140",
    where: "Leaf Rapids · community agent",
    items: "1 tote · 6.2 kg · Tier B",
    batch: "Fri · TR-4824",
    status: "Batched",
    kind: "ontime" as const,
  },
  {
    id: "PC-1141",
    where: "Lynn Lake · community agent",
    items: "3 boxes · 18 kg · Tier C",
    batch: "Fri · empty-leg fill",
    status: "Awaiting pickup",
    kind: "soon" as const,
  },
  {
    id: "PC-1142",
    where: "South Indian Lake · patient courier",
    items: "1 envelope · 0.3 kg · Tier A",
    batch: "Fri · TR-4822",
    status: "In transit",
    kind: "info" as const,
  },
];

const statusColor: Record<string, string> = {
  ontime: "#38d3a6",
  soon: "#ecc94b",
  off: "#9fb2c8",
  info: "#7EC8F0",
};
const statusBg: Record<string, string> = {
  ontime: "#14B88A",
  soon: "#E1B000",
  off: "transparent",
  info: "#3B8DD4",
};
const statusGlyph: Record<string, string> = { ontime: "✓", soon: "◐", off: "—", info: "●" };

export default function Grocery() {
  const [tab, setTab] = useState<0 | 1>(0);
  const orders = tab === 0 ? groceryOrders : parcelOrders;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 6px" }}>
        <PageHeader eyebrow="Operations · Diversified-revenue logistics on spare capacity" title="Grocery & Parcel" />
        <div style={{ display: "flex", gap: 2, borderBottom: "1px solid #1E3350", marginTop: 12 }}>
          {["Grocery Run", "Parcel / Cargo"].map((label, i) => (
            <span
              key={label}
              onClick={() => setTab(i as 0 | 1)}
              style={{
                fontFamily: fonts.body,
                fontWeight: tab === i ? 600 : 500,
                fontSize: 13,
                padding: "9px 16px",
                color: tab === i ? "#F2F6FB" : "#6B8099",
                borderBottom: tab === i ? "2px solid #3B8DD4" : undefined,
                marginBottom: -1,
                cursor: "pointer",
              }}
            >
              {label}
            </span>
          ))}
        </div>
      </div>
      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "18px 26px" }}>
        <div style={{ display: "flex", gap: 12, marginBottom: 16 }}>
          <div style={{ flex: 1, padding: "14px 16px", borderRadius: 11, background: "rgba(225,176,0,.09)", border: "1px solid rgba(225,176,0,.3)" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
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
              <span style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, color: "#ecc94b" }}>Order window — accepting</span>
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#9fb2c8" }}>
              Wednesday noon cutoff · <strong style={{ color: "#E8EEF5" }}>1d 4h remaining</strong>. Orders after cutoff roll to next
              week&rsquo;s Friday delivery.
            </div>
          </div>
          <div style={{ flex: 1, padding: "14px 16px", borderRadius: 11, background: "rgba(59,141,212,.08)", border: "1px solid rgba(59,141,212,.3)" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 6 }}>
              <span
                style={{
                  width: 16,
                  height: 16,
                  borderRadius: 4,
                  background: "#3B8DD4",
                  color: "#04121f",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: 10,
                  fontWeight: 800,
                }}
              >
                ◗
              </span>
              <span style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, color: "#7EC8F0" }}>Friday delivery run</span>
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#9fb2c8" }}>
              Batched onto existing shuttle capacity · empty-leg fill suggested for TR-4824.
            </div>
          </div>
        </div>

        <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 16, letterSpacing: ".06em", color: "#c2d0e0", marginBottom: 11 }}>
          {tab === 0 ? "THIS WEEK’S ORDERS" : "THIS WEEK’S SHIPMENTS"}
        </div>
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "120px 1fr 150px 130px 120px",
            gap: 12,
            padding: "0 14px 8px",
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".12em",
            textTransform: "uppercase",
            color: "#4d688a",
          }}
        >
          <div>{tab === 0 ? "Order" : "Parcel"}</div>
          <div>Community · hub</div>
          <div>Items</div>
          <div>Batch</div>
          <div>Status</div>
        </div>
        {orders.map((o) => (
          <div
            key={o.id}
            style={{
              display: "grid",
              gridTemplateColumns: "120px 1fr 150px 130px 120px",
              gap: 12,
              alignItems: "center",
              padding: "12px 14px",
              marginBottom: 5,
              borderRadius: 9,
              background: "#0F1E33",
              border: "1px solid #152941",
              opacity: "faded" in o && o.faded ? 0.7 : 1,
            }}
          >
            <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: "#7EC8F0" }}>{o.id}</span>
            <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#E8EEF5" }}>
              {o.where}
              {"surcharge" in o && o.surcharge && <span style={{ color: "#E8A020", fontSize: 11 }}> + surcharge</span>}
            </span>
            <span style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8" }}>{o.items}</span>
            <span style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8" }}>{o.batch}</span>
            <span
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 5,
                fontFamily: fonts.body,
                fontWeight: 600,
                fontSize: 11,
                color: statusColor[o.kind],
              }}
            >
              <span
                style={{
                  width: 14,
                  height: 14,
                  borderRadius: 4,
                  background: o.kind === "off" ? "transparent" : statusBg[o.kind],
                  border: o.kind === "off" ? "1.5px solid #7A8899" : undefined,
                  color: o.kind === "off" ? "#7A8899" : o.kind === "soon" ? "#2e2400" : o.kind === "ontime" ? "#04231a" : "#04121f",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontSize: 9,
                  fontWeight: 800,
                }}
              >
                {statusGlyph[o.kind]}
              </span>
              {o.status}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
