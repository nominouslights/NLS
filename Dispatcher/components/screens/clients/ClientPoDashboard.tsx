"use client";

import { useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { ApiError, formatCad, formatUtcDate } from "@/lib/api";
import {
  contractRoundTripRateCad,
  deletePurchaseOrder,
  listPurchaseOrders,
  poExpiryKindFor,
  poTermsLabel,
  refetchUntil,
  sortPurchaseOrders,
  type ActiveContractSummary,
  type PurchaseOrderUpdateInput,
  type PurchaseOrderRecord,
} from "@/lib/api/clients";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { ActionButton } from "@/components/ui/Button";
import PoFormModal from "@/components/PoFormModal";
import { poExpiryLabel } from "./shared";

// PO dashboard for a single client — KPI row (valid / expiring / expired /
// inheriting the contract rate) plus a purchase-order list with real CRUD
// against the Clients API (/api/clients/{id}/purchase-orders). Expiry chips are
// derived client-side from `expiry` (docStatusFor thresholds — Fleet
// document-expiry pattern), never stored.
//
// Each PO carries its OWN pricing terms (roundTripRateCad / oneWayRateCad), and
// what this screen shows is the EFFECTIVE terms, not the stored fields: a blank
// round-trip rate reads as the contract's, and a blank one-way rate shows the ½
// figure it will actually bill at. A blank field that silently means "half of
// something else" is how a pricing bug reaches an invoice — so the wording comes
// from poTermsLabel(), the same helper the accruals report prices and prints
// from (lib/api/clients.ts).

type Editor = { mode: "new" } | { mode: "edit"; po: PurchaseOrderRecord } | null;

export default function ClientPoDashboard({
  clientId,
  clientName,
  contract,
}: {
  clientId: string;
  clientName: string;
  /** The client's active contract — the fallback rate a PO inherits when it
   *  records no round-trip rate of its own. */
  contract: ActiveContractSummary | null;
}) {
  const contractRate = contractRoundTripRateCad(contract);
  // Keyed by clientId so switching clients reads as "loading" until this
  // client's fetch lands.
  const [posState, setPosState] = useState<{ clientId: string; rows: PurchaseOrderRecord[] } | null>(null);
  const [loadErrorState, setLoadErrorState] = useState<{ clientId: string; message: string } | null>(null);
  const [editor, setEditor] = useState<Editor>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listPurchaseOrders(clientId).then(
      (rows) => {
        if (active) {
          setPosState({ clientId, rows: sortPurchaseOrders(rows) });
          setLoadErrorState(null);
        }
      },
      (e) => {
        if (active) {
          setLoadErrorState({
            clientId,
            message: e instanceof ApiError ? e.message : "Failed to load purchase orders.",
          });
        }
      },
    );
    return () => {
      active = false;
    };
  }, [clientId]);

  const pos = posState?.clientId === clientId ? posState.rows : null;
  const loadError = loadErrorState?.clientId === clientId ? loadErrorState.message : null;

  async function runAction(fn: () => Promise<void>) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await fn();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Action failed — please try again.");
    } finally {
      setBusy(false);
    }
  }

  // Reads are eventually consistent projections — refetch with a short retry
  // until the mutation is visible (refetchUntil). Matching the submitted body
  // (not just the id) covers edits, where the stale row still has the old id.
  function poMatches(p: PurchaseOrderRecord, poId: string, input: PurchaseOrderUpdateInput): boolean {
    return (
      p.id === poId &&
      p.poNumber === input.poNumber &&
      p.issued === input.issued &&
      (p.expiry ?? null) === (input.expiry ?? null) &&
      (p.amountCad ?? null) === (input.amountCad ?? null) &&
      (p.roundTripRateCad ?? null) === (input.roundTripRateCad ?? null) &&
      (p.oneWayRateCad ?? null) === (input.oneWayRateCad ?? null) &&
      (p.note ?? null) === (input.note ?? null)
    );
  }

  async function onSaved(poId: string, input: PurchaseOrderUpdateInput) {
    await runAction(async () => {
      const rows = await refetchUntil(
        () => listPurchaseOrders(clientId),
        (r) => r.some((p) => poMatches(p, poId, input)),
      );
      setPosState({ clientId, rows: sortPurchaseOrders(rows) });
    });
  }

  async function onDelete(poId: string) {
    await runAction(async () => {
      await deletePurchaseOrder(clientId, poId);
      const rows = await refetchUntil(
        () => listPurchaseOrders(clientId),
        (r) => !r.some((p) => p.id === poId),
      );
      setPosState({ clientId, rows: sortPurchaseOrders(rows) });
    });
  }

  const kinds = (pos ?? []).map((p) => poExpiryKindFor(p.expiry));
  const valid = kinds.filter((k) => k === "ontime").length;
  const soon = kinds.filter((k) => k === "soon").length;
  const expired = kinds.filter((k) => k === "over").length;
  // POs with no round-trip rate of their own bill at the contract rate — worth a
  // tile, because that inheritance is invisible in the stored record.
  const inheriting = (pos ?? []).filter((p) => p.roundTripRateCad == null).length;

  return (
    <div>
      <div style={{ display: "grid", gridTemplateColumns: "repeat(4, 1fr)", gap: 12, marginBottom: 16 }}>
        <MetricTile icon="✓" iconBg="rgba(0,158,115,.16)" iconColor={statusMeta("ontime").t} label="Valid POs" value={valid} valueColor={colors.headingBright} />
        <MetricTile icon="◐" iconBg="rgba(225,176,0,.18)" iconColor={statusMeta("soon").t} label="Expiring soon" value={soon} valueColor={statusMeta("soon").t} borderColor={soon > 0 ? "rgba(225,176,0,.4)" : undefined} />
        <MetricTile icon="▲" iconBg="rgba(213,94,0,.16)" iconColor={statusMeta("over").t} label="Expired" value={expired} valueColor={statusMeta("over").t} borderColor={expired > 0 ? "rgba(213,94,0,.35)" : undefined} />
        <MetricTile icon="◈" iconBg="rgba(74,74,74,.14)" iconColor={statusMeta("off").t} label="Rate from contract" value={inheriting} valueColor={colors.headingBright} />
      </div>

      <Panel>
        <div style={{ display: "flex", alignItems: "center", marginBottom: 12 }}>
          <SectionLabel>Purchase orders</SectionLabel>
          <ActionButton
            variant="primary"
            style={{ marginLeft: "auto" }}
            disabled={busy}
            onClick={() => setEditor({ mode: "new" })}
          >
            + ADD PO
          </ActionButton>
        </div>

        {actionError && (
          <div style={{ marginBottom: 10 }}>
            <StatusChip kind="over" label={actionError} />
          </div>
        )}

        {loadError ? (
          <StatusChip kind="over" label={`Purchase orders unavailable — ${loadError}`} />
        ) : pos === null ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            Loading purchase orders…
          </div>
        ) : pos.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            No purchase orders on file for this client.
          </div>
        ) : (
          pos.map((p) => {
            const kind = poExpiryKindFor(p.expiry);
            return (
              <div
                key={p.id}
                style={{
                  display: "grid",
                  gridTemplateColumns: "150px 1fr 150px 150px 110px",
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
                    {p.note ?? "Purchase order"}
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>
                    Issued {formatUtcDate(p.issued)}
                    {p.expiry ? ` · expires ${formatUtcDate(p.expiry)}` : " · no expiry"}
                  </div>
                  {/* EFFECTIVE terms, not the stored fields — an inherited rate
                      says where it came from, and a ½ one-way rate says so. */}
                  <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textMuted, marginTop: 2 }}>
                    {poTermsLabel(p, contractRate)}
                  </div>
                </div>
                <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 3 }}>
                  <span style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textSecondary }}>
                    {p.amountCad != null ? formatCad(p.amountCad) : "—"}
                  </span>
                  {p.amountCad == null && <StatusChip kind="off" label="No value recorded" />}
                </div>
                <div style={{ display: "flex", justifyContent: "flex-end" }}>
                  <StatusChip kind={kind} label={poExpiryLabel(kind)} />
                </div>
                <div style={{ display: "flex", justifyContent: "flex-end", gap: 10 }}>
                  <span
                    onClick={busy ? undefined : () => setEditor({ mode: "edit", po: p })}
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontWeight: 600,
                      fontSize: 10,
                      letterSpacing: ".08em",
                      textTransform: "uppercase",
                      color: busy ? colors.textFaint : colors.blue,
                      cursor: busy ? "wait" : "pointer",
                    }}
                  >
                    Edit
                  </span>
                  <span
                    onClick={busy ? undefined : () => onDelete(p.id)}
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontWeight: 600,
                      fontSize: 10,
                      letterSpacing: ".08em",
                      textTransform: "uppercase",
                      color: busy ? colors.textFaint : statusMeta("over").t,
                      cursor: busy ? "wait" : "pointer",
                    }}
                  >
                    Delete
                  </span>
                </div>
              </div>
            );
          })
        )}
      </Panel>

      {editor?.mode === "new" && (
        <PoFormModal
          clientId={clientId}
          clientName={clientName}
          contractRateCad={contractRate}
          onClose={() => setEditor(null)}
          onSaved={onSaved}
        />
      )}
      {editor?.mode === "edit" && (
        <PoFormModal
          clientId={clientId}
          clientName={clientName}
          contractRateCad={contractRate}
          existing={editor.po}
          onClose={() => setEditor(null)}
          onSaved={onSaved}
        />
      )}
    </div>
  );
}
