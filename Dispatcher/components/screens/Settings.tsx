"use client";

import { useState } from "react";
import { fonts } from "@/lib/theme";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";

const TABS = ["Organization", "Users & Roles", "Budget Codes", "Rate Schedules", "Connectors", "Audit Log"];

const users = [
  { name: "R. Kelsey", role: "Owner · Dispatcher", tenant: "Internal", active: true },
  { name: "S. Okimaw", role: "Dispatcher", tenant: "Internal", active: true },
  { name: "L. Fontaine", role: "Bookkeeper", tenant: "Internal", active: true },
];

const budgetCodes = [
  { code: "ZBB-CREW-01", desc: "Alamos crew shuttle" },
  { code: "ZBB-NIHB-01", desc: "NIHB medical transport" },
  { code: "ZBB-CHTR-02", desc: "Charter runs" },
  { code: "ZBB-COMM-01", desc: "Community fare runs" },
];

const rateSchedules = [
  { name: "Alamos contract · crew shuttle", note: "Per-km contract rate, PO-matched" },
  { name: "NIHB Tier 1 / 2 / 3", note: "Direct-billed, patient $0" },
  { name: "Community fare", note: "$67.00 / seat · demand-activated" },
  { name: "CRA prescribed rate (2026)", note: "$0.73 / km cost basis" },
];

const auditLog = [
  { who: "R. Kelsey", what: "Deactivated driver A. Nepinak", when: "Jul 6 · 15:02" },
  { who: "R. Kelsey", what: "Set U-01 out of service", when: "Jul 6 · 14:55" },
  { who: "S. Okimaw", what: "Edited rate schedule · Community fare", when: "Jul 4 · 10:20" },
];

export default function Settings() {
  const [tab, setTab] = useState(0);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Configure the operation without touching code" title="Settings & Administration" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "220px 1fr", borderTop: "1px solid #1E3350" }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 14px", borderRight: "1px solid #1E3350", display: "flex", flexDirection: "column", gap: 3 }}>
          {TABS.map((label, i) => (
            <span
              key={label}
              onClick={() => setTab(i)}
              style={{
                fontFamily: fonts.body,
                fontWeight: tab === i ? 600 : 500,
                fontSize: 13,
                padding: "9px 14px",
                borderRadius: 8,
                background: tab === i ? "#16283F" : undefined,
                color: tab === i ? "#F2F6FB" : "#9fb2c8",
                boxShadow: tab === i ? "inset 3px 0 0 #3B8DD4" : undefined,
                cursor: "pointer",
              }}
            >
              {label}
            </span>
          ))}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: "#0C1A2C" }}>
          {tab === 0 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: "#F2F6FB", margin: "0 0 14px" }}>Organization</h2>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 5 }}>Company profile</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 13.5, color: "#E8EEF5", fontWeight: 500 }}>Northern Link Shuttle &amp; Cargo</div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: "#9fb2c8", marginTop: 6 }}>GST/HST: 80412 3456 RT0001</div>
                </Panel>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 5 }}>Data residency</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 13.5, color: "#E8EEF5", fontWeight: 500 }}>OVHcloud · Beauharnois QC</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#38d3a6", marginTop: 6 }}>✓ Canadian-hosted · PIPEDA-aligned</div>
                </Panel>
              </div>
              <Panel style={{ marginBottom: 12, borderRadius: 11 }}>
                <SectionLabel>Corridor communities</SectionLabel>
                <div style={{ display: "flex", flexWrap: "wrap", gap: 8 }}>
                  {["Thompson · hub", "Leaf Rapids", "Lynn Lake", "South Indian Lake", "Black Sturgeon Falls"].map((c) => (
                    <span
                      key={c}
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12,
                        padding: "5px 12px",
                        borderRadius: 7,
                        background: "#0A1729",
                        border: "1px solid #1E3350",
                        color: "#c2d0e0",
                      }}
                    >
                      {c}
                    </span>
                  ))}
                </div>
              </Panel>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 5 }}>CRA prescribed vehicle allowance (2026)</div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: "#E8EEF5", fontVariantNumeric: "tabular-nums" }}>
                    $0.73 / km
                  </div>
                </Panel>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 5 }}>QBO connector</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 4 }}>
                    <span style={{ width: 9, height: 9, borderRadius: "50%", background: "#14B88A" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13.5, color: "#E8EEF5", fontWeight: 500 }}>Connected · read-only</span>
                  </div>
                </Panel>
              </div>
            </div>
          )}

          {tab === 1 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: "#F2F6FB", margin: "0 0 14px" }}>Users &amp; Roles</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {users.map((u) => (
                  <div
                    key={u.name}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 12,
                      padding: "12px 15px",
                      background: "#0F1E33",
                      border: "1px solid #1E3350",
                      borderRadius: 10,
                    }}
                  >
                    <div style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: "#E8EEF5", flex: 1 }}>{u.name}</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12, color: "#9fb2c8" }}>{u.role}</div>
                    <div
                      style={{
                        fontFamily: fonts.semiCondensed,
                        fontSize: 10,
                        letterSpacing: ".08em",
                        textTransform: "uppercase",
                        color: "#6B8099",
                      }}
                    >
                      {u.tenant}
                    </div>
                  </div>
                ))}
              </div>
              <div
                style={{
                  marginTop: 14,
                  padding: "12px 15px",
                  background: "rgba(59,141,212,.07)",
                  border: "1px solid rgba(59,141,212,.25)",
                  borderRadius: 10,
                  fontFamily: fonts.body,
                  fontSize: 12,
                  lineHeight: 1.55,
                  color: "#9fb2c8",
                }}
              >
                Managed via the self-hosted OIDC identity provider (OpenIddict). A Bookkeeper role hides all dispatch modules entirely
                rather than showing greyed-out controls.
              </div>
            </div>
          )}

          {tab === 2 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: "#F2F6FB", margin: "0 0 14px" }}>Budget Codes</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {budgetCodes.map((b) => (
                  <div
                    key={b.code}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 12,
                      padding: "12px 15px",
                      background: "#0F1E33",
                      border: "1px solid #1E3350",
                      borderRadius: 10,
                    }}
                  >
                    <div style={{ fontFamily: fonts.mono, fontSize: 12.5, color: "#7EC8F0", width: 130 }}>{b.code}</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#c2d0e0" }}>{b.desc}</div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {tab === 3 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: "#F2F6FB", margin: "0 0 14px" }}>Rate Schedules</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {rateSchedules.map((r) => (
                  <div key={r.name} style={{ padding: "12px 15px", background: "#0F1E33", border: "1px solid #1E3350", borderRadius: 10 }}>
                    <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: "#E8EEF5" }}>{r.name}</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#9fb2c8", marginTop: 3 }}>{r.note}</div>
                  </div>
                ))}
              </div>
              <div
                style={{
                  marginTop: 14,
                  padding: "12px 15px",
                  background: "rgba(59,141,212,.07)",
                  border: "1px solid rgba(59,141,212,.25)",
                  borderRadius: 10,
                  fontFamily: fonts.body,
                  fontSize: 12,
                  lineHeight: 1.55,
                  color: "#9fb2c8",
                }}
              >
                Rate-schedule edits are versioned so historical invoices retain their original basis.
              </div>
            </div>
          )}

          {tab === 4 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: "#F2F6FB", margin: "0 0 14px" }}>Connectors</h2>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 5 }}>QuickBooks Online</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 4 }}>
                    <span style={{ width: 9, height: 9, borderRadius: "50%", background: "#14B88A" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13.5, color: "#E8EEF5", fontWeight: 500 }}>Connected · read-only book of record</span>
                  </div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: "#6B8099", marginTop: 8 }}>Last read 4m ago · no write path</div>
                </Panel>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: "#6B8099", marginBottom: 5 }}>Notification provider</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 4 }}>
                    <span style={{ width: 9, height: 9, borderRadius: "50%", background: "#14B88A" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13.5, color: "#E8EEF5", fontWeight: 500 }}>SMS + email · connected</span>
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: "#38d3a6", marginTop: 8 }}>Canadian-hosted · PIPEDA-aligned</div>
                </Panel>
              </div>
            </div>
          )}

          {tab === 5 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: "#F2F6FB", margin: "0 0 14px" }}>Audit Log</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
                {auditLog.map((a, i) => (
                  <div
                    key={i}
                    style={{
                      display: "flex",
                      gap: 11,
                      alignItems: "flex-start",
                      paddingBottom: i < auditLog.length - 1 ? 11 : 0,
                      borderLeft: i < auditLog.length - 1 ? "1.5px solid #1E3350" : "1.5px solid transparent",
                      marginLeft: 5,
                      paddingLeft: 14,
                      position: "relative",
                    }}
                  >
                    <span style={{ position: "absolute", left: -5, top: 1, width: 9, height: 9, borderRadius: "50%", background: "#3B8DD4" }} />
                    <div style={{ flex: 1, display: "flex", justifyContent: "space-between" }}>
                      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: "#c2d0e0" }}>
                        <strong style={{ color: "#E8EEF5" }}>{a.who}</strong> · {a.what}
                      </span>
                      <span style={{ fontFamily: fonts.mono, fontSize: 11, color: "#6B8099" }}>{a.when}</span>
                    </div>
                  </div>
                ))}
              </div>
              <div
                style={{
                  marginTop: 14,
                  padding: "12px 15px",
                  background: "rgba(59,141,212,.07)",
                  border: "1px solid rgba(59,141,212,.25)",
                  borderRadius: 10,
                  fontFamily: fonts.body,
                  fontSize: 12,
                  lineHeight: 1.55,
                  color: "#9fb2c8",
                }}
              >
                Who changed what, when — PIPEDA-aligned accountability.
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
