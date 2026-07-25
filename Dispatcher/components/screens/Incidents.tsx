"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { incidents } from "@/lib/data";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

export default function Incidents({
  incidentSel,
  setIncidentSel,
}: {
  incidentSel: number;
  setIncidentSel: (i: number) => void;
}) {
  const n = incidents[incidentSel];

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Business · Field incident & fault review" title="Incidents & Faults" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "40% 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {incidents.map((row, i) => {
            const active = i === incidentSel;
            const m = statusMeta(row.sk);
            const statusColor =
              row.status === "Resolved" ? statusMeta("ontime").t : row.status === "Investigating" ? statusMeta("soon").t : colors.textMuted;
            return (
              <div
                key={row.id}
                onClick={() => setIncidentSel(i)}
                style={{ display: "flex", flexDirection: "column", gap: 6, padding: "12px 13px", marginBottom: 5, ...rowSurface(active, m.c) }}
              >
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                  <StatusChip kind={row.sk} label={row.sev} />
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{row.id}</span>
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.45 }}>{row.summary}</div>
                <div style={{ display: "flex", justifyContent: "space-between" }}>
                  <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
                    {row.date} · {row.driver} · {row.vehicle}
                  </span>
                  <span
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontSize: 10,
                      letterSpacing: ".06em",
                      textTransform: "uppercase",
                      color: statusColor,
                    }}
                  >
                    {row.status}
                  </span>
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={n.id}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 14 }}>
              <StatusChip kind={n.sk} label={n.sev} />
              <span style={{ fontFamily: fonts.mono, fontSize: 13, color: colors.skyBlue }}>{n.id}</span>
              <span
                style={{
                  marginLeft: "auto",
                  fontFamily: fonts.semiCondensed,
                  fontSize: 11,
                  letterSpacing: ".08em",
                  textTransform: "uppercase",
                  color: colors.textMuted,
                }}
              >
                {n.status}
              </span>
            </div>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, lineHeight: 1.15, color: colors.headingBright, margin: "0 0 10px" }}>
              {n.summary}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 16 }}>
              {n.date} · Driver {n.driver} · Vehicle {n.vehicle} · Trip {n.trip}
            </div>
            <Panel style={{ marginBottom: 12 }}>
              <SectionLabel>Accountability workflow</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, lineHeight: 1.6, color: colors.textMuted }}>
                Separates <strong style={{ color: colors.textPrimary }}>client-facing accountability</strong> (proactive, full disclosure) from{" "}
                <strong style={{ color: colors.textPrimary }}>internal personnel matters</strong> (kept out of external documentation). Deferred
                items tracked separately until committed.
              </div>
            </Panel>
            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton variant="destructive">SET VEHICLE OUT OF SERVICE</ActionButton>
              <ActionButton>MARK RESOLVED</ActionButton>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
