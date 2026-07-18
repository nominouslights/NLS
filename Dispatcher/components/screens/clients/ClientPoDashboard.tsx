"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { formatCad, formatUtcDate } from "@/lib/api";
import { posFor, useClientStore } from "@/lib/clientStore";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { poExpiryLabel } from "./shared";

// PO Expiry dashboard for a single client — KPI row (valid / expiring / expired)
// plus a purchase-order list with expiry status. Built fresh, adapting the Fleet
// document-expiry pattern (docStatusFor + StatusChip). Mock (lib/clientStore).

export default function ClientPoDashboard({ clientId }: { clientId: number }) {
  useClientStore();
  const pos = posFor(clientId);

  const valid = pos.filter((p) => p.k === "ontime").length;
  const soon = pos.filter((p) => p.k === "soon").length;
  const expired = pos.filter((p) => p.k === "over").length;

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12 }}>
        <MonoTag color={statusMeta("soon").t}>MOCK</MonoTag>
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          Prototype — purchase-order expiry tracking.
        </span>
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 12, marginBottom: 16 }}>
        <MetricTile icon="✓" iconBg="rgba(0,158,115,.16)" iconColor={statusMeta("ontime").t} label="Valid POs" value={valid} valueColor={colors.headingBright} />
        <MetricTile icon="◐" iconBg="rgba(225,176,0,.18)" iconColor={statusMeta("soon").t} label="Expiring soon" value={soon} valueColor={statusMeta("soon").t} borderColor={soon > 0 ? "rgba(225,176,0,.4)" : undefined} />
        <MetricTile icon="▲" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="Expired" value={expired} valueColor={statusMeta("over").t} borderColor={expired > 0 ? "rgba(213,94,0,.35)" : undefined} />
      </div>

      <Panel>
        <SectionLabel>Purchase orders</SectionLabel>
        {pos.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            No purchase orders on file for this client.
          </div>
        ) : (
          pos.map((p) => (
            <div
              key={p.id}
              style={{
                display: "grid",
                gridTemplateColumns: "150px 1fr 120px 150px",
                gap: 12,
                alignItems: "center",
                padding: "10px 12px",
                marginBottom: 5,
                ...rowSurface(false),
                cursor: "default",
              }}
            >
              <span style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.skyBlue }}>{p.poNumber}</span>
              <div style={{ minWidth: 0 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
                  {p.description ?? "Purchase order"}
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                  Expires {formatUtcDate(p.expiry)}
                </div>
              </div>
              <span style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary, textAlign: "right" }}>
                {p.amountCad != null ? formatCad(p.amountCad) : "—"}
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
