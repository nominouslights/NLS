"use client";

import { useState } from "react";
import { fonts, rowSurface, statusMeta } from "@/lib/theme";
import { drivers } from "@/lib/data";
import { initials } from "@/lib/format";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

const TABS = ["Profile", "Licence & certs", "Hours of Service", "Clearances", "History"];

export default function Drivers({
  driverSel,
  setDriverSel,
}: {
  driverSel: number;
  setDriverSel: (i: number) => void;
}) {
  const [tab, setTab] = useState(0);
  const d = drivers[driverSel];
  const hos = statusMeta(d.hk);
  const hosPct = d.hk === "soon" ? 33 : d.hk === "off" ? 0 : 72;
  const clrList = d.clr.length ? d.clr.join("   ") : "No client clearances on file";

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Operations · Roster & compliance engine source" title="Drivers & Compliance" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "40% 1fr", borderTop: "1px solid #1E3350" }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: "1px solid #1E3350" }}>
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "34px 1fr 96px 110px",
              gap: 11,
              padding: "0 13px 9px",
              fontFamily: fonts.semiCondensed,
              fontSize: 9.5,
              letterSpacing: ".12em",
              textTransform: "uppercase",
              color: "#4d688a",
            }}
          >
            <div />
            <div>Driver</div>
            <div>HOS left</div>
            <div>Duty</div>
          </div>
          {drivers.map((row, i) => {
            const active = i === driverSel;
            const rowHos = statusMeta(row.hk);
            const deactivated = row.duty === "Deactivated";
            return (
              <div
                key={row.id}
                onClick={() => setDriverSel(i)}
                style={{
                  display: "grid",
                  gridTemplateColumns: "34px 1fr 96px 110px",
                  gap: 11,
                  alignItems: "center",
                  padding: "10px 13px",
                  marginBottom: 5,
                  opacity: deactivated ? 0.62 : 1,
                  ...rowSurface(active, "#3B8DD4"),
                }}
              >
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 9,
                    background: "linear-gradient(135deg,#16283F,#24405f)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 12,
                    color: "#7EC8F0",
                  }}
                >
                  {initials(row.name)}
                </div>
                <div style={{ minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 13,
                      fontWeight: 600,
                      color: "#E8EEF5",
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
                      fontSize: 10,
                      letterSpacing: ".06em",
                      textTransform: "uppercase",
                      color: row.src === "Miller the Mover" ? "#E8A020" : "#6B8099",
                    }}
                  >
                    {row.src}
                  </div>
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12, fontWeight: 500, color: rowHos.t }}>{row.hos}</div>
                <div>
                  <StatusChip kind={row.dk} label={row.duty} />
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: "#0C1A2C" }}>
          <div className="detailfade" key={d.name}>
            <div style={{ display: "flex", alignItems: "center", gap: 15, marginBottom: 18 }}>
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: 13,
                  background: "linear-gradient(135deg,#16283F,#24405f)",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 20,
                  color: "#7EC8F0",
                }}
              >
                {initials(d.name)}
              </div>
              <div>
                <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1, color: "#F2F6FB", margin: "0 0 5px" }}>
                  {d.name}
                </h2>
                <div style={{ display: "flex", alignItems: "center", gap: 10, fontFamily: fonts.body, fontSize: 12.5, color: "#9fb2c8" }}>
                  <span>{d.src}</span>
                  <span style={{ color: "#3B5573" }}>·</span>
                  <span style={{ fontFamily: fonts.mono, fontSize: 11.5 }}>{d.phone}</span>
                  <span style={{ color: "#3B5573" }}>·</span>
                  <span>{d.trips} trips</span>
                </div>
              </div>
              {d.duty === "Deactivated" && (
                <span
                  style={{
                    marginLeft: "auto",
                    fontFamily: fonts.body,
                    fontWeight: 700,
                    fontSize: 12,
                    padding: "6px 13px",
                    borderRadius: 8,
                    background: "rgba(213,94,0,.14)",
                    border: "1px solid rgba(213,94,0,.4)",
                    color: "#f0803f",
                  }}
                >
                  ▲ DEACTIVATED · excluded from eligibility
                </span>
              )}
            </div>

            {/* tab bar */}
            <div style={{ display: "flex", gap: 2, borderBottom: "1px solid #1E3350", marginBottom: 16 }}>
              {TABS.map((label, i) => (
                <span
                  key={label}
                  onClick={() => setTab(i)}
                  style={{
                    fontFamily: fonts.body,
                    fontWeight: tab === i ? 600 : 500,
                    fontSize: 12.5,
                    padding: "9px 14px",
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

            {/* HOS gauge */}
            <Panel style={{ marginBottom: 12, padding: "16px 18px", borderRadius: 11 }}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 10 }}>
                <div
                  style={{
                    fontFamily: fonts.semiCondensed,
                    fontSize: 9.5,
                    letterSpacing: ".14em",
                    textTransform: "uppercase",
                    color: "#8fa6c0",
                  }}
                >
                  Hours of Service · CVDHS cycle
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 13, fontWeight: 500, color: hos.t }}>{d.hos} remaining</div>
              </div>
              <div style={{ height: 10, borderRadius: 6, background: "#0A1729", overflow: "hidden", border: "1px solid #152941" }}>
                <div style={{ height: "100%", width: `${hosPct}%`, background: hos.c, borderRadius: 6 }} />
              </div>
              <div style={{ display: "flex", justifyContent: "space-between", fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginTop: 7 }}>
                <span>13h drive · 14h on-duty · 10h off-duty</span>
                <span>220 km corridor — no short-haul exemption</span>
              </div>
            </Panel>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
              <Panel>
                <SectionLabel>Licence &amp; medical</SectionLabel>
                <div style={{ marginBottom: 9 }}>
                  <StatusChip kind={d.lk} label={d.lic} />
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8", lineHeight: 1.6 }}>
                  Manitoba Class 4 · medical current
                  <br />
                  MPI abstract on file
                </div>
              </Panel>
              <Panel>
                <SectionLabel>Client clearances</SectionLabel>
                <div style={{ fontFamily: fonts.body, fontSize: 13, color: "#38d3a6", fontWeight: 600, lineHeight: 1.7 }}>{clrList}</div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginTop: 6 }}>Ref: LL-ContractorClearances</div>
              </Panel>
            </div>

            <div style={{ display: "flex", gap: 9, marginTop: 16 }}>
              <ActionButton>MESSAGE</ActionButton>
              <ActionButton variant="destructive">DEACTIVATE DRIVER</ActionButton>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
