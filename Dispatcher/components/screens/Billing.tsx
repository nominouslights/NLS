"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { colors, fonts, rowSurface, statusMeta, type StatusKind } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  defaultBillingPeriod,
  directionMeta,
  formatInvoiceCad,
  generateDraftInvoice,
  getInvoice,
  invoiceChip,
  invoiceClipboardText,
  lineClipboardText,
  listBillableTrips,
  listInvoices,
  markInvoiceEntered,
  periodLabel,
  refetchUntil,
  clearInvoicePayment,
  confirmInvoicePayment,
  replaceInvoiceLines,
  sortInvoices,
  updateQboReference,
  voidInvoice,
  writeOffInvoice,
  type BillableTripRecord,
  type InvoiceDetailRecord,
  type InvoiceLineInput,
  type InvoiceSummaryRecord,
} from "@/lib/api/billing";
import { copyToClipboard } from "@/lib/clipboard";
import { printInvoicePrepSheet } from "@/lib/documents/invoicePdf";
import type { LoadedTrip, TripDetailMap } from "@/lib/billing/tripDetail";
import { getTrip, getTripManifest, shortDateLabel, type ManifestPassenger } from "@/lib/api/trips";
import { getClient, listClients, type ClientRecord } from "@/lib/api/clients";
import { PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusBadge, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";

// Billing Worksheets — prepares invoices for MANUAL entry into QuickBooks
// Online. Draft generation pulls uninvoiced completed round trips at the
// contract rate; lines are editable while Draft only. QuickBooks is never
// called via API: the user copies/prints the numbers, keys them into QBO, then
// records the resulting QBO invoice number + entered date back here
// (mark-entered). There is no send / paid / overdue / AR-aging concept —
// netTermsDays is purely informational.

const cardStyle = {
  padding: "13px 15px",
  background: colors.cardBg,
  border: `1px solid ${colors.border}`,
  borderRadius: 10,
  boxShadow: colors.shadowCard,
} as const;

function todayIso(): string {
  const d = new Date();
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

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

/** Small inline leg-direction tag — glyph + text label (never colour-only).
 *  Renders nothing for an unclassified (null) direction. */
function DirectionTag({ direction }: { direction: BillableTripRecord["direction"] }) {
  const d = directionMeta(direction);
  if (!d) return null;
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 3,
        flex: "none",
        padding: "1px 6px",
        borderRadius: 5,
        border: `1px solid ${colors.borderStrong}`,
        fontFamily: fonts.semiCondensed,
        fontSize: 9.5,
        letterSpacing: ".06em",
        textTransform: "uppercase",
        color: colors.textDim,
      }}
      title={`${d.label} leg`}
    >
      <span aria-hidden style={{ fontSize: 11, lineHeight: 1 }}>
        {d.glyph}
      </span>
      {d.label}
    </span>
  );
}

// ---------------------------------------------------------------------------
// Live trip + passenger detail (InvoiceDetail) — read from the Trips API, not
// snapshotted onto the invoice. One entry per trip id referenced by a line.
// The LoadedTrip shape and the derivations over it live in
// lib/billing/tripDetail, shared with the clipboard exports and the printed
// prep sheet so all three describe a trip identically.
// ---------------------------------------------------------------------------

/** Boarded on/off indicator — colour + glyph + text label per the accessible
 *  status-colour rule. Confirmed = teal ✓; pending renders in pendingKind
 *  (gold for on-boarding, neutral gray for off-boarding). */
function BoardBadge({ on, label, pendingKind }: { on: boolean; label: string; pendingKind: StatusKind }) {
  const kind: StatusKind = on ? "ontime" : pendingKind;
  const m = statusMeta(kind);
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        fontFamily: fonts.body,
        fontSize: 10.5,
        color: m.t,
        whiteSpace: "nowrap",
      }}
    >
      <StatusBadge kind={kind} size={13} />
      {label}
    </span>
  );
}

function PassengerRow({ p }: { p: ManifestPassenger }) {
  const leg = [p.pickupStopName, p.dropoffStopName].filter(Boolean).join(" → ");
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 10,
        padding: "5px 0",
        borderTop: `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={{ minWidth: 0 }}>
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 12,
            color: colors.textSecondary,
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {p.name}
        </div>
        {leg && (
          <div
            style={{
              fontFamily: fonts.mono,
              fontSize: 10,
              color: colors.textDim,
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
            }}
          >
            {leg}
          </div>
        )}
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 10, flex: "none" }}>
        <BoardBadge on={p.boardedOn} pendingKind="soon" label={p.boardedOn ? "On ✓" : "Not on"} />
        <BoardBadge on={p.boardedOff} pendingKind="off" label={p.boardedOff ? "Off ✓" : "Not off"} />
      </div>
    </div>
  );
}

/** One trip's live block under an invoice line: number, service date, direction,
 *  and its passengers with on/off indicators. Handles the loading / trip-failed
 *  / manifest-failed / no-manifest / empty-manifest cases. */
function TripDetailBlock({ tripId, loaded }: { tripId: string; loaded: LoadedTrip | undefined }) {
  const shell = {
    padding: "8px 11px",
    marginTop: 6,
    background: colors.detailBg,
    border: `1px solid ${colors.borderSubtle}`,
    borderRadius: 8,
  } as const;

  if (!loaded) {
    return (
      <div style={{ ...shell, fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        Loading trip &amp; passengers…
      </div>
    );
  }

  if (loaded.error || !loaded.trip) {
    return (
      <div style={{ ...shell }}>
        <StatusChip kind="off" label="Trip details unavailable" />
      </div>
    );
  }

  const t = loaded.trip;
  const passengers = loaded.manifest?.passengers ?? [];

  return (
    <div style={shell}>
      <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", marginBottom: passengers.length ? 4 : 0 }}>
        <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.skyBlue }}>{t.tripNumber}</span>
        <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>{shortDateLabel(t.serviceDate)}</span>
        <DirectionTag direction={t.direction} />
        {passengers.length > 0 && (
          <span style={{ fontFamily: fonts.body, fontSize: 10.5, color: colors.textFaint, marginLeft: "auto" }}>
            {passengers.length} pax
          </span>
        )}
      </div>
      {loaded.manifestError ? (
        <StatusChip kind="soon" label="Passenger details unavailable" />
      ) : !loaded.trip.manifestId ? (
        <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>No manifest recorded for this trip.</div>
      ) : passengers.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>Manifest has no passengers.</div>
      ) : (
        <div>
          {passengers.map((p, i) => (
            <PassengerRow key={`${tripId}-${i}`} p={p} />
          ))}
        </div>
      )}
    </div>
  );
}

/**
 * One side of the receivable split. The status kind pairs a colour with the StatusChip's
 * glyph and a text label, so the outstanding/paid distinction never rests on colour alone.
 */
function ReceivableTile({
  label,
  kind,
  count,
  total,
}: {
  label: string;
  kind: StatusKind;
  count: number;
  total: number;
}) {
  return (
    <div
      style={{
        flex: "1 1 180px",
        padding: "11px 14px",
        background: colors.cardBg,
        border: `1px solid ${colors.border}`,
        borderRadius: 11,
        boxShadow: colors.shadowCard,
      }}
    >
      <StatusChip kind={kind} label={`${label} · ${count} invoice${count === 1 ? "" : "s"}`} />
      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 22,
          color: colors.headingBright,
          fontVariantNumeric: "tabular-nums",
          marginTop: 7,
        }}
      >
        {formatInvoiceCad(total)}
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
            disabled={busy}
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
// Mark-entered / edit-reference modal — records the QuickBooks invoice number
// and entered date the user keyed by hand. "enter" (Draft → EnteredInQbo) uses
// mark-entered; "edit" (correct an already-entered reference) uses qbo-reference.
// ---------------------------------------------------------------------------

/**
 * Records that payment against the QBO invoice was received. Manual entry, exactly like the
 * QBO reference above it — the platform never calls the QuickBooks API; this only lets the
 * console answer outstanding-vs-paid without opening QBO.
 */
function ConfirmPaymentModal({
  inv,
  onClose,
  onDone,
}: {
  inv: InvoiceDetailRecord;
  onClose: () => void;
  onDone: (fresh: InvoiceDetailRecord) => void;
}) {
  const [confirmedDate, setConfirmedDate] = useState(todayIso());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!confirmedDate) return setError("Enter the date the payment was received.");
    setBusy(true);
    setError(null);
    try {
      await confirmInvoicePayment(inv.id, confirmedDate);
      // Eventually consistent read — wait until the projection shows it settled.
      const fresh = await refetchUntil(() => getInvoice(inv.id), (d) => d.status === "Paid");
      onDone(fresh);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to record the payment — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Billing · ${inv.invoiceNumber}`}
      title="Confirm payment received"
      onClose={onClose}
      error={error}
      maxWidth={480}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="success" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : "CONFIRM PAYMENT"}
          </ActionButton>
        </>
      }
    >
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: 12.5,
          color: colors.textSecondary,
          lineHeight: 1.6,
          marginBottom: 14,
        }}
      >
        Confirm in QuickBooks that payment for {inv.invoiceNumber} ({formatInvoiceCad(inv.totalCad)}
        {inv.qboInvoiceId ? `, QBO ${inv.qboInvoiceId}` : ""}) has been received, then record the date here. This
        marks the invoice paid in the console and shows every trip on it as paid — it changes nothing in QuickBooks.
      </div>
      <DateField label="Payment received" value={confirmedDate} onChange={setConfirmedDate} />
    </ModalShell>
  );
}

/**
 * Writes the invoice off (EnteredInQbo → WrittenOff): the money is recorded as
 * lost, with an amount (defaults to the full total), effective date, and a
 * required reason. Like every QBO-adjacent action here, it changes nothing in
 * QuickBooks — the matching write-off must be keyed there by hand.
 */
function WriteOffModal({
  inv,
  onClose,
  onDone,
}: {
  inv: InvoiceDetailRecord;
  onClose: () => void;
  onDone: (fresh: InvoiceDetailRecord) => void;
}) {
  const [amount, setAmount] = useState(String(inv.totalCad));
  const [writtenOffDate, setWrittenOffDate] = useState(todayIso());
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    const amt = Number(amount);
    if (amount === "" || Number.isNaN(amt) || amt <= 0)
      return setError("Enter the amount being written off (CAD, greater than zero).");
    if (!writtenOffDate) return setError("Enter the write-off date.");
    const trimmed = reason.trim();
    if (!trimmed) return setError("A reason is required — it is recorded on the written-off invoice.");
    setBusy(true);
    setError(null);
    try {
      await writeOffInvoice(inv.id, { amountCad: amt, writtenOffDate, reason: trimmed });
      // Eventually consistent read — wait until the projection shows it written off.
      const fresh = await refetchUntil(() => getInvoice(inv.id), (d) => d.status === "WrittenOff");
      onDone(fresh);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to write the invoice off — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Billing · ${inv.invoiceNumber}`}
      title="Write off invoice"
      onClose={onClose}
      error={error}
      maxWidth={480}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="destructive" onClick={submit} disabled={busy}>
            {busy ? "WRITING OFF…" : "WRITE OFF"}
          </ActionButton>
        </>
      }
    >
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: 12.5,
          color: colors.textSecondary,
          lineHeight: 1.6,
          marginBottom: 14,
        }}
      >
        Write off {inv.invoiceNumber} ({formatInvoiceCad(inv.totalCad)}
        {inv.qboInvoiceId ? `, QBO ${inv.qboInvoiceId}` : ""})? The invoice and the trips it claims are recorded as a
        loss — this cannot be re-invoiced, and nothing changes in QuickBooks.
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, marginBottom: 12 }}>
        <NumberField label="Amount written off (CAD)" value={amount} onChange={setAmount} min={0} step={0.01} />
        <DateField label="Write-off date" value={writtenOffDate} onChange={setWrittenOffDate} />
      </div>
      <TextAreaField
        label="Reason (required — recorded on the invoice)"
        value={reason}
        onChange={setReason}
        rows={3}
        placeholder="Client insolvent · balance uncollectable"
      />
    </ModalShell>
  );
}

function MarkEnteredModal({
  inv,
  mode,
  onClose,
  onDone,
}: {
  inv: InvoiceDetailRecord;
  mode: "enter" | "edit";
  onClose: () => void;
  onDone: (fresh: InvoiceDetailRecord) => void;
}) {
  const [qboNumber, setQboNumber] = useState(inv.qboInvoiceId ?? "");
  const [enteredDate, setEnteredDate] = useState(inv.qboEnteredDate ?? todayIso());
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    const num = qboNumber.trim();
    if (!num) return setError("Enter the QuickBooks invoice number you keyed.");
    if (!enteredDate) return setError("Enter the date you entered it in QuickBooks.");
    setBusy(true);
    setError(null);
    try {
      if (mode === "edit") await updateQboReference(inv.id, num, enteredDate);
      else await markInvoiceEntered(inv.id, num, enteredDate);
      // Eventually consistent read — wait until the recorded reference is visible.
      const fresh = await refetchUntil(
        () => getInvoice(inv.id),
        (d) => d.status === "EnteredInQbo" && d.qboInvoiceId === num,
      );
      onDone(fresh);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to record the QuickBooks reference — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Billing · ${inv.invoiceNumber}`}
      title={mode === "edit" ? "Edit QuickBooks reference" : "Mark entered in QuickBooks"}
      onClose={onClose}
      error={error}
      maxWidth={480}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : mode === "edit" ? "SAVE REFERENCE" : "MARK ENTERED"}
          </ActionButton>
        </>
      }
    >
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.6, marginBottom: 14 }}>
        {mode === "edit"
          ? `Correct the QuickBooks reference recorded for ${inv.invoiceNumber}.`
          : `Key ${inv.invoiceNumber} (${formatInvoiceCad(inv.totalCad)}) into QuickBooks Online first — COPY FOR QUICKBOOKS and PRINT PREP SHEET both carry the lines plus the trips and passengers behind them — then record its QBO invoice number and entered date here.`}
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
        <TextField label="QBO invoice number" value={qboNumber} onChange={setQboNumber} mono placeholder="1042" />
        <DateField label="Entered date" value={enteredDate} onChange={setEnteredDate} />
      </div>
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
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
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
                <DirectionTag direction={t.direction} />
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

// Stable client-side line id so line selection survives reordering/removal
// (a new line has no server lineId yet, and indices shift as lines change).
let lineUidSeq = 0;
function newLineUid(): string {
  return `l${++lineUidSeq}`;
}

interface EditableLine {
  uid: string;
  lineId: string | null;
  description: string;
  tripIds: string[];
  tripNumber: string | null;
  serviceDate: string | null;
  /** Display labels (trip numbers) for the trips this line prices — one for a
   *  single leg, several once legs are combined into a round-trip line. */
  tripLabels: string[];
  quantity: string;
  unitPriceCad: string;
}

function toEditable(l: InvoiceDetailRecord["lines"][number]): EditableLine {
  return {
    uid: newLineUid(),
    lineId: l.lineId,
    description: l.description,
    tripIds: l.tripIds,
    tripNumber: l.tripNumber,
    serviceDate: l.serviceDate,
    tripLabels: l.tripNumber ? [l.tripNumber] : [],
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
  // Line uids ticked for combining into a single round-trip line.
  const [selected, setSelected] = useState<Set<string>>(new Set());
  // Contract round-trip rate — pre-fills the price when combining legs (null
  // when unavailable or not a RoundTripRate contract; the user then types it).
  const [roundTripRate, setRoundTripRate] = useState<number | null>(null);

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

  // Best-effort contract round-trip rate for pre-filling combined lines.
  useEffect(() => {
    let active = true;
    getClient(inv.clientId).then(
      (c) => {
        if (active && c.activeContract?.billingModel === "RoundTripRate") {
          setRoundTripRate(c.activeContract.ratePerRoundTripCad);
        }
      },
      () => {
        /* rate stays null — the user enters the price */
      },
    );
    return () => {
      active = false;
    };
  }, [inv.clientId]);

  const poolById = useMemo(() => new Map((pool ?? []).map((t) => [t.id, t])), [pool]);

  function patch(i: number, part: Partial<EditableLine>) {
    setLines((prev) => prev.map((l, j) => (j === i ? { ...l, ...part } : l)));
  }

  function addManualLine() {
    setLines((prev) => [
      ...prev,
      {
        uid: newLineUid(),
        lineId: null,
        description: "",
        tripIds: [],
        tripNumber: null,
        serviceDate: null,
        tripLabels: [],
        quantity: "1",
        unitPriceCad: "",
      },
    ]);
  }

  function attachTrip(t: BillableTripRecord) {
    setLines((prev) => [
      ...prev,
      {
        uid: newLineUid(),
        lineId: null,
        description: `${t.routeName} — ${t.tripNumber}`,
        tripIds: [t.id],
        tripNumber: t.tripNumber,
        serviceDate: t.serviceDate,
        tripLabels: [t.tripNumber],
        quantity: "1",
        unitPriceCad: "",
      },
    ]);
  }

  function removeLine(uid: string) {
    setLines((prev) => prev.filter((l) => l.uid !== uid));
    setSelected((prev) => {
      if (!prev.has(uid)) return prev;
      const next = new Set(prev);
      next.delete(uid);
      return next;
    });
  }

  function toggleSelect(uid: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(uid)) next.delete(uid);
      else next.add(uid);
      return next;
    });
  }

  // Combine the ticked lines (≥2) into one round-trip line: unions their trips,
  // prices once at the contract round-trip rate (falling back to the richest
  // merged leg price), and drops the originals. The net trip set is unchanged,
  // so trip claims are untouched when the draft is saved.
  function combineSelected() {
    const chosen = lines.filter((l) => selected.has(l.uid));
    if (chosen.length < 2) return;
    const tripIds = Array.from(new Set(chosen.flatMap((l) => l.tripIds)));
    const tripLabels = Array.from(
      new Set([
        ...chosen.flatMap((l) => l.tripLabels),
        ...tripIds.map((id) => poolById.get(id)?.tripNumber).filter((n): n is string => !!n),
      ]),
    );
    const routes = Array.from(
      new Set(tripIds.map((id) => poolById.get(id)?.routeName).filter((r): r is string => !!r)),
    );
    const description = routes.length === 1 ? `Round trip · ${routes[0]}` : "Round trip";
    const mergedPrices = chosen
      .map((l) => Number(l.unitPriceCad))
      .filter((n) => !Number.isNaN(n) && n > 0);
    const price = roundTripRate ?? (mergedPrices.length ? Math.max(...mergedPrices) : NaN);
    const serviceDate =
      chosen.map((l) => l.serviceDate).filter((d): d is string => !!d).sort()[0] ?? null;
    const combined: EditableLine = {
      uid: newLineUid(),
      lineId: null,
      description,
      tripIds,
      tripNumber: null,
      serviceDate,
      tripLabels,
      quantity: "1",
      unitPriceCad: Number.isNaN(price) ? "" : String(price),
    };
    setLines((prev) => [...prev.filter((l) => !selected.has(l.uid)), combined]);
    setSelected(new Set());
  }

  const attachedTripIds = new Set(lines.flatMap((l) => l.tripIds));
  const attachable = (pool ?? []).filter((t) => !attachedTripIds.has(t.id));
  const selectedCount = lines.reduce((n, l) => (selected.has(l.uid) ? n + 1 : n), 0);

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
        const isSel = selected.has(l.uid);
        const isRoundTrip = l.tripIds.length > 1;
        return (
          <div
            key={l.uid}
            style={{
              display: "grid",
              gridTemplateColumns: "26px minmax(0,1fr) 84px 110px 92px 34px",
              gap: 9,
              alignItems: "end",
              marginBottom: 9,
            }}
          >
            <div
              onClick={() => toggleSelect(l.uid)}
              title={isSel ? "Deselect line" : "Select to combine into a round trip"}
              style={{ height: 40, display: "flex", alignItems: "center", justifyContent: "center", cursor: "pointer" }}
            >
              <span
                aria-hidden
                style={{
                  width: 16,
                  height: 16,
                  borderRadius: 4,
                  border: `1.5px solid ${isSel ? statusMeta("info").t : colors.borderStrong}`,
                  background: isSel ? statusMeta("info").t : "transparent",
                  color: "#fff",
                  fontSize: 11,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                }}
              >
                {isSel ? "✓" : ""}
              </span>
            </div>
            <TextField
              label={i === 0 ? "Description" : `Line ${i + 1}`}
              value={l.description}
              onChange={(v) => patch(i, { description: v })}
              placeholder="Corridor round trip · Thompson → Lynn Lake"
              hint={
                l.tripLabels.length > 0 ? (
                  <span style={{ fontFamily: fonts.mono, color: colors.textFaint }}>
                    {l.tripLabels.join(" + ")}
                    {isRoundTrip ? " · round trip" : l.serviceDate ? ` · ${l.serviceDate}` : ""}
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
              onClick={() => removeLine(l.uid)}
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

      <div style={{ display: "flex", gap: 9, marginTop: 4, alignItems: "center", flexWrap: "wrap" }}>
        <ActionButton onClick={addManualLine}>+ ADD MANUAL LINE</ActionButton>
        <ActionButton
          onClick={combineSelected}
          disabled={selectedCount < 2}
          style={selectedCount < 2 ? { opacity: 0.45, cursor: "not-allowed" } : undefined}
        >
          ⇄ COMBINE AS ROUND TRIP{selectedCount >= 2 ? ` (${selectedCount})` : ""}
        </ActionButton>
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          {selectedCount >= 2
            ? "Bills the ticked legs once at the round-trip rate."
            : "Tip: tick two legs (outbound + inbound) to bill them as one round trip."}
        </span>
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
            <DirectionTag direction={t.direction} />
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
              {t.routeName}
              {t.roundTripKey ? "" : " · unpaired leg"} · {t.serviceDate}
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
        <ActionButton variant="primary" onClick={save} disabled={busy}>
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
  onMutated,
}: {
  id: string;
  /** List-affecting change (status/QBO reference/lines) — parent refreshes the list. */
  onMutated: () => void;
}) {
  const [inv, setInv] = useState<InvoiceDetailRecord | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [confirm, setConfirm] = useState<"void" | "clearPayment" | null>(null);
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [writeOffOpen, setWriteOffOpen] = useState(false);
  const [markMode, setMarkMode] = useState<"enter" | "edit" | null>(null);
  const [copyState, setCopyState] = useState<"idle" | "ok" | "fail">("idle");
  // Per-line copy feedback: the lineId that was just copied (cleared after 2s).
  const [copiedLine, setCopiedLine] = useState<string | null>(null);

  // Live trip + passenger detail, keyed by trip id, fetched from the Trips API
  // (never snapshotted onto the invoice). Cached for this mount; the component
  // remounts per invoice (key={id}), and refetches when the line set changes.
  const [tripDetails, setTripDetails] = useState<TripDetailMap>({});
  const [expandedLines, setExpandedLines] = useState<Record<string, boolean>>({});

  // Distinct trip ids across all lines, as a stable comma-joined key so a
  // status/reference mutation (which does not touch tripIds) never refetches.
  const tripIdsKey = useMemo(() => {
    if (!inv) return "";
    return Array.from(new Set(inv.lines.flatMap((l) => l.tripIds)))
      .sort()
      .join(",");
  }, [inv]);

  // Every referenced trip has landed (loaded OR failed — a failed fetch still
  // has an entry and prints as "trip details unavailable"), so the copy and the
  // prep sheet will carry complete notes. A dead Trips API delays these by one
  // round trip; it can never wedge them.
  const detailsReady = useMemo(() => {
    const ids = tripIdsKey ? tripIdsKey.split(",") : [];
    return ids.every((tid) => tripDetails[tid]);
  }, [tripIdsKey, tripDetails]);

  useEffect(() => {
    const ids = tripIdsKey ? tripIdsKey.split(",") : [];
    if (ids.length === 0) return;
    let active = true;
    // The fetch replaces the whole map (Object.fromEntries below), so trip ids
    // not in the new set drop out; rendering only reads entries for current
    // line trip ids, so no synchronous reset is needed here.
    Promise.all(
      ids.map(async (tid): Promise<[string, LoadedTrip]> => {
        try {
          const trip = await getTrip(tid);
          if (!trip.manifestId) {
            return [tid, { trip, manifest: null, error: false, manifestError: false }];
          }
          try {
            const manifest = await getTripManifest(trip.manifestId);
            return [tid, { trip, manifest, error: false, manifestError: false }];
          } catch {
            return [tid, { trip, manifest: null, error: false, manifestError: true }];
          }
        } catch {
          return [tid, { trip: null, manifest: null, error: true, manifestError: false }];
        }
      }),
    ).then((entries) => {
      if (active) setTripDetails(Object.fromEntries(entries));
    });
    return () => {
      active = false;
    };
  }, [tripIdsKey]);

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

  async function runVoid() {
    if (!inv || busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await voidInvoice(inv.id);
      const fresh = await refetchUntil(() => getInvoice(inv.id), (d) => d.status === "Void");
      setInv(fresh);
      setConfirm(null);
      onMutated();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "The action failed — please try again.");
      setConfirm(null);
    }
    setBusy(false);
  }

  async function runClearPayment() {
    if (!inv || busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await clearInvoicePayment(inv.id);
      const fresh = await refetchUntil(() => getInvoice(inv.id), (d) => d.status === "EnteredInQbo");
      setInv(fresh);
      setConfirm(null);
      onMutated();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "The action failed — please try again.");
      setConfirm(null);
    }
    setBusy(false);
  }

  async function handleCopy() {
    if (!inv || !detailsReady) return;
    const ok = await copyToClipboard(invoiceClipboardText(inv, tripDetails));
    setCopyState(ok ? "ok" : "fail");
    setTimeout(() => setCopyState("idle"), ok ? 2000 : 4500);
  }

  async function handleCopyLine(l: InvoiceDetailRecord["lines"][number]) {
    const ok = await copyToClipboard(lineClipboardText(l, tripDetails));
    setCopiedLine(ok ? l.lineId : null);
    if (ok) {
      setTimeout(() => setCopiedLine((cur) => (cur === l.lineId ? null : cur)), 2000);
    }
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
  const isEntered = inv.status === "EnteredInQbo";
  const isPaid = inv.status === "Paid";
  // Written off = fully read-only, exactly like Void — the loss is on record.
  const isWrittenOff = inv.status === "WrittenOff";
  // Entered but unpaid is what "outstanding" means everywhere in this screen.
  const isOutstanding = isEntered;

  // Both exports carry the same facts — the QBO entry record and the per-line
  // trip & passenger detail — so it never matters which one the dispatcher
  // keys from. Both wait on the trip fetches: half-loaded passenger notes are
  // worse than a moment's delay when the numbers are going into the books.
  const exportButtons = (
    <>
      <ActionButton onClick={handleCopy} disabled={!detailsReady}>
        {!detailsReady ? "PREPARING…" : copyState === "ok" ? "COPIED ✓" : "COPY FOR QUICKBOOKS"}
      </ActionButton>
      <ActionButton onClick={() => printInvoicePrepSheet(inv, tripDetails)} disabled={!detailsReady}>
        {detailsReady ? "PRINT PREP SHEET" : "PREPARING…"}
      </ActionButton>
    </>
  );

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
          {inv.lines.map((l) => {
            const hasTrips = l.tripIds.length > 0;
            const expanded = expandedLines[l.lineId] ?? false;
            // Copy waits for the line's trip+passenger details so the notes
            // are complete (dates, passengers taken, deadhead call-outs).
            const copyReady = l.tripIds.every((tid) => tripDetails[tid]);
            return (
              <div key={l.lineId} style={{ borderBottom: `1px solid ${colors.borderSubtle}` }}>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    gap: 12,
                    padding: "9px 0",
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
                    {hasTrips && (
                      <span
                        onClick={() =>
                          setExpandedLines((prev) => ({ ...prev, [l.lineId]: !expanded }))
                        }
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: 4,
                          marginLeft: 8,
                          cursor: "pointer",
                          fontFamily: fonts.semiCondensed,
                          fontSize: 9.5,
                          letterSpacing: ".06em",
                          textTransform: "uppercase",
                          color: colors.skyBlue,
                          whiteSpace: "nowrap",
                        }}
                      >
                        <span aria-hidden>{expanded ? "▾" : "▸"}</span>
                        {l.tripIds.length} trip{l.tripIds.length === 1 ? "" : "s"} · passengers
                      </span>
                    )}
                    <span
                      onClick={() => copyReady && void handleCopyLine(l)}
                      title={
                        copyReady
                          ? "Copy this line for QuickBooks — dates, passengers taken, deadhead notes"
                          : "Loading trip & passenger details…"
                      }
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 4,
                        marginLeft: 8,
                        cursor: copyReady ? "pointer" : "default",
                        fontFamily: fonts.semiCondensed,
                        fontSize: 9.5,
                        letterSpacing: ".06em",
                        textTransform: "uppercase",
                        color: copyReady ? colors.skyBlue : colors.textFaint,
                        whiteSpace: "nowrap",
                      }}
                    >
                      <span aria-hidden>⧉</span>
                      {copiedLine === l.lineId ? "COPIED ✓" : "COPY"}
                    </span>
                  </span>
                  <span style={{ fontFamily: fonts.mono, color: colors.textDim, flex: "none" }}>
                    {l.quantity} × {formatInvoiceCad(l.unitPriceCad)}
                  </span>
                  <span style={{ fontFamily: fonts.mono, color: colors.textPrimary, flex: "none", width: 92, textAlign: "right" }}>
                    {formatInvoiceCad(l.amountCad)}
                  </span>
                </div>
                {hasTrips && expanded && (
                  <div style={{ padding: "0 0 9px" }}>
                    {l.tripIds.map((tid) => (
                      <TripDetailBlock key={tid} tripId={tid} loaded={tripDetails[tid]} />
                    ))}
                  </div>
                )}
              </div>
            );
          })}

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
        <MiniCard label="Terms (informational)" value={`Net ${inv.netTermsDays}`} />
      </div>

      {/* QuickBooks entry card — where the manual QBO reference is recorded */}
      <div style={{ ...cardStyle, marginBottom: 16 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>
              QuickBooks entry
            </div>
            {isEntered || isPaid ? (
              <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}>
                <StatusChip kind={isPaid ? "ontime" : "info"} label={isPaid ? "Paid" : "Outstanding"} />
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>
                  {inv.qboInvoiceId ?? "—"}
                </span>
                {inv.qboEnteredDate && (
                  <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
                    entered {inv.qboEnteredDate}
                  </span>
                )}
              </div>
            ) : isDraft ? (
              <div style={{ fontFamily: fonts.body, fontSize: 12, color: statusMeta("soon").t, fontWeight: 600 }}>
                Not yet entered in QuickBooks
              </div>
            ) : isWrittenOff ? (
              <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}>
                <StatusChip kind="over" label="Written off" />
                <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>
                  {inv.qboInvoiceId ?? "—"}
                </span>
                {inv.qboEnteredDate && (
                  <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
                    entered {inv.qboEnteredDate}
                  </span>
                )}
              </div>
            ) : (
              <StatusChip kind="off" label="Void — nothing to enter" />
            )}
          </div>
          {isDraft && (
            <ActionButton variant="primary" onClick={() => setMarkMode("enter")} style={{ padding: "4px 10px", fontSize: 12 }}>
              MARK ENTERED
            </ActionButton>
          )}
          {(isEntered || isPaid) && (
            <ActionButton onClick={() => setMarkMode("edit")} style={{ padding: "4px 10px", fontSize: 12 }}>
              EDIT QBO REFERENCE
            </ActionButton>
          )}
        </div>
        {(isEntered || isPaid) && (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 10,
              flexWrap: "wrap",
              marginTop: 12,
              paddingTop: 10,
              borderTop: `1px solid ${colors.borderSubtle}`,
            }}
          >
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 5 }}>
                Payment
              </div>
              {isPaid ? (
                <div style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}>
                  <StatusChip kind="ontime" label="Payment confirmed" />
                  <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textSecondary }}>
                    {inv.paymentConfirmedDate ?? "—"}
                  </span>
                </div>
              ) : (
                <div style={{ fontFamily: fonts.body, fontSize: 12, color: statusMeta("soon").t, fontWeight: 600 }}>
                  Outstanding — payment not yet confirmed
                </div>
              )}
            </div>
            {isOutstanding && (
              <ActionButton
                variant="success"
                onClick={() => setPaymentOpen(true)}
                style={{ padding: "4px 10px", fontSize: 12 }}
              >
                CONFIRM PAYMENT
              </ActionButton>
            )}
            {isPaid && (
              <ActionButton onClick={() => setConfirm("clearPayment")} style={{ padding: "4px 10px", fontSize: 12 }}>
                CLEAR PAYMENT
              </ActionButton>
            )}
          </div>
        )}
      </div>

      {/* write-off record — amount, date, and reason of the loss */}
      {isWrittenOff && (
        <div style={{ ...cardStyle, marginBottom: 16, borderColor: "rgba(213,94,0,.4)" }}>
          <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginBottom: 6 }}>Write-off</div>
          <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap", marginBottom: 6 }}>
            <StatusChip kind="over" label="Written off" />
            <span style={{ fontFamily: fonts.mono, fontSize: 12.5, color: statusMeta("over").t, fontWeight: 600 }}>
              {inv.writtenOffAmountCad != null ? formatInvoiceCad(inv.writtenOffAmountCad) : "—"}
            </span>
            {inv.writtenOffDate && (
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>on {inv.writtenOffDate}</span>
            )}
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.5 }}>
            {inv.writtenOffReason ?? "No reason recorded."}
          </div>
        </div>
      )}

      {/* actions */}
      <div style={{ display: "flex", gap: 9, flexWrap: "wrap", alignItems: "center" }}>
        {isDraft && (
          <>
            {exportButtons}
            <ActionButton variant="primary" onClick={() => setMarkMode("enter")}>
              MARK ENTERED IN QUICKBOOKS
            </ActionButton>
            <ActionButton variant="destructive" onClick={() => setConfirm("void")}>
              VOID DRAFT
            </ActionButton>
          </>
        )}
        {isEntered && (
          <>
            {exportButtons}
            <ActionButton onClick={() => setMarkMode("edit")}>EDIT QBO REFERENCE</ActionButton>
            <ActionButton variant="success" onClick={() => setPaymentOpen(true)}>
              CONFIRM PAYMENT
            </ActionButton>
            <ActionButton variant="destructive" onClick={() => setWriteOffOpen(true)}>
              WRITE OFF
            </ActionButton>
          </>
        )}
        {isWrittenOff && (
          <>
            {exportButtons}
          </>
        )}
        {isPaid && (
          <>
            {exportButtons}
            <ActionButton onClick={() => setMarkMode("edit")}>EDIT QBO REFERENCE</ActionButton>
            {/* No REOPEN here: the backend refuses to reopen a paid invoice until the
                payment confirmation is cleared, so offering it would only ever 409. */}
            <ActionButton onClick={() => setConfirm("clearPayment")}>CLEAR PAYMENT</ActionButton>
          </>
        )}
        {copyState === "fail" && (
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: statusMeta("over").t }}>
            Copy failed — use PRINT PREP SHEET and select the text there instead; it carries the same detail.
          </span>
        )}
      </div>

      {markMode !== null && (
        <MarkEnteredModal
          inv={inv}
          mode={markMode}
          onClose={() => setMarkMode(null)}
          onDone={(fresh) => {
            setInv(fresh);
            setMarkMode(null);
            onMutated();
          }}
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
          onConfirm={runVoid}
          onClose={() => setConfirm(null)}
        />
      )}
      {paymentOpen && (
        <ConfirmPaymentModal
          inv={inv}
          onClose={() => setPaymentOpen(false)}
          onDone={(fresh) => {
            setInv(fresh);
            setPaymentOpen(false);
            onMutated();
          }}
        />
      )}
      {confirm === "clearPayment" && (
        <ConfirmModal
          eyebrow={`Billing · ${inv.invoiceNumber}`}
          title="Clear confirmed payment?"
          body={`Clear the recorded payment on ${inv.invoiceNumber}? It returns to outstanding here — nothing changes in QuickBooks. Use this if the payment was confirmed in error.`}
          confirmLabel="CLEAR PAYMENT"
          busy={busy}
          onConfirm={runClearPayment}
          onClose={() => setConfirm(null)}
        />
      )}
      {writeOffOpen && (
        <WriteOffModal
          inv={inv}
          onClose={() => setWriteOffOpen(false)}
          onDone={(fresh) => {
            setInv(fresh);
            setWriteOffOpen(false);
            onMutated();
          }}
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

  // Receivable split, derived from the rows the list already has. Drafts and voids are in
  // no bucket: nothing has been billed yet, so there is nothing owing. Written-off
  // invoices get their own bucket — the lost money must not silently vanish.
  const outstanding = (rows ?? []).filter((r) => r.status === "EnteredInQbo");
  const paid = (rows ?? []).filter((r) => r.status === "Paid");
  const writtenOff = (rows ?? []).filter((r) => r.status === "WrittenOff");

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
          <PageHeader eyebrow="Business · Prepare invoices for QuickBooks" title="Billing Worksheets" />
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
                  QuickBooks is your invoicing system
                </div>
                <div style={{ fontFamily: fonts.mono, fontSize: 9.5, color: colors.textDim }}>
                  Prepare here · enter there · record the QBO number back
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
              {/* Outstanding vs paid vs written off — entered in QBO but not yet
                  confirmed received, against confirmed received, against recorded
                  losses. Drafts and voids are in none: nothing has been billed yet,
                  so there is nothing owing. */}
              <div
                style={{
                  display: "flex",
                  gap: 10,
                  flexWrap: "wrap",
                  marginBottom: 12,
                }}
              >
                <ReceivableTile
                  label="Outstanding"
                  kind="info"
                  count={outstanding.length}
                  total={outstanding.reduce((sum, r) => sum + r.totalCad, 0)}
                />
                <ReceivableTile
                  label="Paid"
                  kind="ontime"
                  count={paid.length}
                  total={paid.reduce((sum, r) => sum + r.totalCad, 0)}
                />
                <ReceivableTile
                  label="Written off"
                  kind="over"
                  count={writtenOff.length}
                  total={writtenOff.reduce((sum, r) => sum + (r.writtenOffAmountCad ?? 0), 0)}
                />
              </div>
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
                      {row.status === "EnteredInQbo" && row.qboInvoiceId && (
                        <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim }}>
                          QBO {row.qboInvoiceId}
                        </div>
                      )}
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
                        {row.status === "EnteredInQbo" && row.qboEnteredDate ? row.qboEnteredDate : "—"}
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
            <InvoiceDetail key={invoiceSelId} id={invoiceSelId} onMutated={load} />
          ) : (
            rows !== null &&
            !loadError && (
              <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
                Select an invoice to see its lines, QuickBooks entry, and actions.
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
