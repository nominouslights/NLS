"use client";

import { useState } from "react";
import { chipStyle, colors, dutyMeta, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { drivers } from "@/lib/data";
import { credentialsFor, expiringCredentials, hosLogsFor, worstCredentialKind } from "@/lib/driverCompliance";
import { initials } from "@/lib/format";
import type { DriverCredential, DutyStatus, HosLogEntry } from "@/lib/types";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";

const TABS = ["Profile", "Licence & certs", "Hours of Service", "Clearances", "History"];

// Duty status as color + icon + label (never colour alone).
function DutyChip({ status }: { status: DutyStatus }) {
  const m = dutyMeta(status);
  return (
    <span style={chipStyle(m.bg, m.bd, m.text)}>
      <span style={{ fontSize: 10, lineHeight: 1, color: m.color }}>{m.glyph}</span>
      {status}
    </span>
  );
}

// Small "N expiring / expired" flag for a roster row.
function ExpiryFlag({ driverId }: { driverId: number }) {
  const worst = worstCredentialKind(driverId);
  if (!worst) return null;
  const count = expiringCredentials(driverId).length;
  const m = statusMeta(worst);
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        fontFamily: fonts.body,
        fontWeight: 600,
        fontSize: 10,
        padding: "1px 6px",
        borderRadius: 6,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        color: m.t,
      }}
    >
      <span style={{ fontSize: 9, lineHeight: 1 }}>{m.g}</span>
      {count} {worst === "over" ? "expired" : "expiring"}
    </span>
  );
}

function PermitTag() {
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        fontFamily: fonts.semiCondensed,
        fontWeight: 600,
        fontSize: 9.5,
        letterSpacing: ".06em",
        textTransform: "uppercase",
        padding: "1px 6px",
        borderRadius: 6,
        background: "rgba(232,160,32,.13)",
        border: "1px solid rgba(232,160,32,.5)",
        color: colors.amberText,
      }}
    >
      Work permit
    </span>
  );
}

function credExpiryLabel(c: DriverCredential): string {
  if (!c.expiry) return "No expiry";
  if (c.k === "over") return `Expired ${c.expiry}`;
  if (c.k === "soon") return `Expires ${c.expiry}`;
  return `Valid to ${c.expiry}`;
}

function CredentialRow({ c }: { c: DriverCredential }) {
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 12,
        padding: "11px 0",
        borderTop: `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={{ minWidth: 0 }}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 7,
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".1em",
            textTransform: "uppercase",
            color: colors.textLabel,
            marginBottom: 3,
          }}
        >
          {c.type}
          {c.optional && (
            <span style={{ color: colors.textFaint, letterSpacing: ".04em" }}>· optional</span>
          )}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
          {c.label}
        </div>
        {(c.issued || c.note) && (
          <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 2 }}>
            {c.issued && <span>Issued {c.issued}</span>}
            {c.issued && c.note && <span style={{ color: colors.textFaint }}> · </span>}
            {c.note && <span>{c.note}</span>}
          </div>
        )}
      </div>
      <div style={{ flex: "none", textAlign: "right" }}>
        <StatusChip kind={c.k} label={credExpiryLabel(c)} />
      </div>
    </div>
  );
}

function SourceChip({ entry }: { entry: HosLogEntry }) {
  const manual = entry.source === "Manual (paper backup)";
  const style = manual
    ? chipStyle("rgba(232,160,32,.13)", "rgba(232,160,32,.5)", colors.amberText)
    : chipStyle(statusMeta("info").bg, statusMeta("info").bd, statusMeta("info").t);
  return (
    <span style={{ ...style, fontSize: 10.5, padding: "2px 8px" }}>
      <span style={{ fontSize: 9, lineHeight: 1 }}>{manual ? "✎" : "◈"}</span>
      {manual ? "Paper backup" : "Driver App"}
    </span>
  );
}

function HosLogRow({ entry }: { entry: HosLogEntry }) {
  const m = dutyMeta(entry.duty);
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "78px 92px 1fr auto",
        gap: 10,
        alignItems: "center",
        padding: "10px 0",
        borderTop: `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>{entry.date}</div>
      <div style={{ display: "flex", alignItems: "center", gap: 5, fontFamily: fonts.body, fontSize: 12, fontWeight: 600, color: m.text }}>
        <span style={{ fontSize: 9, color: m.color }}>{m.glyph}</span>
        {entry.duty}
      </div>
      <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>
        On {entry.onDutyH}h · Drv {entry.drivingH}h · Off {entry.offDutyH}h
      </div>
      <div style={{ textAlign: "right" }}>
        <SourceChip entry={entry} />
        {entry.enteredBy && (
          <div style={{ fontFamily: fonts.body, fontSize: 10, color: colors.textDim, marginTop: 2 }}>{entry.enteredBy}</div>
        )}
      </div>
    </div>
  );
}

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
  const creds = credentialsFor(d.id);
  const logs = hosLogsFor(d.id);
  const expiring = expiringCredentials(d.id);

  const hosGauge = (
    <Panel style={{ marginBottom: 12, padding: "16px 18px", borderRadius: 11 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 10 }}>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".14em",
            textTransform: "uppercase",
            color: colors.textLabel,
          }}
        >
          Hours of Service · CVDHS cycle
        </div>
        <div style={{ fontFamily: fonts.mono, fontSize: 13, fontWeight: 500, color: hos.t }}>{d.hos} remaining</div>
      </div>
      <div style={{ height: 10, borderRadius: 6, background: colors.inputBg, overflow: "hidden", border: `1px solid ${colors.borderSubtle}` }}>
        <div style={{ height: "100%", width: `${hosPct}%`, background: hos.c, borderRadius: 6 }} />
      </div>
      <div style={{ display: "flex", justifyContent: "space-between", fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 7 }}>
        <span>13h drive · 14h on-duty · 10h off-duty</span>
        <span>220 km corridor — no short-haul exemption</span>
      </div>
    </Panel>
  );

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow="Operations · Roster & compliance engine source" title="Drivers & Compliance" />
      </div>
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "40% 1fr", borderTop: `1px solid ${colors.border}` }}>
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
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
              color: colors.textFaint,
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
                  ...rowSurface(active, colors.blue),
                }}
              >
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 9,
                    background: `linear-gradient(135deg,${colors.cardBgActive},${colors.borderStrong})`,
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 12,
                    color: colors.skyBlue,
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
                      fontSize: 10,
                      letterSpacing: ".06em",
                      textTransform: "uppercase",
                      color: row.src === "Miller the Mover" ? colors.amberText : colors.textDim,
                    }}
                  >
                    {row.src}
                  </div>
                  {(worstCredentialKind(row.id) || row.workPermit) && (
                    <div style={{ display: "flex", flexWrap: "wrap", gap: 5, marginTop: 4 }}>
                      <ExpiryFlag driverId={row.id} />
                      {row.workPermit && <PermitTag />}
                    </div>
                  )}
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 12, fontWeight: 500, color: rowHos.t }}>{row.hos}</div>
                <div>
                  {deactivated ? <StatusChip kind="over" label="Deactivated" /> : <DutyChip status={row.dutyStatus} />}
                </div>
              </div>
            );
          })}
        </div>

        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          <div className="detailfade" key={d.name}>
            <div style={{ display: "flex", alignItems: "center", gap: 15, marginBottom: 18 }}>
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: 13,
                  background: `linear-gradient(135deg,${colors.cardBgActive},${colors.borderStrong})`,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 20,
                  color: colors.skyBlue,
                }}
              >
                {initials(d.name)}
              </div>
              <div>
                <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 26, lineHeight: 1, color: colors.headingBright, margin: "0 0 6px" }}>
                  {d.name}
                </h2>
                <div style={{ display: "flex", alignItems: "center", gap: 10, fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>
                  {d.duty === "Deactivated" ? <StatusChip kind="over" label="Deactivated" /> : <DutyChip status={d.dutyStatus} />}
                  <span>{d.src}</span>
                  <span style={{ color: colors.textFaint }}>·</span>
                  <span style={{ fontFamily: fonts.mono, fontSize: 11.5 }}>{d.phone}</span>
                  <span style={{ color: colors.textFaint }}>·</span>
                  <span>{d.trips} trips</span>
                </div>
              </div>
              {d.workPermit && (
                <span style={{ marginLeft: d.duty === "Deactivated" ? undefined : "auto" }}>
                  <PermitTag />
                </span>
              )}
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
                    color: statusMeta("over").t,
                  }}
                >
                  ▲ DEACTIVATED · excluded from eligibility
                </span>
              )}
            </div>

            {/* tab bar */}
            <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16 }}>
              {TABS.map((label, i) => (
                <span
                  key={label}
                  onClick={() => setTab(i)}
                  style={{
                    fontFamily: fonts.body,
                    fontWeight: tab === i ? 600 : 500,
                    fontSize: 12.5,
                    padding: "9px 14px",
                    color: tab === i ? colors.headingBright : colors.textDim,
                    borderBottom: tab === i ? `2px solid ${colors.blue}` : undefined,
                    marginBottom: -1,
                    cursor: "pointer",
                  }}
                >
                  {label}
                </span>
              ))}
            </div>

            {/* ---- Profile ---- */}
            {tab === 0 && (
              <>
                <Panel style={{ marginBottom: 12 }}>
                  <SectionLabel>Compliance summary</SectionLabel>
                  {expiring.length === 0 ? (
                    <StatusChip kind="ontime" label="All credentials current" />
                  ) : (
                    <StatusChip
                      kind={worstCredentialKind(d.id) ?? "soon"}
                      label={`${expiring.length} credential${expiring.length > 1 ? "s" : ""} need attention`}
                    />
                  )}
                  <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.6, marginTop: 10 }}>
                    {d.src}
                    {d.workPermit && " · works under a foreign-worker permit"}
                    <br />
                    {d.trips} trips completed · {d.phone}
                  </div>
                </Panel>
                {hosGauge}
              </>
            )}

            {/* ---- Licence & certs ---- */}
            {tab === 1 && (
              <Panel>
                <SectionLabel>Licences &amp; certifications</SectionLabel>
                {creds.length === 0 ? (
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No credentials on file.</div>
                ) : (
                  <div style={{ marginTop: -4 }}>
                    {creds.map((c) => (
                      <CredentialRow key={c.id} c={c} />
                    ))}
                  </div>
                )}
              </Panel>
            )}

            {/* ---- Hours of Service ---- */}
            {tab === 2 && (
              <>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
                  <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>Current duty status</span>
                  {d.duty === "Deactivated" ? <StatusChip kind="over" label="Deactivated" /> : <DutyChip status={d.dutyStatus} />}
                </div>
                {hosGauge}
                <Panel>
                  <SectionLabel>Duty log · driver app + paper backup</SectionLabel>
                  {logs.length === 0 ? (
                    <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No hours-of-service entries recorded.</div>
                  ) : (
                    <div style={{ marginTop: -2 }}>
                      {logs.map((entry) => (
                        <HosLogRow key={entry.id} entry={entry} />
                      ))}
                    </div>
                  )}
                </Panel>
              </>
            )}

            {/* ---- Clearances ---- */}
            {tab === 3 && (
              <Panel>
                <SectionLabel>Client clearances</SectionLabel>
                <div style={{ fontFamily: fonts.body, fontSize: 13, color: statusMeta("ontime").t, fontWeight: 600, lineHeight: 1.7 }}>{clrList}</div>
                <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 6 }}>Ref: LL-ContractorClearances</div>
              </Panel>
            )}

            {/* ---- History ---- */}
            {tab === 4 && (
              <Panel>
                <SectionLabel>Activity</SectionLabel>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.7 }}>
                  {d.trips} trips completed · last DVIR {d.dvir.toLowerCase()}
                  <br />
                  Detailed trip history and incident log — coming with the Trips domain integration.
                </div>
              </Panel>
            )}

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
