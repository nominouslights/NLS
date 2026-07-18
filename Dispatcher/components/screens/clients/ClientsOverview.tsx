"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { formatUtcDate } from "@/lib/api";
import { clients } from "@/lib/data";
import { openFollowUps, purchaseOrders, useClientStore } from "@/lib/clientStore";
import { docStatusFor } from "@/lib/maintenanceStore";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { CLIENT_TABS, followUpLabel, InteractionTypeChip, poExpiryLabel } from "./shared";

// Clients overview — shown in the detail pane until a client is selected.
// Surfaces upcoming follow-ups (the required "visible somewhere" reminder view)
// and a cross-client PO-expiry watch. Mock (lib/clientStore).

const INTERACTIONS_TAB = CLIENT_TABS.indexOf("Interactions");

export default function ClientsOverview({ onOpenClient }: { onOpenClient: (clientId: number, tab?: number) => void }) {
  useClientStore();

  const nameById = new Map(clients.map((c) => [c.id, c.name]));
  const followUps = openFollowUps();
  const flaggedPos = purchaseOrders
    .map((p) => ({ ...p, k: docStatusFor(p.expiry) }))
    .filter((p) => p.k !== "ontime")
    .sort((a, b) => (a.expiry < b.expiry ? -1 : 1));

  const overdue = followUps.filter((f) => docStatusFor(f.followUpDate as string) === "over").length;
  const dueSoon = followUps.filter((f) => docStatusFor(f.followUpDate as string) === "soon").length;
  const expiredPos = flaggedPos.filter((p) => p.k === "over").length;

  return (
    <div className="detailfade">
      <div style={{ marginBottom: 6 }}>
        <SectionLabel>Client overview</SectionLabel>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginBottom: 16 }}>
          Select a client from the list to view its contract, contact roster, and interaction log.
        </div>
      </div>

      {/* KPI row */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12, marginBottom: 16 }}>
        <MetricTile icon="▲" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="Follow-ups overdue" value={overdue} valueColor={overdue > 0 ? statusMeta("over").t : colors.headingBright} borderColor={overdue > 0 ? "rgba(213,94,0,.35)" : undefined} />
        <MetricTile icon="◐" iconBg="rgba(225,176,0,.18)" iconColor={statusMeta("soon").t} label="Follow-ups due soon" value={dueSoon} valueColor={statusMeta("soon").t} />
        <MetricTile icon="●" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="POs expiring / expired" value={flaggedPos.length} valueColor={expiredPos > 0 ? statusMeta("over").t : colors.headingBright} />
      </div>

      {/* Upcoming follow-ups */}
      <Panel style={{ marginBottom: 14 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
          <SectionLabel>Upcoming follow-ups</SectionLabel>
          <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
        </div>
        {followUps.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No follow-ups scheduled.</div>
        ) : (
          followUps.map((f) => {
            const kind = docStatusFor(f.followUpDate as string);
            return (
              <div
                key={f.id}
                onClick={() => onOpenClient(f.clientId, INTERACTIONS_TAB)}
                style={{ display: "grid", gridTemplateColumns: "1fr 150px", gap: 12, alignItems: "center", padding: "10px 12px", marginBottom: 5, ...rowSurface(false) }}
              >
                <div style={{ minWidth: 0 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 3 }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                      {nameById.get(f.clientId) ?? "Client"}
                    </span>
                    <InteractionTypeChip type={f.type} />
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                    Due {formatUtcDate(f.followUpDate)}
                    {f.followUpNote ? ` · ${f.followUpNote}` : ""}
                  </div>
                </div>
                <div style={{ display: "flex", justifyContent: "flex-end" }}>
                  <StatusChip kind={kind} label={followUpLabel(kind)} />
                </div>
              </div>
            );
          })
        )}
      </Panel>

      {/* Cross-client PO expiry watch */}
      <Panel>
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
          <SectionLabel>PO expiry watch</SectionLabel>
          <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
        </div>
        {flaggedPos.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            All purchase orders are valid.
          </div>
        ) : (
          flaggedPos.map((p) => (
            <div
              key={p.id}
              onClick={() => onOpenClient(p.clientId, 0)}
              style={{ display: "grid", gridTemplateColumns: "1fr 150px 150px", gap: 12, alignItems: "center", padding: "10px 12px", marginBottom: 5, ...rowSurface(false) }}
            >
              <div style={{ minWidth: 0 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                  {nameById.get(p.clientId) ?? "Client"}
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>{p.poNumber}</div>
              </div>
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                Expires {formatUtcDate(p.expiry)}
              </span>
              <div style={{ display: "flex", justifyContent: "flex-end" }}>
                <StatusChip kind={p.k} label={poExpiryLabel(p.k)} />
              </div>
            </div>
          ))
        )}
      </Panel>
    </div>
  );
}
