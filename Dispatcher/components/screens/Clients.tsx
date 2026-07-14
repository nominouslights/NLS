"use client";

import { colors, fonts, rowSurface, svcMeta } from "@/lib/theme";
import { clients } from "@/lib/data";
import { PageHeader, Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

export default function Clients({
  clientSel,
  setClientSel,
  onCreateTrip,
}: {
  clientSel: number;
  setClientSel: (i: number) => void;
  onCreateTrip: () => void;
}) {
  const c = clients[clientSel];
  const accent = svcMeta(c.svc).accent;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Business · Operational client records & contract health" title="Clients & Contracts" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "38% 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {clients.map((row, i) => {
            const active = i === clientSel;
            const rsc = svcMeta(row.svc);
            return (
              <div
                key={row.id}
                onClick={() => setClientSel(i)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 11,
                  padding: "12px 13px",
                  marginBottom: 5,
                  ...rowSurface(active, rsc.accent),
                }}
              >
                <span
                  style={{
                    width: 9,
                    height: 9,
                    flex: "none",
                    borderRadius: row.svc === "alamos" ? 2 : "50%",
                    background: rsc.accent,
                  }}
                />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 13.5,
                      fontWeight: 600,
                      color: colors.textPrimary,
                      whiteSpace: "nowrap",
                      overflow: "hidden",
                      textOverflow: "ellipsis",
                    }}
                  >
                    {row.name}
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontSize: 9.5,
                      letterSpacing: ".1em",
                      textTransform: "uppercase",
                      color: colors.textDim,
                    }}
                  >
                    {row.tag}
                  </div>
                </div>
                <StatusChip kind={row.rk} label={row.renew} />
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={c.name}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 6 }}>
              <span style={{ width: 14, height: 14, borderRadius: 4, background: accent }} />
              <span
                style={{
                  fontFamily: fonts.semiCondensed,
                  fontSize: 10,
                  letterSpacing: ".12em",
                  textTransform: "uppercase",
                  color: colors.textLabel,
                }}
              >
                {c.tag}
              </span>
              <span style={{ marginLeft: "auto" }}>
                <StatusChip kind={c.rk} label={c.renew} />
              </span>
            </div>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 28, lineHeight: 1, color: colors.headingBright, margin: "6px 0 16px" }}>
              {c.name}
            </h2>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
              <Panel>
                <SectionLabel>Contract summary</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="Term" value={c.term} valueStyle={{ fontFamily: fonts.mono, fontSize: 11.5 }} />
                  <DetailRow label="Rate schedule" value={c.rate} />
                </div>
              </Panel>
              <Panel>
                <SectionLabel>PO &amp; billing config</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                  <DetailRow label="PO structure" value={c.po} valueStyle={{ fontFamily: fonts.mono, fontSize: 11.5 }} />
                  <DetailRow label="Tax" value={c.gst} />
                </div>
              </Panel>
            </div>

            <Panel style={{ marginBottom: 12 }}>
              <SectionLabel>Contact map</SectionLabel>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {c.contacts.map(([name, role]) => (
                  <div
                    key={name}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 12,
                      padding: "9px 12px",
                      background: colors.inputBg,
                      border: `1px solid ${colors.borderSubtle}`,
                      borderRadius: 8,
                    }}
                  >
                    <div
                      style={{
                        width: 30,
                        height: 30,
                        borderRadius: 8,
                        background: colors.cardBgActive,
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "center",
                        fontFamily: fonts.condensed,
                        fontWeight: 700,
                        fontSize: 11,
                        color: colors.skyBlue,
                        flex: "none",
                      }}
                    >
                      ●
                    </div>
                    <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary, flex: 1 }}>{name}</div>
                    <div
                      style={{
                        fontFamily: fonts.semiCondensed,
                        fontSize: 10.5,
                        letterSpacing: ".06em",
                        textTransform: "uppercase",
                        color: colors.textLabel,
                      }}
                    >
                      {role}
                    </div>
                  </div>
                ))}
              </div>
            </Panel>

            <div
              style={{
                padding: "13px 15px",
                background: "rgba(232,160,32,.08)",
                border: "1px solid rgba(232,160,32,.28)",
                borderRadius: 10,
                marginBottom: 16,
                display: "flex",
                gap: 10,
                alignItems: "flex-start",
              }}
            >
              <span style={{ color: colors.amber, fontWeight: 800, fontSize: 13 }}>▲</span>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, lineHeight: 1.55, color: colors.textSecondary }}>{c.notes}</div>
            </div>

            <div style={{ display: "flex", gap: 9 }}>
              <ActionButton variant="amber" onClick={onCreateTrip}>
                CREATE TRIP FOR THIS CLIENT
              </ActionButton>
              <ActionButton>VIEW TRIP HISTORY</ActionButton>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
