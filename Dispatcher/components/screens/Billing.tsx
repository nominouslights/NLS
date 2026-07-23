"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  arAgingFor,
  defaultBillingPeriod,
  formatInvoiceCad,
  generateDraftInvoice,
  getInvoice,
  invoiceAgeLabel,
  invoiceChip,
  listBillableTrips,
  listInvoices,
  markInvoicePaid,
  periodLabel,
  qboKindFor,
  QBO_LABELS,
  refetchUntil,
  replaceInvoiceLines,
  sendInvoice,
  setInvoiceQboStatus,
  sortInvoices,
  voidInvoice,
  daysPastDue,
  type ArAging,
  type BillableTripRecord,
  type InvoiceDetailRecord,
  type InvoiceLineInput,
  type InvoiceSummaryRecord,
  type QboSyncStatus,
} from "@/lib/api/billing";
import { listClients, type ClientRecord } from "@/lib/api/clients";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextField } from "@/components/ui/Field";

// Billing & Invoicing — real invoices from the Billing API (GET /api/billing/
// invoices). Draft generation pulls uninvoiced completed round trips at the
// contract rate; lines are editable while Draft only; Overdue and AR aging are
// frontend derivations of Sent + dueDate (never stored statuses). QuickBooks
// Online stays a READ-ONLY book of record — the qbo-status endpoint merely
// records reconciliation state; there is no write path to QBO.

const cardStyle = {
  padding: "13px 15px",
  background: colors.cardBg,
  border: `1px solid ${colors.border}`,
  borderRadius: 10,
  boxShadow: colors.shadowCard,
} as const;

function MiniCard({ label, value, mono = true }: { label: string; value: string; mono?: boolean }) {
  return (
    <div style={cardStyle}>
      <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 4 }}>{label}</div>
      <div
        style={{
          fontFamily: mono ? fonts.mono : fonts.body,
          fontSize: 12,
          color: colors.textSecondary,
          fontWeight: mono ? undefined : 500,
        }}
      >
        {value}
      </div>
    </div>
  );
}

function ConfirmModal({
  eyebrow,
  title,
  body,
  confirmLabel,
  destructive = false,
  busy,
  onConfirm,
  onClose,
}: {
  eyebrow: string;
  title: string;
  body: string;
  confirmLabel: string;
  destructive?: boolean;
  busy: boolean;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <ModalShell
      eyebrow={eyebrow}
      title={title}
      onClose={onClose}
      maxWidth={480}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton
            variant={destructive ? "destructive" : "primary"}
            onClick={onConfirm}
            style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}
          >
            {busy ? "WORKING…" : confirmLabel}
          </ActionButton>
        </>
      }
    >
      <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textSecondary, lineHeight: 1.6 }}>{body}</div>
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Generate-draft dialog — client + billing period → POST generate-draft, with
// a live preview of the uninvoiced billable trips the draft would draw from.
// ---------------------------------------------------------------------------

function GenerateDraftModal({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: (id: string) => void;
}) {
  const [clients, setClients] = useState<ClientRecord[] | null>(null);
  const [clientId, setClientId] = useState("");
  const initial = useMemo(() => defaultBillingPeriod(null), []);
  const [start, setStart] = useState(initial.start);
  const [end, setEnd] = useState(initial.end);
  // Preview results keyed by the client+period they were fetched for, so a
  // changed selection simply stops matching (no synchronous state resets).
  const [preview, setPreview] = useState<{ key: string; rows: BillableTripRecord[] } | null>(null);
  const [previewError, setPreviewError] = useState<{ key: string; message: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    listClients().then(
      (rows) => {
        if (active) setClients([...rows].sort((a, b) => a.name.localeCompare(b.name)));
      },
      (e) => {
        if (active) setError(e instanceof ApiError ? e.message : "Failed to load the client roster.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const selected = clients?.find((c) => c.id === clientId) ?? null;

  function pickClient(id: string) {
    setClientId(id);
    setError(null);
    const c = clients?.find((x) => x.id === id);
    // Default the period from the client's contract billing frequency.
    const p = defaultBillingPeriod(c?.activeContract?.billingFrequency ?? null);
    setStart(p.start);
    setEnd(p.end);
  }

  // Preview the uninvoiced billable trips for the selection.
  const previewKey = clientId && start && end && end >= start ? `${clientId}|${start}|${end}` : null;

  useEffect(() => {
    if (!previewKey) return;
    let active = true;
    const [cid, from, to] = previewKey.split("|");
    listBillableTrips({ clientId: cid, uninvoiced: true, from, to }).then(
      (rows) => {
        if (active) setPreview({ key: previewKey, rows });
      },
      (e) => {
        if (active)
          setPreviewError({
            key: previewKey,
            message: e instanceof ApiError ? e.message : "Failed to load billable trips.",
          });
      },
    );
    return () => {
      active = false;
    };
  }, [previewKey]);

  const previewRows = preview !== null && preview.key === previewKey ? preview.rows : null;
  const previewErr = previewError !== null && previewError.key === previewKey ? previewError.message : null;

  async function submit() {
    if (busy) return;
    if (!clientId) return setError("Pick a client to invoice.");
    if (!start || !end) return setError("Enter the billing period start and end dates.");
    if (end < start) return setError("The period end date must be on or after the start date.");
    setBusy(true);
    setError(null);
    try {
      const id = await generateDraftInvoice(clientId, start, end);
      // Reads are eventually consistent — wait until the new draft is visible.
      await refetchUntil(
        () => getInvoice(id).then((d) => d, () => null),
        (d) => d !== null,
      );
      onCreated(id);
    } catch (e) {
      if (e instanceof ApiError) {
        if (e.code === "Billing.Invoice.NoActiveContract") {
          setError(`${e.message} Set up an active contract on the client profile first (Clients → Contract).`);
        } else if (e.code === "Billing.Invoice.NotRoundTripBilled") {
          setError(`${e.message}`);
        } else {
          setError(e.message);
        }
      } else {
        setError("Failed to generate the draft — please try again.");
      }
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Business · Billing"
      title="Generate Draft Invoice"
      onClose={onClose}
      error={error}
      maxWidth={640}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
            {busy ? "GENERATING…" : "GENERATE DRAFT"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <div style={{ gridColumn: "1 / -1" }}>
          <SelectField
            label={clients === null ? "Client (loading…)" : "Client"}
            value={clientId}
            onChange={pickClient}
            disabled={clients === null}
            options={[
              { value: "", label: clients === null ? "Loading clients…" : "Select a client…" },
              ...(clients ?? []).map((c) => ({ value: c.id, label: c.name })),
            ]}
            hint={
              selected?.activeContract ? (
                <span style={{ color: colors.textFaint }}>
                  — {selected.activeContract.billingFrequency.toLowerCase()} billing
                  {selected.activeContract.billingModel === "Manual" ? " · manual lines only" : ""}
                </span>
              ) : selected ? (
                <span style={{ color: statusMeta("soon").t }}>— no active contract</span>
              ) : undefined
            }
          />
        </div>
        <DateField label="Period start" value={start} onChange={setStart} />
        <DateField label="Period end" value={end} onChange={setEnd} />
      </div>

      {/* uninvoiced billable-trip preview for the selection */}
      <div style={{ marginTop: 18 }}>
        <SectionLabel>Uninvoiced billable trips in period</SectionLabel>
        {!previewKey && (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
            Pick a client to preview the completed trips the draft would pull.
          </div>
        )}
        {previewKey && previewErr && <StatusChip kind="over" label={`Preview unavailable — ${previewErr}`} />}
        {previewKey && !previewErr && previewRows === null && (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading billable trips…</div>
        )}
        {previewKey && previewRows !== null && previewRows.length === 0 && (
          <StatusChip kind="soon" label="No uninvoiced completed trips in this period" />
        )}
        {previewKey && previewRows !== null && previewRows.length > 0 && (
          <div style={{ border: `1px solid ${colors.borderSubtle}`, borderRadius: 9, overflow: "hidden" }}>
            {previewRows.slice(0, 8).map((t, i) => (
              <div
                key={t.id}
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  gap: 10,
                  padding: "8px 12px",
                  background: colors.cardBg,
                  borderTop: i === 0 ? "none" : `1px solid ${colors.borderSubtle}`,
                  fontFamily: fonts.body,
                  fontSize: 12,
                }}
              >
                <span style={{ fontFamily: fonts.mono, color: colors.skyBlue, flex: "none" }}>{t.tripNumber}</span>
                <span
                  style={{
                    color: colors.textSecondary,
                    minWidth: 0,
                    overflow: "hidden",
                    textOverflow: "ellipsis",
                    whiteSpace: "nowrap",
                    flex: 1,
                  }}
                >
                  {t.routeName}
                  {t.roundTripKey ? "" : " · unpaired leg"}
                </span>
                <span style={{ fontFamily: fonts.mono, color: colors.textDim, flex: "none" }}>{t.serviceDate}</span>
              </div>
            ))}
            {previewRows.length > 8 && (
              <div
                style={{
                  padding: "7px 12px",
                  borderTop: `1px solid ${colors.borderSubtle}`,
                  fontFamily: fonts.body,
                  fontSize: 11.5,
                  color: colors.textDim,
                  background: colors.cardBg,
                }}
              >
                + {previewRows.length - 8} more
              </div>
            )}
          </div>
        )}
      </div>
    </ModalShell>
  );
}

// ---------------------------------------------------------------------------
// Draft line editor — editable while status = Draft only. PUT /lines replaces
// the whole set; amounts are server-computed.
// ---------------------------------------------------------------------------

interface EditableLine {
  lineId: string | null;
  description: string;
  tripIds: string[];
  tripNumber: string | null;
  serviceDate: string | null;
  quantity: string;
  unitPriceCad: string;
}

function toEditable(l: InvoiceDetailRecord["lines"][number]): EditableLine {
  return {
    lineId: l.lineId,
    description: l.description,
    tripIds: l.tripIds,
    tripNumber: l.tripNumber,
    serviceDate: l.serviceDate,
    quantity: String(l.quantity),
    unitPriceCad: String(l.unitPriceCad),
  };
}

function LineEditor({
  inv,
  onClose,
  onSaved,
}: {
  inv: InvoiceDetailRecord;
  onClose: () => void;
  onSaved: (fresh: InvoiceDetailRecord) => void;
}) {
  const [lines, setLines] = useState<EditableLine[]>(inv.lines.map(toEditable));
  const [pool, setPool] = useState<BillableTripRecord[] | null>(null);
  const [poolError, setPoolError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Uninvoiced billable trips for this client — attachable as new lines.
  useEffect(() => {
    let active = true;
    listBillableTrips({ clientId: inv.clientId, uninvoiced: true }).then(
      (rows) => {
        if (active) setPool(rows);
      },
      (e) => {
        if (active) setPoolError(e instanceof ApiError ? e.message : "Failed to load billable trips.");
      },
    );
    return () => {
      active = false;
    };
  }, [inv.clientId]);

  function patch(i: number, part: Partial<EditableLine>) {
    setLines((prev) => prev.map((l, j) => (j === i ? { ...l, ...part } : l)));
  }

  function addManualLine() {
    setLines((prev) => [
      ...prev,
      { lineId: null, description: "", tripIds: [], tripNumber: null, serviceDate: null, quantity: "1", unitPriceCad: "" },
    ]);
  }

  function attachTrip(t: BillableTripRecord) {
    setLines((prev) => [
      ...prev,
      {
        lineId: null,
        description: `${t.routeName} — ${t.tripNumber}`,
        tripIds: [t.id],
        tripNumber: t.tripNumber,
        serviceDate: t.serviceDate,
        quantity: "1",
        unitPriceCad: "",
      },
    ]);
  }

  const attachedTripIds = new Set(lines.flatMap((l) => l.tripIds));
  const attachable = (pool ?? []).filter((t) => !attachedTripIds.has(t.id));

  async function save() {
    if (busy) return;
    const payload: InvoiceLineInput[] = [];
    for (const [i, l] of lines.entries()) {
      if (!l.description.trim()) return setError(`Line ${i + 1} needs a description.`);
      const qty = Number(l.quantity);
      if (Number.isNaN(qty) || qty <= 0) return setError(`Line ${i + 1}: quantity must be greater than zero.`);
      const price = Number(l.unitPriceCad);
      if (l.unitPriceCad === "" || Number.isNaN(price) || price < 0)
        return setError(`Line ${i + 1}: enter a unit price (CAD, zero or more).`);
      payload.push({
        lineId: l.lineId,
        description: l.description.trim(),
        tripIds: l.tripIds.length ? l.tripIds : null,
        tripNumber: l.tripNumber,
        serviceDate: l.serviceDate,
        quantity: qty,
        unitPriceCad: price,
      });
    }
    setBusy(true);
    setError(null);
    try {
      await replaceInvoiceLines(inv.id, payload);
      // Eventually consistent read — wait for the projection to show the new set.
      const expected = payload.reduce((s, l) => s + Math.round(l.quantity * l.unitPriceCad * 100) / 100, 0);
      const fresh = await refetchUntil(
        () => getInvoice(inv.id),
        (d) => d.lines.length === payload.length && Math.abs(d.subtotalCad - expected) < 0.005,
      );
      onSaved(fresh);
    } catch (e) {
      if (e instanceof ApiError && e.code === "Billing.Invoice.TripAlreadyInvoiced") {
        setError(`${e.message} Remove that line — a completed trip can only appear on one invoice.`);
      } else {
        setError(e instanceof ApiError ? e.message : "Failed to save the invoice lines — please try again.");
      }
      setBusy(false);
    }
  }

  return (
    <div
      style={{
        padding: "16px 18px",
        background: colors.cardBg,
        border: `1px solid ${colors.borderActive}`,
        borderRadius: 11,
        marginBottom: 12,
        boxShadow: colors.shadowCard,
      }}
    >
      <SectionLabel>Edit draft lines · amounts are computed server-side</SectionLabel>

      {error && (
        <div style={{ marginBottom: 12 }}>
          <StatusChip kind="over" label={error} />
        </div>
      )}

      {lines.length === 0 && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginBottom: 12 }}>
          No lines — add a manual line or attach a billable trip below. Saving with no lines empties the draft.
        </div>
      )}

      {lines.map((l, i) => {
        const qty = Number(l.quantity);
        const price = Number(l.unitPriceCad);
        const amount =
          !Number.isNaN(qty) && !Number.isNaN(price) && l.unitPriceCad !== ""
            ? formatInvoiceCad(Math.round(qty * price * 100) / 100)
            : "—";
        return (
          <div
            key={l.lineId ?? `new-${i}`}
            style={{
              display: "grid",
              gridTemplateColumns: "minmax(0,1fr) 84px 110px 92px 34px",
              gap: 9,
              alignItems: "end",
              marginBottom: 9,
            }}
          >
            <TextField
              label={i === 0 ? "Description" : `Line ${i + 1}`}
              value={l.description}
              onChange={(v) => patch(i, { description: v })}
              placeholder="Corridor round trip · Thompson → Lynn Lake"
              hint={
                l.tripNumber ? (
                  <span style={{ fontFamily: fonts.mono, color: colors.textFaint }}>
                    {l.tripNumber}
                    {l.serviceDate ? ` · ${l.serviceDate}` : ""}
                  </span>
                ) : undefined
              }
            />
            <NumberField label="Qty" value={l.quantity} onChange={(v) => patch(i, { quantity: v })} min={0} step={0.5} />
            <NumberField
              label="Unit (CAD)"
              value={l.unitPriceCad}
              onChange={(v) => patch(i, { unitPriceCad: v })}
              min={0}
              step={0.01}
              placeholder="128.00"
            />
            <div>
              <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textLabel, marginBottom: 5 }}>Amount</div>
              <div
                style={{
                  height: 40,
                  display: "flex",
                  alignItems: "center",
                  fontFamily: fonts.mono,
                  fontSize: 12,
                  color: colors.textSecondary,
                }}
              >
                {amount}
              </div>
            </div>
            <div
              onClick={() => setLines((prev) => prev.filter((_, j) => j !== i))}
              title="Remove line"
              style={{
                height: 40,
                borderRadius: 8,
                border: "1px solid rgba(213,94,0,.4)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                color: statusMeta("over").t,
                cursor: "pointer",
                fontSize: 14,
                fontWeight: 700,
              }}
            >
              ✕
            </div>
          </div>
        );
      })}

      <div style={{ display: "flex", gap: 9, marginTop: 4 }}>
        <ActionButton onClick={addManualLine}>+ ADD MANUAL LINE</ActionButton>
      </div>

      {/* attachable uninvoiced billable trips */}
      <div style={{ marginTop: 16 }}>
        <SectionLabel>Attach uninvoiced billable trips</SectionLabel>
        {poolError && <StatusChip kind="over" label={`Billable trips unavailable — ${poolError}`} />}
        {!poolError && pool === null && (
          <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Loading billable trips…</div>
        )}
        {pool !== null && attachable.length === 0 && (
          <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            No uninvoiced completed trips remain for {inv.clientName}.
          </div>
        )}
        {attachable.slice(0, 6).map((t) => (
          <div
            key={t.id}
            style={{
              display: "flex",
              alignItems: "center",
              gap: 10,
              padding: "7px 11px",
              marginBottom: 5,
              border: `1px solid ${colors.borderSubtle}`,
              borderRadius: 8,
              fontFamily: fonts.body,
              fontSize: 12,
            }}
          >
            <span style={{ fontFamily: fonts.mono, color: colors.skyBlue, flex: "none" }}>{t.tripNumber}</span>
            <span
              style={{
                color: colors.textSecondary,
                minWidth: 0,
                flex: 1,
                overflow: "hidden",
                textOverflow: "ellipsis",
                whiteSpace: "nowrap",
              }}
            >
              {t.routeName} · {t.serviceDate}
            </span>
            <ActionButton onClick={() => attachTrip(t)} style={{ padding: "4px 10px", fontSize: 12 }}>
              + ATTACH
            </ActionButton>
          </div>
        ))}
        {attachable.length > 6 && (
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            + {attachable.length - 6} more uninvoiced trips
          </div>
        )}
      </div>

      <div style={{ display: "flex", gap: 9, marginTop: 16 }}>
        <ActionButton variant="primary" onClick={save} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
          {busy ? "SAVING…" : "SAVE LINES"}
        </ActionButton>
        <ActionButton onClick={onClose}>CANCEL</ActionButton>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Detail pane
// ---------------------------------------------------------------------------

function InvoiceDetail({
  id,
  aging,
  onMutated,
}: {
  id: string;
  aging: ArAging;
  /** List-affecting change (status/QBO/lines) — parent refreshes the list. */
  onMutated: () => void;
}) {
  const [inv, setInv] = useState<InvoiceDetailRecord | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [confirm, setConfirm] = useState<"send" | "void" | "paid" | null>(null);

  // QBO reconcile control (records state only — QBO stays read-only).
  const [qboOpen, setQboOpen] = useState(false);
  const [qboId, setQboId] = useState("");
  const [qboStatus, setQboStatus] = useState<QboSyncStatus>("NotSynced");

  // The parent remounts this component per invoice (key={id}), so all state
  // resets on selection change without synchronous effect resets.
  const load = useCallback(async () => {
    try {
      const fresh = await getInvoice(id);
      setInv(fresh);
      setLoadError(null);
    } catch (e) {
      setInv(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load the invoice.");
    }
  }, [id]);

  useEffect(() => {
    let active = true;
    getInvoice(id).then(
      (fresh) => {
        if (active) {
          setInv(fresh);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setInv(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load the invoice.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [id]);

  function openQbo() {
    if (!inv) return;
    setQboId(inv.qboInvoiceId ?? "");
    setQboStatus(inv.qboSyncStatus);
    setQboOpen(true);
  }

  async function runStatusAction(action: "send" | "void" | "paid") {
    if (!inv || busy) return;
    const target = action === "send" ? "Sent" : action === "void" ? "Void" : "Paid";
    setBusy(true);
    setActionError(null);
    try {
      if (action === "send") await sendInvoice(inv.id);
      else if (action === "void") await voidInvoice(inv.id);
      else await markInvoicePaid(inv.id);
      const fresh = await refetchUntil(() => getInvoice(inv.id), (d) => d.status === target);
      setInv(fresh);
      setConfirm(null);
      onMutated();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "The action failed — please try again.");
      setConfirm(null);
    }
    setBusy(false);
  }

  async function applyQbo() {
    if (!inv || busy) return;
    setBusy(true);
    setActionError(null);
    const nextId = qboId.trim() || null;
    try {
      await setInvoiceQboStatus(inv.id, nextId, qboStatus);
      const fresh = await refetchUntil(
        () => getInvoice(inv.id),
        (d) => d.qboSyncStatus === qboStatus && d.qboInvoiceId === nextId,
      );
      setInv(fresh);
      setQboOpen(false);
      onMutated();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to record the QBO state — please try again.");
    }
    setBusy(false);
  }

  if (loadError) {
    return (
      <Panel borderColor="rgba(213,94,0,.4)">
        <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
          <StatusChip kind="over" label={`Invoice unavailable — ${loadError}`} />
          <ActionButton variant="primary" onClick={load}>
            RETRY
          </ActionButton>
        </div>
      </Panel>
    );
  }

  if (!inv) {
    return (
      <div>
        {[0, 1, 2].map((i) => (
          <div
            key={i}
            style={{
              height: 86,
              borderRadius: 11,
              border: `1px solid ${colors.borderSubtle}`,
              background: colors.cardBg,
              marginBottom: 12,
              opacity: 0.55 - i * 0.12,
            }}
          />
        ))}
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading invoice from API…</div>
      </div>
    );
  }

  const chip = invoiceChip(inv);
  const isDraft = inv.status === "Draft";
  const overdueDays = inv.isOverdue ? daysPastDue(inv.dueDate) : null;
  const unmatched = inv.qboSyncStatus === "UnmatchedPayment";
  const qm = statusMeta(qboKindFor(inv.qboSyncStatus));

  return (
    <div className="detailfade" key={inv.id}>
      {/* header row */}
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 14 }}>
        <StatusChip kind={chip.kind} label={chip.label} />
        <span style={{ fontFamily: fonts.mono, fontSize: 14, color: colors.skyBlue }}>{inv.invoiceNumber}</span>
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>{inv.clientName}</span>
        <span
          style={{
            marginLeft: "auto",
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 26,
            color: colors.headingBright,
            fontVariantNumeric: "tabular-nums",
          }}
        >
          {formatInvoiceCad(inv.totalCad)}
        </span>
      </div>

      {actionError && (
        <div style={{ marginBottom: 12 }}>
          <StatusChip kind="over" label={actionError} />
        </div>
      )}

      {/* alert banners — driven by real derived fields */}
      {inv.isOverdue && (
        <div
          style={{
            padding: "11px 14px",
            background: "rgba(213,94,0,.1)",
            border: "1px solid rgba(213,94,0,.4)",
            borderRadius: 9,
            marginBottom: 14,
            fontFamily: fonts.body,
            fontSize: 12.5,
            color: statusMeta("over").t,
            fontWeight: 600,
          }}
        >
          ▲ Overdue{overdueDays != null ? ` ${overdueDays}d past due` : ""} · net {inv.netTermsDays} terms
          {inv.dueDate ? ` · was due ${inv.dueDate}` : ""} · follow-up recommended
        </div>
      )}
      {unmatched && (
        <div
          style={{
            padding: "11px 14px",
            background: "rgba(225,176,0,.09)",
            border: "1px solid rgba(225,176,0,.3)",
            borderRadius: 9,
            marginBottom: 14,
            fontFamily: fonts.body,
            fontSize: 12.5,
            color: statusMeta("soon").t,
            fontWeight: 600,
          }}
        >
          ◐ Unmatched payment in QBO — reconcile manually, then record the match below (QBO stays read-only)
        </div>
      )}

      {/* line items */}
      {editing ? (
        <LineEditor
          inv={inv}
          onClose={() => setEditing(false)}
          onSaved={(fresh) => {
            setInv(fresh);
            setEditing(false);
            onMutated();
          }}
        />
      ) : (
        <div
          style={{
            padding: "16px 18px",
            background: colors.cardBg,
            border: `1px solid ${colors.border}`,
            borderRadius: 11,
            marginBottom: 12,
            boxShadow: colors.shadowCard,
          }}
        >
          <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between", marginBottom: 12 }}>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 9.5,
                letterSpacing: ".14em",
                textTransform: "uppercase",
                color: colors.textLabel,
              }}
            >
              Line items · billing period {periodLabel(inv)}
            </div>
            {isDraft && (
              <ActionButton onClick={() => setEditing(true)} style={{ padding: "4px 10px", fontSize: 12 }}>
                ✎ EDIT LINES
              </ActionButton>
            )}
          </div>

          {inv.lines.length === 0 && (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 0 10px" }}>
              No lines on this invoice{isDraft ? " yet — use EDIT LINES to add them" : ""}.
            </div>
          )}
          {inv.lines.map((l) => (
            <div
              key={l.lineId}
              style={{
                display: "flex",
                justifyContent: "space-between",
                gap: 12,
                padding: "9px 0",
                borderBottom: `1px solid ${colors.borderSubtle}`,
                fontFamily: fonts.body,
                fontSize: 12.5,
              }}
            >
              <span style={{ color: colors.textSecondary, minWidth: 0 }}>
                {l.description}
                {(l.tripNumber || l.serviceDate) && (
                  <span style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>
                    {"  "}
                    {l.tripNumber ?? ""}
                    {l.tripNumber && l.serviceDate ? " · " : ""}
                    {l.serviceDate ?? ""}
                  </span>
                )}
              </span>
              <span style={{ fontFamily: fonts.mono, color: colors.textDim, flex: "none" }}>
                {l.quantity} × {formatInvoiceCad(l.unitPriceCad)}
              </span>
              <span style={{ fontFamily: fonts.mono, color: colors.textPrimary, flex: "none", width: 92, textAlign: "right" }}>
                {formatInvoiceCad(l.amountCad)}
              </span>
            </div>
          ))}

          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              padding: "9px 0",
              borderBottom: `1px solid ${colors.borderSubtle}`,
              fontFamily: fonts.body,
              fontSize: 12.5,
            }}
          >
            <span style={{ color: colors.textSecondary }}>Subtotal</span>
            <span style={{ fontFamily: fonts.mono, color: colors.textPrimary }}>{formatInvoiceCad(inv.subtotalCad)}</span>
          </div>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              padding: "9px 0",
              borderBottom: `1px solid ${colors.borderSubtle}`,
              fontFamily: fonts.body,
              fontSize: 12.5,
            }}
          >
            <span style={{ color: colors.textSecondary }}>
              {inv.gstApplicable
                ? `GST (${Math.round(inv.gstRate * 1000) / 10}%) · no PST on transportation`
                : "GST — not applicable per contract"}
            </span>
            <span style={{ fontFamily: fonts.mono, color: colors.textPrimary }}>{formatInvoiceCad(inv.gstCad)}</span>
          </div>
          <div style={{ display: "flex", justifyContent: "space-between", padding: "11px 0 0", fontFamily: fonts.body, fontSize: 13, fontWeight: 700 }}>
            <span style={{ color: colors.textPrimary }}>Total (CAD)</span>
            <span style={{ fontFamily: fonts.mono, color: statusMeta("ontime").t }}>{formatInvoiceCad(inv.totalCad)}</span>
          </div>
        </div>
      )}

      {/* PO / budget / terms cards — snapshots stored on the invoice */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12, marginBottom: 12 }}>
        <MiniCard label="PO match" value={inv.poNumber ?? "—"} />
        <MiniCard label="Budget code (ZBB)" value={inv.budgetCode ?? "—"} />
        <MiniCard
          label="Terms"
          value={`Net ${inv.netTermsDays}${inv.dueDate ? ` · due ${inv.dueDate}` : ""}`}
        />
      </div>

      {/* QBO sync card — reconciliation state only, no write path */}
      <div style={{ ...cardStyle, marginBottom: 12 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>
              QBO sync · read-only book of record
            </div>
            <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}>
              <StatusChip kind={qboKindFor(inv.qboSyncStatus)} label={QBO_LABELS[inv.qboSyncStatus]} />
              <span style={{ fontFamily: fonts.mono, fontSize: 11, color: qm.t }}>{inv.qboInvoiceId ?? "no QBO id"}</span>
            </div>
          </div>
          {!qboOpen && (
            <ActionButton onClick={openQbo} style={{ padding: "4px 10px", fontSize: 12 }}>
              RECORD RECONCILIATION
            </ActionButton>
          )}
        </div>
        {qboOpen && (
          <div style={{ marginTop: 12, display: "grid", gridTemplateColumns: "1fr 1fr auto auto", gap: 9, alignItems: "end" }}>
            <TextField label="QBO invoice id" value={qboId} onChange={setQboId} mono placeholder="QBO-10422" />
            <SelectField
              label="Reconciliation state"
              value={qboStatus}
              onChange={(v) => setQboStatus(v as QboSyncStatus)}
              options={(Object.keys(QBO_LABELS) as QboSyncStatus[]).map((s) => ({ value: s, label: QBO_LABELS[s] }))}
            />
            <ActionButton variant="primary" onClick={applyQbo} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
              {busy ? "SAVING…" : "APPLY"}
            </ActionButton>
            <ActionButton onClick={() => setQboOpen(false)}>CANCEL</ActionButton>
          </div>
        )}
      </div>

      {/* AR aging — computed client-side from the live invoice list */}
      <div style={{ padding: "15px 16px", background: colors.cardBg, border: `1px solid ${colors.border}`, borderRadius: 11, marginBottom: 16, boxShadow: colors.shadowCard }}>
        <SectionLabel>AR aging · outstanding (Sent) invoices by days past due</SectionLabel>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(3,1fr)", gap: 12 }}>
          <div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: statusMeta("ontime").t, fontVariantNumeric: "tabular-nums" }}>
              {formatInvoiceCad(aging.current)}
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>Current · 0–30</div>
          </div>
          <div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: statusMeta("soon").t, fontVariantNumeric: "tabular-nums" }}>
              {formatInvoiceCad(aging.days31to60)}
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>31–60</div>
          </div>
          <div>
            <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 20, color: statusMeta("over").t, fontVariantNumeric: "tabular-nums" }}>
              {formatInvoiceCad(aging.days61plus)}
            </div>
            <div style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textDim }}>61+</div>
          </div>
        </div>
      </div>

      {/* actions */}
      <div style={{ display: "flex", gap: 9, flexWrap: "wrap" }}>
        {isDraft && (
          <>
            <ActionButton variant="primary" onClick={() => setConfirm("send")}>
              REVIEW &amp; SEND
            </ActionButton>
            <ActionButton variant="destructive" onClick={() => setConfirm("void")}>
              VOID DRAFT
            </ActionButton>
          </>
        )}
        {inv.status === "Sent" && (
          <ActionButton variant="success" onClick={() => setConfirm("paid")}>
            ✓ MARK PAID
          </ActionButton>
        )}
        {/* TODO(billing): PDF export & QBO deep link — placeholders until a
            document pipeline / QBO id-to-URL mapping exists. Disabled on
            purpose; no backend endpoint yet. */}
        <ActionButton style={{ opacity: 0.45, cursor: "not-allowed" }}>EXPORT PDF</ActionButton>
        <ActionButton style={{ opacity: 0.45, cursor: "not-allowed" }}>VIEW IN QBO</ActionButton>
      </div>

      {confirm === "send" && (
        <ConfirmModal
          eyebrow={`Billing · ${inv.invoiceNumber}`}
          title="Send invoice?"
          body={`Send ${inv.invoiceNumber} (${formatInvoiceCad(inv.totalCad)}) to ${inv.clientName}? Lines lock once sent — net ${inv.netTermsDays} terms start today.`}
          confirmLabel="SEND INVOICE"
          busy={busy}
          onConfirm={() => runStatusAction("send")}
          onClose={() => setConfirm(null)}
        />
      )}
      {confirm === "void" && (
        <ConfirmModal
          eyebrow={`Billing · ${inv.invoiceNumber}`}
          title="Void draft?"
          body={`Void draft ${inv.invoiceNumber}? Its trips are released back to the uninvoiced pool and can be pulled onto a new draft.`}
          confirmLabel="VOID DRAFT"
          destructive
          busy={busy}
          onConfirm={() => runStatusAction("void")}
          onClose={() => setConfirm(null)}
        />
      )}
      {confirm === "paid" && (
        <ConfirmModal
          eyebrow={`Billing · ${inv.invoiceNumber}`}
          title="Mark paid?"
          body={`Record ${inv.invoiceNumber} (${formatInvoiceCad(inv.totalCad)}) as paid by ${inv.clientName}?`}
          confirmLabel="MARK PAID"
          busy={busy}
          onConfirm={() => runStatusAction("paid")}
          onClose={() => setConfirm(null)}
        />
      )}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------

export default function Billing({
  invoiceSelId,
  setInvoiceSelId,
}: {
  invoiceSelId: string | null;
  setInvoiceSelId: (id: string | null) => void;
}) {
  const [rows, setRows] = useState<InvoiceSummaryRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [generateOpen, setGenerateOpen] = useState(false);

  const load = useCallback(async () => {
    try {
      const fresh = await listInvoices();
      setRows(sortInvoices(fresh));
      setLoadError(null);
    } catch (e) {
      setRows(null);
      setLoadError(e instanceof ApiError ? e.message : "Failed to load invoices.");
    }
  }, []);

  useEffect(() => {
    let active = true;
    listInvoices().then(
      (fresh) => {
        if (active) {
          setRows(sortInvoices(fresh));
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setRows(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load invoices.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  // Keep a valid selection: default to the first row, drop stale ids.
  useEffect(() => {
    if (rows === null) return;
    if (invoiceSelId && rows.some((r) => r.id === invoiceSelId)) return;
    setInvoiceSelId(rows.length > 0 ? rows[0].id : null);
  }, [rows, invoiceSelId, setInvoiceSelId]);

  const aging = useMemo(() => arAgingFor(rows ?? []), [rows]);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
          <PageHeader eyebrow="Business · Invoicing & QBO reconciliation" title="Billing & Invoicing" />
          <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                padding: "7px 13px",
                borderRadius: 9,
                background: "rgba(31,111,178,.09)",
                border: "1px solid rgba(31,111,178,.3)",
              }}
            >
              <span style={{ width: 8, height: 8, borderRadius: "50%", background: "#009E73" }} />
              <div style={{ lineHeight: 1.3 }}>
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, fontWeight: 600, color: colors.skyBlue }}>
                  QuickBooks Online · read-only book of record
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 9.5, color: colors.textDim }}>
                  Reconciliation recorded here · no write path
                </div>
              </div>
            </div>
            <ActionButton variant="primary" onClick={() => setGenerateOpen(true)}>
              + GENERATE DRAFT
            </ActionButton>
          </div>
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, display: "grid", gridTemplateColumns: "44% 1fr", borderTop: `1px solid ${colors.border}` }}>
        {/* master list */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "16px 18px", borderRight: `1px solid ${colors.border}` }}>
          {loadError && (
            <Panel borderColor="rgba(213,94,0,.4)">
              <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <StatusChip kind="over" label={`Invoices unavailable — ${loadError}`} />
                <ActionButton variant="primary" onClick={load}>
                  RETRY
                </ActionButton>
              </div>
            </Panel>
          )}

          {rows === null && !loadError && (
            <div>
              {[0, 1, 2, 3, 4].map((i) => (
                <div
                  key={i}
                  style={{
                    height: 56,
                    borderRadius: 9,
                    border: `1px solid ${colors.borderSubtle}`,
                    background: colors.cardBg,
                    marginBottom: 5,
                    opacity: 0.55 - i * 0.09,
                  }}
                />
              ))}
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
                Loading invoices from API…
              </div>
            </div>
          )}

          {rows !== null && rows.length === 0 && (
            <Panel>
              <SectionLabel>No invoices yet</SectionLabel>
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6 }}>
                Generate a draft to pull a client&rsquo;s completed, uninvoiced round trips for a billing period at the
                contract rate.
              </div>
              <div style={{ marginTop: 12 }}>
                <ActionButton variant="primary" onClick={() => setGenerateOpen(true)}>
                  + GENERATE DRAFT
                </ActionButton>
              </div>
            </Panel>
          )}

          {rows !== null && rows.length > 0 && (
            <>
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "96px 1fr 96px 128px",
                  gap: 11,
                  padding: "0 13px 9px",
                  fontFamily: fonts.semiCondensed,
                  fontSize: 9.5,
                  letterSpacing: ".12em",
                  textTransform: "uppercase",
                  color: colors.textFaint,
                }}
              >
                <div>Invoice</div>
                <div>Client / PO</div>
                <div>Total</div>
                <div>Status</div>
              </div>
              {rows.map((row) => {
                const active = row.id === invoiceSelId;
                const chip = invoiceChip(row);
                const qm = statusMeta(qboKindFor(row.qboSyncStatus));
                return (
                  <div
                    key={row.id}
                    onClick={() => setInvoiceSelId(row.id)}
                    style={{
                      display: "grid",
                      gridTemplateColumns: "96px 1fr 96px 128px",
                      gap: 11,
                      alignItems: "center",
                      padding: "11px 13px",
                      marginBottom: 5,
                      ...rowSurface(active, colors.blue),
                    }}
                  >
                    <div>
                      <div style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{row.invoiceNumber}</div>
                      <div
                        style={{
                          fontFamily: fonts.semiCondensed,
                          fontSize: 10,
                          letterSpacing: ".05em",
                          textTransform: "uppercase",
                          color: qm.t,
                        }}
                      >
                        {QBO_LABELS[row.qboSyncStatus]}
                      </div>
                    </div>
                    <div style={{ minWidth: 0 }}>
                      <div
                        style={{
                          fontFamily: fonts.body,
                          fontSize: 12.5,
                          fontWeight: 600,
                          color: colors.textPrimary,
                          whiteSpace: "nowrap",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                        }}
                      >
                        {row.clientName}
                      </div>
                      <div style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>{row.poNumber ?? "—"}</div>
                    </div>
                    <div style={{ fontFamily: fonts.mono, fontSize: 12.5, color: colors.textPrimary, fontWeight: 500 }}>
                      {formatInvoiceCad(row.totalCad)}
                    </div>
                    <div>
                      <StatusChip kind={chip.kind} label={chip.label} />
                      <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 3 }}>
                        {invoiceAgeLabel(row)}
                      </div>
                    </div>
                  </div>
                );
              })}
            </>
          )}
        </div>

        {/* detail */}
        <div style={{ minHeight: 0, overflowY: "auto", padding: "22px 26px", background: colors.detailBg }}>
          {invoiceSelId ? (
            <InvoiceDetail key={invoiceSelId} id={invoiceSelId} aging={aging} onMutated={load} />
          ) : (
            rows !== null &&
            !loadError && (
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
                Select an invoice to see its lines, QBO reconciliation, and actions.
              </div>
            )
          )}
        </div>
      </div>

      {generateOpen && (
        <GenerateDraftModal
          onClose={() => setGenerateOpen(false)}
          onCreated={(id) => {
            setGenerateOpen(false);
            setInvoiceSelId(id);
            void load();
          }}
        />
      )}
    </div>
  );
}
