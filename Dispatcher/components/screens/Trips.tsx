"use client";

import { useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta, svcMeta } from "@/lib/theme";
import { trips } from "@/lib/data";
import { listTripManifests, type TripManifest } from "@/lib/api";
import { printTripManifest } from "@/lib/documents/tripManifestPdf";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { CorridorStepper } from "@/components/ui/CorridorStepper";
import { Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";

const filterChips = [
  { label: "All trips", active: true },
  { label: "Open only", active: false },
  { label: "Assigned", active: false },
];
const presetChips = [
  { label: "★ This week's Alamos runs", bg: "rgba(232,160,32,.10)", bd: "rgba(232,160,32,.30)", tx: colors.amberText },
  { label: "★ Unassigned Open trips", bg: statusMeta("soon").bg, bd: statusMeta("soon").bd, tx: statusMeta("soon").t },
];

const timelineEvents = [
  { label: "Created", time: "Jul 6 · 14:22", state: "done" as const },
  { label: "Assigned to D. Chartrand", time: "Jul 6 · 14:40", state: "done" as const },
  { label: "Trip started · Thompson", time: "Jul 7 · 06:31", state: "active" as const },
  { label: "Stop 2 · Leaf Rapids — pending", time: "ETA 08:05", state: "pending" as const },
];

export default function Trips({
  tripSel,
  setTripSel,
  onNewTrip,
}: {
  tripSel: number;
  setTripSel: (i: number) => void;
  onNewTrip: () => void;
}) {
  const [filter, setFilter] = useState(0);
  const [manifests, setManifests] = useState<TripManifest[]>([]);
  const t = trips[tripSel];

  useEffect(() => {
    // Backend trip manifests (Trips API). The screen is mock-driven otherwise,
    // so an unreachable API just means no PRINT TRIP MANIFEST buttons.
    let active = true;
    listTripManifests().then(
      (rows) => {
        if (active) setManifests(rows);
      },
      (e) => {
        console.error("Trip manifests unavailable:", e);
      },
    );
    return () => {
      active = false;
    };
  }, []);

  // A completed NL-TM-01 exists for this trip when its trip number matches.
  const manifest = manifests.find((m) => m.tripNumber === t.id) ?? null;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <div style={{ display: "flex", alignItems: "flex-end", justifyContent: "space-between", marginBottom: 14 }}>
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
              Operations · Bookings &amp; Reservations
            </div>
            <h1 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 30, lineHeight: 1, color: colors.headingBright, margin: 0 }}>
              Trips
            </h1>
          </div>
          <div
            onClick={onNewTrip}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 7,
              padding: "8px 15px",
              borderRadius: 8,
              background: colors.blue,
              color: "#FFFFFF",
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 13.5,
              letterSpacing: ".04em",
              cursor: "pointer",
            }}
          >
            <span style={{ fontSize: 15, lineHeight: 1 }}>+</span> NEW TRIP
          </div>
        </div>
        <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
          {filterChips.map((f, i) => (
            <span
              key={f.label}
              onClick={() => setFilter(i)}
              style={{
                fontFamily: fonts.body,
                fontWeight: filter === i ? 600 : 500,
                fontSize: 12,
                padding: "5px 12px",
                borderRadius: 7,
                background: filter === i ? colors.cardBgActive : colors.cardBg,
                border: `1px solid ${filter === i ? colors.borderActive : colors.border}`,
                color: filter === i ? colors.headingBright : colors.textMuted,
                cursor: "pointer",
              }}
            >
              {f.label}
            </span>
          ))}
          <span style={{ width: 1, height: 20, background: colors.border, margin: "0 3px" }} />
          {presetChips.map((p) => (
            <span
              key={p.label}
              style={{
                fontFamily: fonts.body,
                fontWeight: 500,
                fontSize: 12,
                padding: "5px 12px",
                borderRadius: 7,
                background: p.bg,
                border: `1px solid ${p.bd}`,
                color: p.tx,
                cursor: "pointer",
              }}
            >
              {p.label}
            </span>
          ))}
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "42% 1fr", gap: 0, borderTop: `1px solid ${colors.border}` }}>
        {/* MASTER */}
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
            {trips.length} trips · Tue Jul 7
          </div>
          {trips.map((row, i) => {
            const rsc = svcMeta(row.svc);
            const active = i === tripSel;
            return (
              <div
                key={row.id}
                onClick={() => setTripSel(i)}
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: 7,
                  padding: "12px 14px",
                  marginBottom: 5,
                  ...rowSurface(active, rsc.accent),
                }}
              >
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
                  <ServiceChip svc={row.svc} />
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{row.id}</span>
                </div>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 10 }}>
                  <div style={{ minWidth: 0 }}>
                    <div
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 13,
                        fontWeight: 600,
                        color: colors.textPrimary,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {row.stops.join("  →  ")}
                    </div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim, marginTop: 2 }}>
                      {row.win} · {row.km} km
                    </div>
                  </div>
                  <StatusChip kind={row.sk} label={row.status} />
                </div>
                <div
                  style={{
                    fontFamily: fonts.body,
                    fontSize: 12.5,
                    fontWeight: row.open ? 600 : 500,
                    color: row.open ? colors.amberText : colors.textSecondary,
                  }}
                >
                  {row.driver ?? "OPEN — needs coverage"} · <span style={{ color: colors.textDim, fontWeight: 400 }}>{row.vehicle}</span>
                </div>
              </div>
            );
          })}
        </div>

        {/* DETAIL */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={t.id}>
            {/* header */}
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 6 }}>
              <ServiceChip svc={t.svc} />
              <StatusChip kind={t.sk} label={t.status} />
              <span style={{ marginLeft: "auto", fontFamily: fonts.mono, fontSize: 13, color: colors.skyBlue }}>{t.id}</span>
            </div>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1.05, color: colors.headingBright, margin: "6px 0 4px" }}>
              {t.stops.join("  →  ")}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.textMuted, marginBottom: 16 }}>
              {t.win} · {t.km} km · {t.client}
            </div>

            <CorridorStepper stops={t.stops} />

            {/* two column blocks */}
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
              <Panel>
                <SectionLabel>Assignment</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow
                    label="Driver"
                    value={t.driver ?? "OPEN — needs coverage"}
                    valueStyle={{ color: t.open ? colors.amberText : colors.textSecondary, fontWeight: t.open ? 600 : 500 }}
                  />
                  <DetailRow label="Vehicle" value={t.vehicle} />
                  <DetailRow label="HOS remaining" value="4h 20m" valueStyle={{ fontFamily: fonts.mono, color: statusMeta("ontime").t }} />
                  <DetailRow label="Clearance" value="Alamos ✓ · licence ✓" valueStyle={{ color: statusMeta("ontime").t }} />
                </div>
              </Panel>
              <Panel>
                <SectionLabel>Manifest</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="Capacity" value={t.cap} valueStyle={{ fontFamily: fonts.mono }} />
                  <DetailRow label="Mixing rule" value="✓ Compliant" valueStyle={{ color: statusMeta("ontime").t }} />
                  <DetailRow label="Demand" value="Confirmed" />
                  <DetailRow label="Escorts" value="0" />
                </div>
              </Panel>
            </div>

            {/* billing */}
            <Panel style={{ marginBottom: 12 }}>
              <SectionLabel>Billing</SectionLabel>
              <div style={{ display: "grid", gridTemplateColumns: "repeat(4,1fr)", gap: 12 }}>
                <div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Rate basis</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, fontWeight: 500 }}>Contract</div>
                </div>
                <div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>PO</div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>{t.po}</div>
                </div>
                <div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Budget code</div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>ZBB-CREW-01</div>
                </div>
                <div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 2 }}>Invoice</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: statusMeta("soon").t, fontWeight: 500 }}>Not yet drafted</div>
                </div>
              </div>
            </Panel>

            {/* timeline */}
            <Panel style={{ marginBottom: 16 }}>
              <SectionLabel>Timeline &amp; audit</SectionLabel>
              <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
                {timelineEvents.map((ev, i) => (
                  <div
                    key={ev.label}
                    style={{
                      display: "flex",
                      gap: 11,
                      alignItems: "flex-start",
                      paddingBottom: i < timelineEvents.length - 1 ? 11 : 0,
                      borderLeft: i < timelineEvents.length - 1 ? `1.5px solid ${colors.border}` : "1.5px solid transparent",
                      marginLeft: 5,
                      paddingLeft: 14,
                      position: "relative",
                    }}
                  >
                    <span
                      style={{
                        position: "absolute",
                        left: -5,
                        top: 1,
                        width: 9,
                        height: 9,
                        borderRadius: "50%",
                        background: ev.state === "pending" ? colors.border : ev.state === "active" ? colors.blue : statusMeta("ontime").c,
                        border: ev.state === "pending" ? `1.5px solid ${colors.textFaint}` : undefined,
                      }}
                    />
                    <div style={{ flex: 1, display: "flex", justifyContent: "space-between" }}>
                      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: ev.state === "pending" ? colors.textDim : colors.textSecondary }}>
                        {ev.label}
                      </span>
                      <span style={{ fontFamily: fonts.mono, fontSize: 11, color: ev.state === "pending" ? colors.textFaint : colors.textDim }}>
                        {ev.time}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            </Panel>

            {/* actions */}
            <div style={{ display: "flex", flexWrap: "wrap", gap: 9 }}>
              <ActionButton variant="primary">EDIT TRIP</ActionButton>
              <ActionButton>REASSIGN</ActionButton>
              <ActionButton>GENERATE MANIFEST</ActionButton>
              {manifest && (
                <ActionButton onClick={() => printTripManifest(manifest)}>
                  PRINT TRIP MANIFEST
                </ActionButton>
              )}
              <ActionButton>MESSAGE DRIVER</ActionButton>
              <ActionButton variant="destructive">CANCEL</ActionButton>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
