"use client";

import { useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { ApiError, formatUtcDate } from "@/lib/api";
import {
  listOpenFollowUps,
  listPurchaseOrders,
  poExpiryKindFor,
  type ClientInteractionRecord,
  type ClientRecord,
  type PurchaseOrderRecord,
} from "@/lib/api/clients";
import { docStatusFor } from "@/lib/maintenanceStore";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { CLIENT_TABS, followUpLabel, InteractionTypeChip, poExpiryLabel } from "./shared";

// Clients overview — shown in the detail pane until a client is selected.
// The cross-client PO expiry watch reads the real Clients API (POs fetched
// per client and aggregated client-side — no aggregation endpoint yet). The
// upcoming-follow-ups widget reads the real /api/clients/follow-ups endpoint,
// joined to the roster by client Guid.

const INTERACTIONS_TAB = CLIENT_TABS.indexOf("Interactions");

interface FlaggedPo extends PurchaseOrderRecord {
  clientName: string;
}

export default function ClientsOverview({
  roster,
  onOpenClient,
}: {
  roster: ClientRecord[];
  onOpenClient: (clientId: string, tab?: number) => void;
}) {
  const [allPos, setAllPos] = useState<FlaggedPo[] | null>(null);
  const [poError, setPoError] = useState<string | null>(null);
  const [followUps, setFollowUps] = useState<ClientInteractionRecord[] | null>(null);
  const [followUpError, setFollowUpError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listOpenFollowUps().then(
      (rows) => {
        if (active) {
          setFollowUps(rows);
          setFollowUpError(null);
        }
      },
      (e) => {
        if (active) {
          setFollowUps(null);
          setFollowUpError(e instanceof ApiError ? e.message : "Failed to load follow-ups.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    Promise.all(
      roster.map(async (c) => {
        const rows = await listPurchaseOrders(c.id);
        return rows.map((p): FlaggedPo => ({ ...p, clientName: c.name }));
      }),
    ).then(
      (perClient) => {
        if (active) {
          setAllPos(perClient.flat());
          setPoError(null);
        }
      },
      (e) => {
        if (active) {
          setAllPos(null);
          setPoError(e instanceof ApiError ? e.message : "Failed to load purchase orders.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [roster]);

  // Follow-ups come from the real /api/clients/follow-ups endpoint; join each
  // row to its client by Guid.
  const clientById = new Map(roster.map((c) => [c.id, c]));
  const followUpRows = followUps ?? [];

  const flaggedPos = (allPos ?? [])
    .filter((p) => poExpiryKindFor(p.expiry) !== "ontime")
    .sort((a, b) => ((a.expiry ?? "9999") < (b.expiry ?? "9999") ? -1 : 1));

  const overdue = followUpRows.filter((f) => docStatusFor(f.followUpDate) === "over").length;
  const dueSoon = followUpRows.filter((f) => docStatusFor(f.followUpDate) === "soon").length;
  const expiredPos = flaggedPos.filter((p) => poExpiryKindFor(p.expiry) === "over").length;

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
        <MetricTile icon="▲" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="Follow-ups overdue" value={followUps === null ? "…" : overdue} valueColor={overdue > 0 ? statusMeta("over").t : colors.headingBright} borderColor={overdue > 0 ? "rgba(213,94,0,.35)" : undefined} />
        <MetricTile icon="◐" iconBg="rgba(225,176,0,.18)" iconColor={statusMeta("soon").t} label="Follow-ups due soon" value={followUps === null ? "…" : dueSoon} valueColor={statusMeta("soon").t} />
        <MetricTile icon="●" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="POs expiring / expired" value={allPos === null ? "…" : flaggedPos.length} valueColor={expiredPos > 0 ? statusMeta("over").t : colors.headingBright} />
      </div>

      {/* Upcoming follow-ups — real Clients API (/api/clients/follow-ups) */}
      <Panel style={{ marginBottom: 14 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
          <SectionLabel>Upcoming follow-ups</SectionLabel>
        </div>
        {followUpError ? (
          <StatusChip kind="over" label={`Follow-ups unavailable — ${followUpError}`} />
        ) : followUps === null ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading follow-ups…</div>
        ) : followUpRows.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No follow-ups scheduled.</div>
        ) : (
          followUpRows.map((f) => {
            const kind = docStatusFor(f.followUpDate);
            const apiClient = clientById.get(f.clientId);
            return (
              <div
                key={f.id}
                onClick={apiClient ? () => onOpenClient(apiClient.id, INTERACTIONS_TAB) : undefined}
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 150px",
                  gap: 12,
                  alignItems: "center",
                  padding: "10px 12px",
                  marginBottom: 5,
                  ...rowSurface(false),
                  cursor: apiClient ? "pointer" : "default",
                }}
              >
                <div style={{ minWidth: 0 }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 3 }}>
                    <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                      {apiClient?.name ?? "Client"}
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

      {/* Cross-client PO expiry watch — real Clients API */}
      <Panel>
        <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
          <SectionLabel>PO expiry watch</SectionLabel>
        </div>
        {poError ? (
          <StatusChip kind="over" label={`Purchase orders unavailable — ${poError}`} />
        ) : allPos === null ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            Loading purchase orders…
          </div>
        ) : flaggedPos.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            All purchase orders are valid.
          </div>
        ) : (
          flaggedPos.map((p) => {
            const kind = poExpiryKindFor(p.expiry);
            return (
              <div
                key={p.id}
                onClick={() => onOpenClient(p.clientId, 0)}
                style={{ display: "grid", gridTemplateColumns: "1fr 150px 150px", gap: 12, alignItems: "center", padding: "10px 12px", marginBottom: 5, ...rowSurface(false) }}
              >
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                    {p.clientName}
                  </div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>{p.poNumber}</div>
                </div>
                <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                  Expires {formatUtcDate(p.expiry)}
                </span>
                <div style={{ display: "flex", justifyContent: "flex-end" }}>
                  <StatusChip kind={kind} label={poExpiryLabel(kind)} />
                </div>
              </div>
            );
          })
        )}
      </Panel>
    </div>
  );
}
