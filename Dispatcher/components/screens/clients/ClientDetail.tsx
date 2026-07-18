"use client";

import { colors, fonts, svcMeta } from "@/lib/theme";
import type { Client } from "@/lib/types";
import { Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import ContactRoster from "./ContactRoster";
import InteractionTimeline from "./InteractionTimeline";
import ClientPoDashboard from "./ClientPoDashboard";
import { CLIENT_TABS, isClientType, VENDOR_TABS } from "./shared";

// Unified client detail — one screen per organization combining the contract /
// PO expiry dashboard, the contact roster, and the interaction timeline. The
// roster + interaction tabs are Client-type only; Vendor/Partner records get the
// contract/PO view only (no CRM UI yet).

export default function ClientDetail({
  client,
  tab,
  setTab,
  onCreateTrip,
}: {
  client: Client;
  tab: number;
  setTab: (n: number) => void;
  onCreateTrip: () => void;
}) {
  const accent = svcMeta(client.svc).accent;
  const clientType = isClientType(client);
  const tabs = clientType ? CLIENT_TABS : VENDOR_TABS;
  const activeTab = tab < tabs.length ? tab : 0;

  return (
    <div className="detailfade" key={client.id}>
      {/* header */}
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
          {client.tag}
        </span>
        <span style={{ marginLeft: "auto" }}>
          <StatusChip kind={client.rk} label={client.renew} />
        </span>
      </div>
      <h2
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 28,
          lineHeight: 1,
          color: colors.headingBright,
          margin: "6px 0 14px",
        }}
      >
        {client.name}
      </h2>

      {/* tab bar */}
      <div style={{ display: "flex", gap: 2, borderBottom: `1px solid ${colors.border}`, marginBottom: 16, flexWrap: "wrap" }}>
        {tabs.map((t, i) => (
          <span
            key={t}
            onClick={() => setTab(i)}
            style={{
              fontFamily: fonts.body,
              fontWeight: activeTab === i ? 600 : 500,
              fontSize: 12.5,
              padding: "9px 14px",
              color: activeTab === i ? colors.headingBright : colors.textDim,
              borderBottom: activeTab === i ? `2px solid ${colors.blue}` : undefined,
              marginBottom: -1,
              cursor: "pointer",
              whiteSpace: "nowrap",
            }}
          >
            {t}
          </span>
        ))}
      </div>

      {/* Overview & POs */}
      {activeTab === 0 && (
        <div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
            <Panel>
              <SectionLabel>Contract summary</SectionLabel>
              <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                <DetailRow label="Term" value={client.term} valueStyle={{ fontFamily: fonts.mono, fontSize: 11.5 }} />
                <DetailRow label="Rate schedule" value={client.rate} />
              </div>
            </Panel>
            <Panel>
              <SectionLabel>PO &amp; billing config</SectionLabel>
              <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                <DetailRow label="PO structure" value={client.po} valueStyle={{ fontFamily: fonts.mono, fontSize: 11.5 }} />
                <DetailRow label="Tax" value={client.gst} />
              </div>
            </Panel>
          </div>

          {client.notes && (
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
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, lineHeight: 1.55, color: colors.textSecondary }}>
                {client.notes}
              </div>
            </div>
          )}

          <div style={{ marginBottom: 16 }}>
            <ClientPoDashboard clientId={client.id} />
          </div>

          <div style={{ display: "flex", gap: 9 }}>
            <ActionButton variant="amber" onClick={onCreateTrip}>
              CREATE TRIP FOR THIS CLIENT
            </ActionButton>
            <ActionButton>VIEW TRIP HISTORY</ActionButton>
          </div>

          {!clientType && (
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 14 }}>
              Contact roster and interaction log apply to Client-type organizations. This is a Vendor / Partner
              record.
            </div>
          )}
        </div>
      )}

      {/* Contacts (client-type only) */}
      {clientType && activeTab === 1 && <ContactRoster clientId={client.id} clientName={client.name} />}

      {/* Interactions (client-type only) */}
      {clientType && activeTab === 2 && <InteractionTimeline clientId={client.id} clientName={client.name} />}
    </div>
  );
}
