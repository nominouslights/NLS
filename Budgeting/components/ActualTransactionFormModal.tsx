"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { BudgetCode, BudgetPeriod } from "@/lib/types";
import { ApiError } from "@/lib/api/transport";
import {
  actualAmountError,
  actualCodeCandidates,
  actualDateError,
  actualDescriptionError,
  listActualTransactions,
  periodCovering,
  recordActualTransaction,
  refetchUntil,
  updateActualTransaction,
  ACTUAL_DESCRIPTION_MAX_LENGTH,
  type ActualTransactionRecord,
} from "@/lib/api/budgeting";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextAreaField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";

// Record-or-correct modal for one actual transaction, opened from Actuals vs Budget. Follows
// BudgetAllocationFormModal: one useState per field, the server's validation is authoritative,
// and a 400 or 409 surfaces as the backend's own message in the banner.
//
// Two server rules this form makes visible rather than letting the user discover by 409:
//
//   1. **The date must fall inside an existing budget period.** The period is derived from the
//      date and never stored, so a date in no period would be a row the period-driven console
//      could never show again — the server refuses it (NoPeriodCoversDate). This form knows
//      every period, so it names the period a date counts in while the date is being picked, and
//      refuses an uncovered one before the round trip.
//   2. **New spend cannot go on a retired code, but an existing row on one stays correctable.**
//      That asymmetry is the server's (actualCodeCandidates documents both handlers), and it is
//      why editing keeps the row's own code selectable however long ago it was retired.
//
// There is no period field and no tax field, and neither is an omission: the date decides the
// period, and the platform never computes tax.
//
// The whole-dollar check is this app's convention rather than the server's rule — see
// actualAmountError.

export default function ActualTransactionFormModal({
  periods,
  openPeriod,
  codes,
  transaction,
  onClose,
  onSaved,
}: {
  /** Every period, so the covering-period check sees the same set the server does. */
  periods: BudgetPeriod[];
  /** The period the screen is showing — the default date comes from it. */
  openPeriod: BudgetPeriod;
  /** The whole chart; filtered by actualCodeCandidates. */
  codes: BudgetCode[];
  /** null → record a new transaction; a row → correct it. */
  transaction: ActualTransactionRecord | null;
  onClose: () => void;
  /** The write is visible in the projection by the time this is called. */
  onSaved: () => void;
}) {
  const editing = transaction !== null;
  const candidates = actualCodeCandidates(codes, transaction?.budgetCodeId ?? null);

  const [codeId, setCodeId] = useState(transaction?.budgetCodeId ?? candidates[0]?.id ?? "");
  const [amount, setAmount] = useState(transaction ? String(transaction.amountCad) : "");
  const [occurredOn, setOccurredOn] = useState(
    transaction?.occurredOn ?? defaultDate(openPeriod),
  );
  const [description, setDescription] = useState(transaction?.description ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const codeOptions = candidates.map((c) => ({
    value: c.id,
    label: `${c.code} · ${c.name}${c.active ? "" : " (retired)"}`,
  }));
  const selectedCode = codes.find((c) => c.id === codeId) ?? null;
  // Live answer to "where does this land?", from the same rule the server applies.
  const covering = periodCovering(periods, occurredOn);

  async function submit() {
    if (busy) return;

    // Mirrors of the server's checks, so the common mistakes are caught without a round trip.
    // The server re-checks all of it — it sees periods and codes this client may not have
    // refetched — and its answer is the one that counts.
    if (!codeId) return setError("Add an active budget code before recording a transaction.");
    const amountProblem = actualAmountError(amount);
    if (amountProblem) return setError(amountProblem);
    const dateProblem = actualDateError(occurredOn, periods);
    if (dateProblem) return setError(dateProblem);
    const descriptionProblem = actualDescriptionError(description);
    if (descriptionProblem) return setError(descriptionProblem);

    const amountCad = Number(amount);
    const trimmed = description.trim();
    // Where the row will be after the save — which is not necessarily where it is now, because
    // correcting the date is exactly how a transaction moves between periods.
    const target = covering ?? openPeriod;

    setBusy(true);
    setError(null);

    try {
      const body = { budgetCodeId: codeId, amountCad, occurredOn, description: trimmed };
      let id: string;
      if (editing) {
        await updateActualTransaction(transaction.id, body);
        id = transaction.id;
      } else {
        id = await recordActualTransaction(body);
      }

      // The read side is a projection and trails the write — refetch until the change is visible
      // rather than assuming it already is, and compare VALUES, not existence: on a correction
      // the row was always there, so "the row exists" would be satisfied by the stale read.
      // The list fetched is the target period's, because a corrected date may have moved the row
      // out of the one on screen.
      await refetchUntil(
        () => listActualTransactions(target.id),
        (rows) =>
          rows.some(
            (r) =>
              r.id === id &&
              r.budgetCodeId === codeId &&
              r.amountCad === amountCad &&
              r.occurredOn === occurredOn &&
              r.description === trimmed,
          ),
      );
      onSaved();
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError ? e.message : "Failed to save the transaction — please try again.",
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Actuals · ${openPeriod.label}`}
      title={editing ? `Correct ${transaction.code}` : "Record a transaction"}
      onClose={onClose}
      error={error}
      maxWidth={620}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton
            variant="primary"
            onClick={submit}
            disabled={busy || candidates.length === 0}
          >
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "RECORD TRANSACTION"}
          </ActionButton>
        </>
      }
    >
      {candidates.length > 0 ? (
        <SelectField
          label="Budget code"
          value={codeId}
          onChange={setCodeId}
          options={codeOptions}
          hint={
            editing
              ? "Moving a transaction to another code is how a miscoded expense is fixed"
              : "Active codes — an expense is a positive amount on an expense code"
          }
        />
      ) : (
        <Note>
          There are no budget codes yet. Add one under Budget Codes before recording what was
          spent, so the figure has something to roll up to.
        </Note>
      )}

      {selectedCode && !selectedCode.active && (
        <div style={{ marginTop: 8 }}>
          <StatusChip kind="off" label="Retired code" />
          <Note>
            {selectedCode.id === transaction?.budgetCodeId
              ? "This code was retired after the transaction was recorded. The row stays correctable where it is — but it cannot be moved onto another retired code."
              : "Retired codes take no new transactions. Restore it under Budget Codes, or pick another."}
          </Note>
        </div>
      )}

      <div style={{ marginTop: 14 }}>
        <NumberField
          label="Amount (CAD)"
          value={amount}
          onChange={setAmount}
          min={0}
          step={1}
          placeholder="0"
          hint="Whole dollars — always positive; the code's category says which side of the ledger it lands on"
        />
      </div>

      <div style={{ marginTop: 14 }}>
        <DateField
          label="Date it happened"
          value={occurredOn}
          onChange={setOccurredOn}
          hint={
            covering
              ? `Counts in ${covering.label} (${covering.startsOn} — ${covering.endsOn})`
              : "No budget period covers this date — create the period first, or check the date"
          }
        />
      </div>

      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Description"
          value={description}
          onChange={setDescription}
          rows={3}
          placeholder="What the money was for — invoice, vendor, run, whatever names it again in six months."
          hint={`Required · ${description.trim().length}/${ACTUAL_DESCRIPTION_MAX_LENGTH}`}
        />
      </div>

      <Note>
        The date decides the period — there is no period to pick. Correcting the date is how a
        transaction moves between periods, and a date inside a closed period is still accepted:
        actuals are facts, and only the plan is frozen by the lifecycle.
      </Note>
    </ModalShell>
  );
}

/**
 * Today when the open period covers it, and the period's last day otherwise. A period being
 * reviewed weeks after it ended is the ordinary case for entering actuals, and defaulting to a
 * date outside the period on screen would make the first save read as a refusal.
 *
 * Built from local date parts rather than toISOString, which shifts the day west of UTC — the
 * hazard lib/period.ts documents.
 */
function defaultDate(period: BudgetPeriod): string {
  const now = new Date();
  const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(
    now.getDate(),
  ).padStart(2, "0")}`;
  return period.startsOn <= today && today <= period.endsOn ? today : period.endsOn;
}

/** A quiet explanatory line, matching BudgetAllocationFormModal's trailing note. */
function Note({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        marginTop: 10,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textDim,
        lineHeight: 1.6,
      }}
    >
      {children}
    </div>
  );
}
