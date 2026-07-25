"use client";

import { colors, fonts, rowSurface } from "@/lib/theme";
import { riders } from "@/lib/data";
import { PageHeader, Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";

export default function Riders({ riderSel, setRiderSel }: { riderSel: number; setRiderSel: (i: number) => void }) {
  const r = riders[riderSel];

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Business · Community passengers & NIHB patients" title="Riders" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "38% 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {riders.map((row, i) => {
            const active = i === riderSel;
            return (
              <div
                key={row.id}
                onClick={() => setRiderSel(i)}
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 84px",
                  gap: 11,
                  alignItems: "center",
                  padding: "11px 13px",
                  marginBottom: 5,
                  ...rowSurface(active, colors.skyBlue),
                }}
              >
                <div style={{ minWidth: 0 }}>
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
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                    {row.home} · last {row.last}
                  </div>
                </div>
                <span
                  style={{
                    fontFamily: fonts.body,
                    fontWeight: 600,
                    fontSize: 10.5,
                    padding: "2px 8px",
                    borderRadius: 5,
                    background: row.prog === "NIHB" ? "rgba(23,119,158,.12)" : "rgba(31,111,178,.12)",
                    color: row.prog === "NIHB" ? colors.skyBlue : colors.blue,
                    border: `1px solid ${row.prog === "NIHB" ? "rgba(23,119,158,.35)" : "rgba(31,111,178,.35)"}`,
                  }}
                >
                  {row.prog}
                </span>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={r.name}>
            <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 28, lineHeight: 1, color: colors.headingBright, margin: "0 0 4px" }}>
              {r.name}
            </h2>
            <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 16 }}>
              {r.phone} · {r.home} · {r.pc}
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
              <Panel>
                <SectionLabel>NIHB status</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  <DetailRow label="Voucher" value={r.voucher} />
                  <DetailRow label="Escorts" value={r.escort} />
                </div>
              </Panel>
              <Panel>
                <SectionLabel>Travel</SectionLabel>
                <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  <DetailRow label="Accessibility" value={r.needs} />
                  <DetailRow label="No-shows" value={r.noshow} />
                </div>
              </Panel>
            </div>
            <div
              style={{
                padding: "12px 15px",
                background: "rgba(31,111,178,.07)",
                border: "1px solid rgba(31,111,178,.25)",
                borderRadius: 10,
                marginBottom: 16,
                fontFamily: fonts.body,
                fontSize: 12,
                lineHeight: 1.55,
                color: colors.textMuted,
              }}
            >
              Privacy-minded (PIPEDA) — only fields needed to route and bill travel are stored. Sensitive medical detail is not held
              here.
            </div>
            <ActionButton variant="primary">BOOK FOR THIS RIDER</ActionButton>
          </div>
        </div>
      </div>
    </div>
  );
}
