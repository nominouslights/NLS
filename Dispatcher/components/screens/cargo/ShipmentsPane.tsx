"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  addShipmentLeg,
  cancelShipment,
  deliverShipment,
  getShipment,
  isShipmentOpen,
  listShipments,
  recordShipmentLegDrop,
  recordShipmentLegPickup,
  refetchUntil,
  registerShipment,
  removeShipmentLeg,
  setShipmentSecured,
  shipmentChip,
  shipmentDimsLabel,
  shipmentLegChip,
  shipmentStatusLabel,
  SHIPMENT_KIND_LABELS,
  SHIPMENT_KINDS,
  updateShipment,
  type ShipmentInput,
  type ShipmentKind,
  type ShipmentLegInput,
  type ShipmentRecord,
  type ShipmentStatus,
} from "@/lib/api/shipments";
import { shortDateLabel } from "@/lib/api/trips";
import { Panel, SectionLabel, DetailRow } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { ModalShell } from "@/components/ui/ModalShell";
import { Pager } from "@/components/ui/Pager";
import { TextAreaField, TextField } from "@/components/ui/Field";
import RegisterShipmentModal from "@/components/screens/cargo/RegisterShipmentModal";
import AssignLegModal from "@/components/screens/cargo/AssignLegModal";

// Shipments — master/detail over GET /api/trips/shipments. Every filter is
// applied server-side (the page and its total always agree); mutations follow
// the eventual-consistency pattern: mutate, then refetchUntil the projection
// shows the change.
//
// Deferred deliberately (not yet in this UI, API calls exist in
// lib/api/shipments.ts): bulk-assign, shipment billing edits
// (PUT /{id}/billing), and close-without-billing.

const PAGE_SIZE = 50;

/** Label attributed to dispatcher-recorded leg events (no user id yet). */
const DISPATCHER_LABEL = "Dispatch";

const STATUS_FILTERS: { value: ShipmentStatus | ""; label: string }[] = [
  { value: "", label: "All statuses" },
  { value: "Registered", label: "Registered" },
  { value: "Assigned", label: "Assigned" },
  { value: "InTransit", label: "In transit" },
  { value: "Delivered", label: "Delivered" },
  { value: "ReadyForBilling", label: "Ready for billing" },
  { value: "Cancelled", label: "Cancelled" },
];

function fmtUtc(iso: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("en-CA", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit", hour12: true });
}

const cad = new Intl.NumberFormat("en-CA", { style: "currency", currency: "CAD" });

export default function ShipmentsPane({
  selectedId,
  setSelectedId,
  onOpenTrip,
  registerOpen,
  setRegisterOpen,
}: {
  selectedId: string | null;
  setSelectedId: (id: string | null) => void;
  onOpenTrip: (tripId: string) => void;
  registerOpen: boolean;
  setRegisterOpen: (open: boolean) => void;
}) {
  const [rows, setRows] = useState<ShipmentRecord[] | null>(null);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [loadError, setLoadError] = useState<string | null>(null);
  // A selected shipment no longer matching the filters is still shown in detail.
  const [extra, setExtra] = useState<ShipmentRecord | null>(null);

  // ---- filters (all server-side) ----
  const [statusF, setStatusF] = useState<ShipmentStatus | "">("");
  const [kindF, setKindF] = useState<ShipmentKind | "">("");
  const [unassigned, setUnassigned] = useState(false);
  const [q, setQ] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const [modal, setModal] = useState<null | "edit" | "assign" | "deliver" | "cancel">(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const fetchList = useCallback(() => {
    return listShipments({
      status: statusF || undefined,
      kind: kindF || undefined,
      unassignedOnly: unassigned || undefined,
      q: q.trim() || undefined,
      from: from || undefined,
      to: to || undefined,
      page,
      pageSize: PAGE_SIZE,
    });
  }, [statusF, kindF, unassigned, q, from, to, page]);

  const load = useCallback(async () => {
    try {
      const fresh = await fetchList();
      setRows(fresh.items);
      setTotalCount(fresh.totalCount);
      setLoadError(null);
    } catch (e) {
      setRows(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load shipments.");
    }
  }, [fetchList]);

  useEffect(() => {
    let active = true;
    fetchList().then(
      (fresh) => {
        if (active) {
          setRows(fresh.items);
          setTotalCount(fresh.totalCount);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRows(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load shipments.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [fetchList]);

  const fromList = rows?.find((r) => r.id === selectedId) ?? null;
  const s = fromList ?? (extra && extra.id === selectedId ? extra : null) ?? rows?.[0] ?? null;

  /** Poll the mutated shipment until the projection satisfies the predicate,
   *  then refresh the page around it (Trips.tsx pattern — a mutation can move
   *  the row off the current filter entirely). */
  async function reloadUntil(id: string, satisfied: (sh: ShipmentRecord | undefined) => boolean) {
    const updated = await refetchUntil(() => getShipment(id).catch(() => undefined), satisfied);
    if (updated) setExtra(updated);
    await load();
  }

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

  async function onSaved(input: ShipmentInput, existingId: string | null) {
    if (existingId) {
      const before = rows?.find((r) => r.id === existingId)?.updatedAtUtc ?? extra?.updatedAtUtc ?? null;
      await updateShipment(existingId, input);
      await reloadUntil(
        existingId,
        (sh) => sh !== undefined && sh.description === input.description && sh.updatedAtUtc !== before,
      );
    } else {
      const newId = await registerShipment(input);
      await reloadUntil(newId, (sh) => sh !== undefined);
      setSelectedId(newId);
    }
  }

  async function onLegAssigned(id: string, input: ShipmentLegInput) {
    await addShipmentLeg(id, input);
    await reloadUntil(id, (sh) => sh !== undefined && sh.legs.some((l) => l.tripId === input.tripId));
  }

  const filterChip = (on: boolean, label: string, onClick: () => void) => (
    <span
      onClick={onClick}
      style={{
        fontFamily: fonts.body,
        fontWeight: on ? 600 : 500,
        fontSize: 12,
        padding: "5px 12px",
        borderRadius: 7,
        background: on ? colors.cardBgActive : colors.cardBg,
        border: `1px solid ${on ? colors.borderActive : colors.border}`,
        color: on ? colors.headingBright : colors.textMuted,
        cursor: "pointer",
        userSelect: "none",
        whiteSpace: "nowrap",
      }}
    >
      {label}
    </span>
  );

  const filters = (
    <div style={{ flex: "none", padding: "0 26px 12px", display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
      {STATUS_FILTERS.map((f) =>
        filterChip(statusF === f.value, f.label, () => {
          setStatusF(f.value);
          setPage(1);
        }),
      )}
      <span style={{ width: 1, height: 20, background: colors.border }} />
      {filterChip(kindF === "", "All kinds", () => {
        setKindF("");
        setPage(1);
      })}
      {SHIPMENT_KINDS.map((k) =>
        filterChip(kindF === k, SHIPMENT_KIND_LABELS[k], () => {
          setKindF(k);
          setPage(1);
        }),
      )}
      <span style={{ width: 1, height: 20, background: colors.border }} />
      {filterChip(unassigned, "Unassigned only", () => {
        setUnassigned(!unassigned);
        setPage(1);
      })}
      <input
        className="nl-input"
        value={q}
        placeholder="Search shipments…"
        onChange={(e) => {
          setQ(e.target.value);
          setPage(1);
        }}
        style={{
          height: 30,
          boxSizing: "border-box",
          borderRadius: 7,
          background: colors.inputBg,
          border: `1px solid ${colors.borderStrong}`,
          padding: "0 10px",
          fontFamily: fonts.body,
          fontSize: 12,
          color: colors.textPrimary,
          outline: "none",
          width: 170,
        }}
      />
      <input
        type="date"
        className="nl-input"
        value={from}
        onChange={(e) => {
          setFrom(e.target.value);
          setPage(1);
        }}
        title="From date"
        style={{
          height: 30,
          boxSizing: "border-box",
          borderRadius: 7,
          background: colors.inputBg,
          border: `1px solid ${colors.borderStrong}`,
          padding: "0 8px",
          fontFamily: fonts.mono,
          fontSize: 11.5,
          color: colors.textPrimary,
          colorScheme: "light",
          outline: "none",
        }}
      />
      <input
        type="date"
        className="nl-input"
        value={to}
        onChange={(e) => {
          setTo(e.target.value);
          setPage(1);
        }}
        title="To date"
        style={{
          height: 30,
          boxSizing: "border-box",
          borderRadius: 7,
          background: colors.inputBg,
          border: `1px solid ${colors.borderStrong}`,
          padding: "0 8px",
          fontFamily: fonts.mono,
          fontSize: 11.5,
          color: colors.textPrimary,
          colorScheme: "light",
          outline: "none",
        }}
      />
    </div>
  );

  const modals = (
    <>
      {registerOpen && (
        <RegisterShipmentModal existing={null} onClose={() => setRegisterOpen(false)} onSaved={onSaved} />
      )}
      {modal === "edit" && s && (
        <RegisterShipmentModal existing={s} onClose={() => setModal(null)} onSaved={onSaved} />
      )}
      {modal === "assign" && s && (
        <AssignLegModal
          shipment={s}
          onClose={() => setModal(null)}
          onAssigned={(input) => onLegAssigned(s.id, input)}
        />
      )}
      {modal === "deliver" && s && (
        <DeliverModal
          shipment={s}
          onClose={() => setModal(null)}
          onConfirmed={async (receivedBy, note) => {
            await deliverShipment(s.id, { receivedBy: receivedBy || null, note: note || null });
            await reloadUntil(s.id, (sh) => sh !== undefined && sh.deliveredAtUtc !== null);
          }}
        />
      )}
      {modal === "cancel" && s && (
        <CancelShipmentModal
          shipment={s}
          onClose={() => setModal(null)}
          onConfirmed={async (reason) => {
            await cancelShipment(s.id, reason || null);
            await reloadUntil(s.id, (sh) => sh !== undefined && sh.status === "Cancelled");
          }}
        />
      )}
    </>
  );

  if (loadError) {
    return (
      <>
        {filters}
        <div style={{ padding: "14px 26px", maxWidth: 560 }}>
          <Panel borderColor="rgba(213,94,0,.4)">
            <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
              <StatusChip kind="over" label={`Shipments unavailable — ${loadError}`} />
              <ActionButton variant="primary" onClick={load}>
                RETRY
              </ActionButton>
            </div>
          </Panel>
        </div>
        {modals}
      </>
    );
  }

  if (rows === null) {
    return (
      <>
        {filters}
        <div style={{ padding: "4px 26px" }}>
          {[0, 1, 2, 3].map((i) => (
            <div
              key={i}
              style={{
                height: 58,
                borderRadius: 9,
                border: `1px solid ${colors.borderSubtle}`,
                background: colors.cardBg,
                marginBottom: 6,
                opacity: 0.55 - i * 0.1,
              }}
            />
          ))}
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
            Loading shipments from API…
          </div>
        </div>
        {modals}
      </>
    );
  }

  const legActionsAllowed = !!s && isShipmentOpen(s);

  return (
    <>
      {filters}
      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "38% 1fr", borderTop: `1px solid ${colors.border}` }}>
        {/* MASTER — shipment list */}
        <div style={{ minHeight: 0, display: "flex", flexDirection: "column", borderRight: `1px solid ${colors.border}` }}>
          <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "16px 18px" }}>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 9.5,
                letterSpacing: ".14em",
                textTransform: "uppercase",
                color: colors.textFaint,
                marginBottom: 10,
              }}
            >
              {totalCount} shipment{totalCount === 1 ? "" : "s"}
            </div>
            {rows.length === 0 && (
              <Panel>
                <SectionLabel>No shipments</SectionLabel>
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                  Nothing matches the current filters. Register a shipment with the button above — cargo can exist long
                  before the trip that carries it does.
                </div>
              </Panel>
            )}
            {rows.map((row) => {
              const chip = shipmentChip(row);
              const active = s !== null && row.id === s.id;
              return (
                <div
                  key={row.id}
                  onClick={() => setSelectedId(row.id)}
                  style={{
                    padding: "11px 13px",
                    marginBottom: 5,
                    borderRadius: 9,
                    border: `1px solid ${active ? colors.borderActive : colors.borderSubtle}`,
                    background: active ? colors.cardBgActive : colors.cardBg,
                    boxShadow: active ? `inset 3px 0 0 ${colors.blue}, ${colors.shadowCard}` : colors.shadowCard,
                    cursor: "pointer",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                    <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{row.shipmentNumber}</span>
                    <span
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12.5,
                        fontWeight: 600,
                        color: colors.textPrimary,
                        minWidth: 0,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                        flex: 1,
                      }}
                    >
                      {row.description}
                    </span>
                    <StatusChip kind={chip.kind} label={chip.label} />
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 4 }}>
                    {SHIPMENT_KIND_LABELS[row.kind]} · {row.originName} → {row.destinationName}
                    {row.clientName ? ` · ${row.clientName}` : ""}
                    {row.legs.length > 0 ? ` · ${row.legs.length} leg${row.legs.length === 1 ? "" : "s"}` : " · no trip yet"}
                  </div>
                </div>
              );
            })}
          </div>
          <Pager page={page} pageSize={PAGE_SIZE} totalCount={totalCount} onPage={setPage} />
        </div>

        {/* DETAIL */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {actionError && (
            <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
              <StatusChip kind="over" label={actionError} />
            </Panel>
          )}

          {s === null ? (
            <Panel>
              <SectionLabel>Nothing selected</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                Register a shipment, then assign it onto a Cargo or Grocery trip. A shipment rides one leg per trip —
                two legs is a hub transfer.
              </div>
            </Panel>
          ) : (
            <div className="detailfade" key={s.id}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 6 }}>
                {(() => {
                  const chip = shipmentChip(s);
                  return <StatusChip kind={chip.kind} label={chip.label} />;
                })()}
                {s.hazmat && <StatusChip kind="over" label="Hazmat" />}
                <span style={{ marginLeft: "auto", fontFamily: fonts.mono, fontSize: 13, color: colors.skyBlue }}>
                  {s.shipmentNumber}
                </span>
              </div>
              <h2 style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 24, lineHeight: 1.1, color: colors.headingBright, margin: "4px 0" }}>
                {s.description}
              </h2>
              <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textMuted, marginBottom: 16 }}>
                {SHIPMENT_KIND_LABELS[s.kind]} · {s.originName} → {s.destinationName} · {s.pieces} pc
                {s.pieces === 1 ? "" : "s"}
                {shipmentDimsLabel(s) ? ` · ${shipmentDimsLabel(s)}` : ""}
              </div>

              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
                <Panel>
                  <SectionLabel>Parties</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow label="Bills to" value={s.clientName ?? "no client — walk-up"} />
                    <DetailRow label="PO" value={s.poNumber ?? "—"} valueStyle={{ fontFamily: fonts.mono }} />
                    <DetailRow
                      label="Consignor (from)"
                      value={s.consignorName ? `${s.consignorName}${s.consignorContact ? ` · ${s.consignorContact}` : ""}` : "—"}
                    />
                    <DetailRow
                      label="Consignee (to)"
                      value={s.consigneeName ? `${s.consigneeName}${s.consigneeContact ? ` · ${s.consigneeContact}` : ""}` : "—"}
                    />
                  </div>
                </Panel>
                <Panel>
                  <SectionLabel>Handling &amp; billing</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    <DetailRow
                      label="Secured"
                      value={
                        s.secured ? (
                          <StatusChip kind="ontime" label="Secured" />
                        ) : (
                          <StatusChip kind="soon" label="Not secured" />
                        )
                      }
                    />
                    <DetailRow label="Declared value" value={s.declaredValueCad != null ? cad.format(s.declaredValueCad) : "—"} valueStyle={{ fontFamily: fonts.mono }} />
                    <DetailRow label="Charge" value={s.chargeCad != null ? cad.format(s.chargeCad) : "—"} valueStyle={{ fontFamily: fonts.mono }} />
                    <DetailRow label="Payment" value={s.paymentMethod ?? "—"} />
                    <DetailRow label="Ready / required by" value={`${s.readyDate ?? "—"} / ${s.requiredByDate ?? "—"}`} valueStyle={{ fontFamily: fonts.mono }} />
                    {s.specialHandling && <DetailRow label="Special handling" value={s.specialHandling} />}
                  </div>
                </Panel>
              </div>

              {/* legs timeline */}
              <Panel style={{ marginBottom: 12 }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 10 }}>
                  <SectionLabel>Legs · which runs move the goods</SectionLabel>
                  {legActionsAllowed && (
                    <span style={{ marginLeft: "auto" }}>
                      <ActionButton variant="primary" onClick={() => setModal("assign")} disabled={busy}>
                        ASSIGN TO TRIP
                      </ActionButton>
                    </span>
                  )}
                </div>
                {s.legs.length === 0 ? (
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, lineHeight: 1.6 }}>
                    No legs yet — assign this shipment onto a Cargo or Grocery trip. A cargo trip cannot go en route
                    until at least one shipment is assigned to it.
                  </div>
                ) : (
                  <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                    {s.legs.map((leg) => {
                      const lc = shipmentLegChip(leg);
                      return (
                        <div
                          key={leg.id}
                          style={{
                            display: "flex",
                            alignItems: "center",
                            gap: 9,
                            flexWrap: "wrap",
                            padding: "9px 11px",
                            borderRadius: 9,
                            border: `1px solid ${colors.borderSubtle}`,
                            background: colors.cardBg,
                          }}
                        >
                          <span style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textFaint }}>#{leg.sequence}</span>
                          <span
                            onClick={() => onOpenTrip(leg.tripId)}
                            style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.blue, cursor: "pointer", textDecoration: "underline" }}
                            title="Open this trip on the Trips screen"
                          >
                            {leg.tripNumber}
                          </span>
                          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary }}>
                            {shortDateLabel(leg.tripServiceDate)} · {leg.fromName} → {leg.toName}
                            {leg.tripClientName ? ` · carried on a ${leg.tripClientName} run` : ""}
                          </span>
                          <StatusChip kind={lc.kind} label={lc.label} />
                          {leg.status === "PickedUp" && (
                            <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
                              by {leg.pickedUpBy ?? "—"} · {fmtUtc(leg.pickedUpAtUtc)}
                            </span>
                          )}
                          {leg.status === "Dropped" && (
                            <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
                              {fmtUtc(leg.droppedAtUtc)}
                            </span>
                          )}
                          {legActionsAllowed && (
                            <span style={{ marginLeft: "auto", display: "flex", gap: 6 }}>
                              {leg.status === "Planned" && (
                                <>
                                  <ActionButton
                                    variant="success"
                                    disabled={busy}
                                    onClick={() =>
                                      runAction(async () => {
                                        await recordShipmentLegPickup(s.id, leg.sequence, DISPATCHER_LABEL);
                                        await reloadUntil(
                                          s.id,
                                          (sh) => sh !== undefined && sh.legs.find((l) => l.id === leg.id)?.status === "PickedUp",
                                        );
                                      })
                                    }
                                  >
                                    PICKUP
                                  </ActionButton>
                                  <ActionButton
                                    variant="destructive"
                                    disabled={busy}
                                    onClick={() =>
                                      runAction(async () => {
                                        await removeShipmentLeg(s.id, leg.sequence);
                                        await reloadUntil(
                                          s.id,
                                          (sh) => sh !== undefined && !sh.legs.some((l) => l.id === leg.id),
                                        );
                                      })
                                    }
                                  >
                                    REMOVE
                                  </ActionButton>
                                </>
                              )}
                              {leg.status === "PickedUp" && (
                                <ActionButton
                                  variant="success"
                                  disabled={busy}
                                  onClick={() =>
                                    runAction(async () => {
                                      await recordShipmentLegDrop(s.id, leg.sequence, DISPATCHER_LABEL);
                                      await reloadUntil(
                                        s.id,
                                        (sh) => sh !== undefined && sh.legs.find((l) => l.id === leg.id)?.status === "Dropped",
                                      );
                                    })
                                  }
                                >
                                  DROP
                                </ActionButton>
                              )}
                            </span>
                          )}
                        </div>
                      );
                    })}
                  </div>
                )}
                {s.awaitingTransfer && (
                  <div style={{ marginTop: 10 }}>
                    <StatusChip kind="soon" label="At a hub between legs — visible inventory, assign the onward run" />
                  </div>
                )}
              </Panel>

              {/* delivery / closure record */}
              {(s.deliveredAtUtc || s.cancelledReason || s.writtenOffReason) && (
                <Panel style={{ marginBottom: 12 }}>
                  <SectionLabel>Closure</SectionLabel>
                  <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
                    {s.deliveredAtUtc && (
                      <>
                        <DetailRow label="Delivered" value={fmtUtc(s.deliveredAtUtc)} valueStyle={{ fontFamily: fonts.mono }} />
                        <DetailRow label="Received by" value={s.receivedBy ?? "—"} />
                        {s.deliveryNote && <DetailRow label="Note" value={s.deliveryNote} />}
                      </>
                    )}
                    {s.cancelledReason && <DetailRow label="Cancelled" value={s.cancelledReason} />}
                    {s.writtenOffReason && <DetailRow label="Written off" value={s.writtenOffReason} />}
                  </div>
                </Panel>
              )}

              {/* actions */}
              <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 9 }}>
                {!isShipmentOpen(s) && (
                  <StatusChip kind={shipmentChip(s).kind} label={`${shipmentStatusLabel(s.status)} · read-only`} />
                )}
                {isShipmentOpen(s) && (
                  <>
                    <ActionButton variant="primary" onClick={() => setModal("edit")} disabled={busy}>
                      EDIT SHIPMENT
                    </ActionButton>
                    <ActionButton
                      onClick={() =>
                        runAction(async () => {
                          const target = !s.secured;
                          await setShipmentSecured(s.id, target);
                          await reloadUntil(s.id, (sh) => sh !== undefined && sh.secured === target);
                        })
                      }
                      disabled={busy}
                    >
                      {s.secured ? "MARK NOT SECURED" : "MARK SECURED"}
                    </ActionButton>
                    <ActionButton variant="success" onClick={() => setModal("deliver")} disabled={busy}>
                      DELIVER
                    </ActionButton>
                    <ActionButton variant="destructive" onClick={() => setModal("cancel")} disabled={busy}>
                      CANCEL
                    </ActionButton>
                  </>
                )}
              </div>
            </div>
          )}
        </div>
      </div>
      {modals}
    </>
  );
}

// ---------------------------------------------------------------------------
// Small confirm modals
// ---------------------------------------------------------------------------

function DeliverModal({
  shipment,
  onClose,
  onConfirmed,
}: {
  shipment: ShipmentRecord;
  onClose: () => void;
  onConfirmed: (receivedBy: string, note: string) => Promise<void>;
}) {
  const [receivedBy, setReceivedBy] = useState("");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function confirm() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await onConfirmed(receivedBy.trim(), note.trim());
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to record the delivery — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Shipment · ${shipment.shipmentNumber}`}
      title="Record Delivery"
      onClose={onClose}
      error={error}
      maxWidth={480}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="success" onClick={confirm} disabled={busy}>
            {busy ? "WORKING…" : "RECORD DELIVERY"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        <TextField label="Received by (optional)" value={receivedBy} onChange={setReceivedBy} placeholder="B. Moody" />
        <TextAreaField label="Delivery note (optional)" value={note} onChange={setNote} rows={2} />
      </div>
    </ModalShell>
  );
}

function CancelShipmentModal({
  shipment,
  onClose,
  onConfirmed,
}: {
  shipment: ShipmentRecord;
  onClose: () => void;
  onConfirmed: (reason: string) => Promise<void>;
}) {
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function confirm() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await onConfirmed(reason.trim());
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to cancel the shipment — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Shipment · ${shipment.shipmentNumber}`}
      title="Cancel Shipment"
      onClose={onClose}
      error={error}
      maxWidth={480}
      footer={
        <>
          <ActionButton onClick={onClose}>KEEP SHIPMENT</ActionButton>
          <ActionButton variant="destructive" onClick={confirm} disabled={busy}>
            {busy ? "WORKING…" : "CANCEL SHIPMENT"}
          </ActionButton>
        </>
      }
    >
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 12 }}>
        Cancelling removes the shipment from routing — its planned legs stop counting toward the cargo trips they were
        assigned to.
      </div>
      <TextAreaField label="Reason (optional)" value={reason} onChange={setReason} rows={2} />
    </ModalShell>
  );
}
