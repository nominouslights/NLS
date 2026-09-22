"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import {
  createPurchaseOrder,
  poTermsLabel,
  updatePurchaseOrder,
  type PurchaseOrderUpdateInput,
  type PurchaseOrderRecord,
} from "@/lib/api/clients";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create or edit a client purchase order — POST/PUT
// /api/clients/{id}/purchase-orders (Clients module). Expiry is optional; the
// expiry chips on the PO dashboard are derived client-side from it
// (docStatusFor thresholds), never stored.
//
// A PO carries its own PRICING TERMS, and both rate fields are optional: a blank
// round-trip rate inherits the contract's, a blank one-way rate bills at ½ the
// effective round-trip rate. Because a blank field that silently means "half of
// something else" is how a pricing bug reaches an invoice, this form previews the
// EFFECTIVE terms live, in the same words the PO dashboard and the accruals
// report use (poTermsLabel — lib/api/clients.ts).

/** A rate box's value as a number, or null when blank. NaN reads as invalid and
 *  is caught on submit. */
function rateValue(raw: string): number | null {
  return raw === "" ? null : Number(raw);
}

export default function PoFormModal({
  clientId,
  clientName,
  contractRateCad,
  existing,
  onClose,
  onSaved,
}: {
  clientId: string;
  clientName: string;
  /** The active contract's round-trip rate — the fallback a blank PO rate
   *  inherits, shown live in the effective-terms preview. */
  contractRateCad: number | null;
  /** When set, edits this PO (PUT); otherwise creates (POST). */
  existing?: PurchaseOrderRecord | null;
  onClose: () => void;
  /** The saved PO id + the submitted body (for refetchUntil predicates). */
  onSaved: (poId: string, input: PurchaseOrderUpdateInput) => void;
}) {
  const editing = existing != null;

  const [poNumber, setPoNumber] = useState(existing?.poNumber ?? "");
  const [issued, setIssued] = useState(existing?.issued ?? "");
  const [expiry, setExpiry] = useState(existing?.expiry ?? "");
  const [noExpiry, setNoExpiry] = useState(editing ? existing.expiry == null : false);
  const [amount, setAmount] = useState(existing?.amountCad != null ? String(existing.amountCad) : "");
  const [roundTripRate, setRoundTripRate] = useState(
    existing?.roundTripRateCad != null ? String(existing.roundTripRateCad) : "",
  );
  const [oneWayRate, setOneWayRate] = useState(
    existing?.oneWayRateCad != null ? String(existing.oneWayRateCad) : "",
  );
  const [note, setNote] = useState(existing?.note ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // What this PO will actually price at, recomputed as the boxes are typed in.
  const roundTripNum = rateValue(roundTripRate);
  const oneWayNum = rateValue(oneWayRate);
  const termsPreview = poTermsLabel(
    {
      id: "",
      clientId,
      poNumber,
      issued,
      expiry: null,
      amountCad: null,
      roundTripRateCad: roundTripNum != null && Number.isFinite(roundTripNum) ? roundTripNum : null,
      oneWayRateCad: oneWayNum != null && Number.isFinite(oneWayNum) ? oneWayNum : null,
      note: null,
    },
    contractRateCad,
  );

  async function submit() {
    if (busy) return;
    if (!poNumber.trim()) return setError("Enter the PO number (as issued by the client).");
    if (!issued) return setError("Enter the issue date.");
    if (!noExpiry && !expiry) return setError("Enter an expiry date, or turn on \"No expiry\".");
    const amountNum = amount === "" ? null : Number(amount);
    if (amountNum != null && (Number.isNaN(amountNum) || amountNum < 0)) {
      return setError("The amount must be a non-negative number (CAD), or left blank.");
    }
    if (roundTripNum != null && (Number.isNaN(roundTripNum) || roundTripNum < 0)) {
      return setError(
        "The round-trip rate must be a non-negative number (CAD), or left blank to inherit the contract rate.",
      );
    }
    if (oneWayNum != null && (Number.isNaN(oneWayNum) || oneWayNum < 0)) {
      return setError(
        "The one-way rate must be a non-negative number (CAD), or left blank to bill at ½ the round-trip rate.",
      );
    }

    // Every key is set explicitly, null included — PUT is a full replace and the
    // backend rejects a missing key, so an omitted field is never how a rate is
    // cleared here. PurchaseOrderUpdateInput is what makes that a compile error.
    const input: PurchaseOrderUpdateInput = {
      poNumber: poNumber.trim(),
      issued,
      expiry: noExpiry ? null : expiry,
      amountCad: amountNum,
      roundTripRateCad: roundTripNum,
      oneWayRateCad: oneWayNum,
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
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
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
      {/* pricing terms — both optional, both inheriting when blank */}
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginTop: 14 }}>
        <NumberField
          label="Round-trip rate (CAD, optional)"
          value={roundTripRate}
          onChange={setRoundTripRate}
          min={0}
          step={0.01}
          placeholder={contractRateCad != null ? `${contractRateCad} (contract)` : "no contract rate"}
        />
        <NumberField
          label="One-way rate (CAD, optional)"
          value={oneWayRate}
          onChange={setOneWayRate}
          min={0}
          step={0.01}
          placeholder="½ of the round trip"
        />
      </div>
      <div
        style={{
          marginTop: 10,
          padding: "9px 12px",
          background: colors.cardBgActive,
          border: `1px solid ${colors.borderSubtle}`,
          borderRadius: 8,
        }}
      >
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontWeight: 600,
            fontSize: 10,
            letterSpacing: ".08em",
            textTransform: "uppercase",
            color: colors.textDim,
          }}
        >
          Will bill at
        </div>
        <div style={{ fontFamily: fonts.mono, fontSize: 12, color: colors.textPrimary, marginTop: 3 }}>
          {termsPreview}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 4, lineHeight: 1.5 }}>
          Leave a rate blank to inherit: the round trip falls back to the contract rate, one way to
          ½ the effective round-trip rate. Rates are quoted tax-inclusive — taxes are applied in
          QuickBooks.
        </div>
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
