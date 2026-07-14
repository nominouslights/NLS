"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
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
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "220px 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 14px", borderRight: `1px solid ${colors.border}`, display: "flex", flexDirection: "column", gap: 3 }}>
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
                background: tab === i ? colors.cardBgActive : undefined,
                color: tab === i ? colors.headingBright : colors.textMuted,
                boxShadow: tab === i ? `inset 3px 0 0 ${colors.blue}` : undefined,
                cursor: "pointer",
              }}
            >
              {label}
            </span>
          ))}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {tab === 0 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: colors.headingBright, margin: "0 0 14px" }}>Organization</h2>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>Company profile</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 13.5, color: colors.textPrimary, fontWeight: 500 }}>Northern Link Shuttle &amp; Cargo</div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textMuted, marginTop: 6 }}>GST/HST: 80412 3456 RT0001</div>
                </Panel>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>Data residency</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 13.5, color: colors.textPrimary, fontWeight: 500 }}>OVHcloud · Beauharnois QC</div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: statusMeta("ontime").t, marginTop: 6 }}>✓ Canadian-hosted · PIPEDA-aligned</div>
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
                        background: colors.inputBg,
                        border: `1px solid ${colors.border}`,
                        color: colors.textSecondary,
                      }}
                    >
                      {c}
                    </span>
                  ))}
                </div>
              </Panel>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>CRA prescribed vehicle allowance (2026)</div>
                  <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, color: colors.textPrimary, fontVariantNumeric: "tabular-nums" }}>
                    $0.73 / km
                  </div>
                </Panel>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>QBO connector</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 4 }}>
                    <span style={{ width: 9, height: 9, borderRadius: "50%", background: "#009E73" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13.5, color: colors.textPrimary, fontWeight: 500 }}>Connected · read-only</span>
                  </div>
                </Panel>
              </div>
            </div>
          )}

          {tab === 1 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: colors.headingBright, margin: "0 0 14px" }}>Users &amp; Roles</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {users.map((u) => (
                  <div
                    key={u.name}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 12,
                      padding: "12px 15px",
                      background: colors.cardBg,
                      border: `1px solid ${colors.border}`,
                      borderRadius: 10,
                      boxShadow: colors.shadowCard,
                    }}
                  >
                    <div style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: colors.textPrimary, flex: 1 }}>{u.name}</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>{u.role}</div>
                    <div
                      style={{
                        fontFamily: fonts.semiCondensed,
                        fontSize: 10,
                        letterSpacing: ".08em",
                        textTransform: "uppercase",
                        color: colors.textDim,
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
                  background: "rgba(31,111,178,.07)",
                  border: "1px solid rgba(31,111,178,.25)",
                  borderRadius: 10,
                  fontFamily: fonts.body,
                  fontSize: 12,
                  lineHeight: 1.55,
                  color: colors.textMuted,
                }}
              >
                Managed via the self-hosted OIDC identity provider (OpenIddict). A Bookkeeper role hides all dispatch modules entirely
                rather than showing greyed-out controls.
              </div>
            </div>
          )}

          {tab === 2 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: colors.headingBright, margin: "0 0 14px" }}>Budget Codes</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {budgetCodes.map((b) => (
                  <div
                    key={b.code}
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 12,
                      padding: "12px 15px",
                      background: colors.cardBg,
                      border: `1px solid ${colors.border}`,
                      borderRadius: 10,
                      boxShadow: colors.shadowCard,
                    }}
                  >
                    <div style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.skyBlue, width: 130 }}>{b.code}</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>{b.desc}</div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {tab === 3 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: colors.headingBright, margin: "0 0 14px" }}>Rate Schedules</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {rateSchedules.map((r) => (
                  <div key={r.name} style={{ padding: "12px 15px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 10, boxShadow: colors.shadowCard }}>
                    <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>{r.name}</div>
                    <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textMuted, marginTop: 3 }}>{r.note}</div>
                  </div>
                ))}
              </div>
              <div
                style={{
                  marginTop: 14,
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
                Rate-schedule edits are versioned so historical invoices retain their original basis.
              </div>
            </div>
          )}

          {tab === 4 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: colors.headingBright, margin: "0 0 14px" }}>Connectors</h2>
              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>QuickBooks Online</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 4 }}>
                    <span style={{ width: 9, height: 9, borderRadius: "50%", background: "#009E73" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13.5, color: colors.textPrimary, fontWeight: 500 }}>Connected · read-only book of record</span>
                  </div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim, marginTop: 8 }}>Last read 4m ago · no write path</div>
                </Panel>
                <Panel style={{ borderRadius: 11 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>Notification provider</div>
                  <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 4 }}>
                    <span style={{ width: 9, height: 9, borderRadius: "50%", background: "#009E73" }} />
                    <span style={{ fontFamily: fonts.body, fontSize: 13.5, color: colors.textPrimary, fontWeight: 500 }}>SMS + email · connected</span>
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: statusMeta("ontime").t, marginTop: 8 }}>Canadian-hosted · PIPEDA-aligned</div>
                </Panel>
              </div>
            </div>
          )}

          {tab === 5 && (
            <div className="detailfade">
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 23, color: colors.headingBright, margin: "0 0 14px" }}>Audit Log</h2>
              <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
                {auditLog.map((a, i) => (
                  <div
                    key={i}
                    style={{
                      display: "flex",
                      gap: 11,
                      alignItems: "flex-start",
                      paddingBottom: i < auditLog.length - 1 ? 11 : 0,
                      borderLeft: i < auditLog.length - 1 ? `1.5px solid ${colors.border}` : "1.5px solid transparent",
                      marginLeft: 5,
                      paddingLeft: 14,
                      position: "relative",
                    }}
                  >
                    <span style={{ position: "absolute", left: -5, top: 1, width: 9, height: 9, borderRadius: "50%", background: colors.blue }} />
                    <div style={{ flex: 1, display: "flex", justifyContent: "space-between" }}>
                      <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>
                        <strong style={{ color: colors.textPrimary }}>{a.who}</strong> · {a.what}
                      </span>
                      <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{a.when}</span>
                    </div>
                  </div>
                ))}
              </div>
              <div
                style={{
                  marginTop: 14,
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
                Who changed what, when — PIPEDA-aligned accountability.
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
