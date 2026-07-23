"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createPurchaseOrder,
  updatePurchaseOrder,
  type PurchaseOrderInput,
  type PurchaseOrderRecord,
} from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create or edit a client purchase order — POST/PUT
// /api/clients/{id}/purchase-orders (Clients module). Expiry is optional; the
// expiry chips on the PO dashboard are derived client-side from it
// (docStatusFor thresholds), never stored.

export default function PoFormModal({
  clientId,
  clientName,
  existing,
  onClose,
  onSaved,
}: {
  clientId: string;
  clientName: string;
  /** When set, edits this PO (PUT); otherwise creates (POST). */
  existing?: PurchaseOrderRecord | null;
  onClose: () => void;
  /** The saved PO id + the submitted body (for refetchUntil predicates). */
  onSaved: (poId: string, input: PurchaseOrderInput) => void;
}) {
  const editing = existing != null;

  const [poNumber, setPoNumber] = useState(existing?.poNumber ?? "");
  const [issued, setIssued] = useState(existing?.issued ?? "");
  const [expiry, setExpiry] = useState(existing?.expiry ?? "");
  const [noExpiry, setNoExpiry] = useState(editing ? existing.expiry == null : false);
  const [amount, setAmount] = useState(existing?.amountCad != null ? String(existing.amountCad) : "");
  const [note, setNote] = useState(existing?.note ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    if (!poNumber.trim()) return setError("Enter the PO number (as issued by the client).");
    if (!issued) return setError("Enter the issue date.");
    if (!noExpiry && !expiry) return setError("Enter an expiry date, or turn on \"No expiry\".");
    const amountNum = amount === "" ? null : Number(amount);
    if (amountNum != null && (Number.isNaN(amountNum) || amountNum < 0)) {
      return setError("The amount must be a non-negative number (CAD), or left blank.");
    }

    const input: PurchaseOrderInput = {
      poNumber: poNumber.trim(),
      issued,
      expiry: noExpiry ? null : expiry,
      amountCad: amountNum,
      note: note.trim() || null,
    };

    setBusy(true);
    setError(null);
    try {
      if (editing) {
        await updatePurchaseOrder(clientId, existing.id, input);
        onSaved(existing.id, input);
      } else {
        const newId = await createPurchaseOrder(clientId, input);
        onSaved(newId, input);
      }
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to save the purchase order — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Business · ${clientName} · Purchase orders`}
      title={editing ? "Edit Purchase Order" : "Add Purchase Order"}
      onClose={onClose}
      error={error}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} style={busy ? { opacity: 0.6, cursor: "wait" } : undefined}>
            {busy ? "SAVING…" : editing ? "SAVE PO" : "ADD PO"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14 }}>
        <TextField label="PO number" value={poNumber} onChange={setPoNumber} mono placeholder="PO-AG-2310" />
        <NumberField label="Amount (CAD, optional)" value={amount} onChange={setAmount} min={0} step={0.01} placeholder="96000" />
        <DateField label="Issued" value={issued} onChange={setIssued} />
        <DateField label="Expiry" value={expiry} onChange={setExpiry} disabled={noExpiry} />
      </div>
      <div style={{ marginTop: 14 }}>
        <TextField label="Note (optional)" value={note} onChange={setNote} placeholder="Crew shuttle — annual" />
      </div>

      {/* no-expiry toggle */}
      <div style={{ marginTop: 16, display: "flex", alignItems: "center", gap: 11 }}>
        <span
          onClick={() => setNoExpiry((v) => !v)}
          style={{
            width: 40,
            height: 22,
            flex: "none",
            borderRadius: 999,
            background: noExpiry ? colors.blue : colors.borderStrong,
            position: "relative",
            cursor: "pointer",
            transition: "background .15s",
          }}
        >
          <span
            style={{
              position: "absolute",
              top: 2,
              left: noExpiry ? 20 : 2,
              width: 18,
              height: 18,
              borderRadius: "50%",
              background: "#FFFFFF",
              boxShadow: colors.shadowCard,
              transition: "left .15s",
            }}
          />
        </span>
        <div>
          <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
            No expiry
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            Turn on for open POs — the dashboard expiry chip shows Valid.
          </div>
        </div>
      </div>
    </ModalShell>
  );
}
