"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { BudgetCode, BudgetPeriod } from "@/lib/types";
import { ApiError } from "@/lib/api/transport";
import {
  actualAmountError,
  actualCodeCandidates,
  actualDateError,
  actualDescriptionError,
  listActualTransactions,
  listBudgetCodes,
  periodCovering,
  recordActualTransaction,
  refetchUntil,
  toBudgetCode,
  updateActualTransaction,
  ACTUAL_DESCRIPTION_MAX_LENGTH,
  type ActualTransactionRecord,
} from "@/lib/api/budgeting";
import { ModalShell } from "@/components/ui/ModalShell";
import { DateField, NumberField, SelectField, TextAreaField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { usePeriodHold } from "@/lib/periodHold";

// Record-or-correct modal for one actual transaction, opened from Actuals vs Budget. One
// useState per field, the server's validation is authoritative, and a 400/404/409 surfaces as
// the backend's own message in the banner. Its save takes the period hold (usePeriodHold), like
// BudgetItemFormModal's.
//
// Three server rules this form makes visible rather than letting the user discover by refusal:
//
//   1. **The date must fall inside an existing budget period.** The period is derived from the
//      date and never stored, so a date in no period would be a row the period-driven console
//      could never show again — the server refuses it (NoPeriodCoversDate). This form knows
//      every period, so it names the period a date counts in while the date is being picked, and
//      refuses an uncovered one before the round trip.
//   2. **New spend cannot go on a retired code, but an existing row on one stays correctable.**
//      That asymmetry is the server's (actualCodeCandidates documents both handlers), and it is
//      why editing keeps the row's own code selectable however long ago it was retired.
//   3. **The code must belong to the period covering the date.** Codes are per period; the
//      handlers look the code up in the covering period and answer 404 Budgeting.Code.NotFound
//      for any other period's code, even one with the same string. So the picker offers the
//      COVERING period's chart: the entered period's comes in as a prop, any other period's is
//      fetched here when the date moves into it. On such a move the selection follows the code
//      STRING (the cross-period identity) if that period has an active code of it, and otherwise
//      empties so the user re-picks — never silently keeps an id the server will refuse.
//
// There is no period field and no tax field, and neither is an omission: the date decides the
// period, and the platform never computes tax.
//
// Amounts may carry cents (numeric(12,2)); a third decimal place is refused here rather than
// rounded by the server — see actualAmountError.

export default function ActualTransactionFormModal({
  periods,
  openPeriod,
  openPeriodCodes,
  transaction,
  onClose,
  onSaved,
}: {
  /** Every period, so the covering-period check sees the same set the server does. */
  periods: BudgetPeriod[];
  /** The period the screen is showing — the default date comes from it. */
  openPeriod: BudgetPeriod;
  /** The open period's chart (codes are per period); filtered by actualCodeCandidates. */
  openPeriodCodes: BudgetCode[];
  /** null → record a new transaction; a row → correct it. */
  transaction: ActualTransactionRecord | null;
  onClose: () => void;
  /** The write is visible in the projection by the time this is called. */
  onSaved: () => void;
}) {
  const editing = transaction !== null;
  const initialCandidates = actualCodeCandidates(openPeriodCodes, transaction?.budgetCodeId ?? null);

  const [codeId, setCodeId] = useState(
    transaction?.budgetCodeId ?? initialCandidates[0]?.id ?? "",
  );
  /** The string of the code last picked — what the selection follows into another period. */
  const [codeString, setCodeString] = useState(
    transaction?.code ?? initialCandidates[0]?.code ?? "",
  );
  const [amount, setAmount] = useState(transaction ? String(transaction.amountCad) : "");
  const [occurredOn, setOccurredOn] = useState(
    transaction?.occurredOn ?? defaultDate(openPeriod),
  );
  const [description, setDescription] = useState(transaction?.description ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  /** Other periods' charts, fetched on demand when the date moves into them; null = failed. */
  const [charts, setCharts] = useState<Record<string, BudgetCode[] | null>>({});
  usePeriodHold(busy);

  // Live answer to "where does this land?", from the same rule the server applies.
  const covering = periodCovering(periods, occurredOn);
  const coveringId = covering?.id ?? null;
  const needsFetch = coveringId !== null && coveringId !== openPeriod.id && !(coveringId in charts);

  useEffect(() => {
    if (!needsFetch || coveringId === null) return;
    let active = true;
    listBudgetCodes(coveringId).then(
      (records) => {
        if (active) setCharts((prev) => ({ ...prev, [coveringId]: records.map(toBudgetCode) }));
      },
      () => {
        if (active) setCharts((prev) => ({ ...prev, [coveringId]: null }));
      },
    );
    return () => {
      active = false;
    };
  }, [needsFetch, coveringId]);

  // The covering period's chart: undefined while loading, null when it failed to load.
  const chart: BudgetCode[] | null | undefined =
    coveringId === null || coveringId === openPeriod.id ? openPeriodCodes : charts[coveringId];
  const chartLoading = chart === undefined;
  const candidates = chart ? actualCodeCandidates(chart, transaction?.budgetCodeId ?? null) : [];

  // Derived, never synced in an effect: the picked id if this chart has it, else this chart's
  // active code with the same string, else nothing (the user re-picks).
  const effectiveCodeId = candidates.some((c) => c.id === codeId)
    ? codeId
    : (candidates.find((c) => c.active && c.code === codeString)?.id ?? "");
  const followedByString = effectiveCodeId !== "" && effectiveCodeId !== codeId;

  const codeOptions = candidates.map((c) => ({
    value: c.id,
    label: `${c.code} · ${c.name}${c.active ? "" : " (retired)"}`,
  }));
  const selectedCode = candidates.find((c) => c.id === effectiveCodeId) ?? null;
  // The row the user actually picked, from whichever chart it came from — when the selection has
  // followed its string into another period, this is what the followed row is compared against.
  const pickedCode =
    [openPeriodCodes, ...Object.values(charts)]
      .flatMap((c) => c ?? [])
      .find((c) => c.id === codeId) ?? null;
  const followedDiffers =
    followedByString &&
    selectedCode !== null &&
    pickedCode !== null &&
    (pickedCode.name !== selectedCode.name || pickedCode.category !== selectedCode.category);

  function pickCode(id: string) {
    setCodeId(id);
    setCodeString(candidates.find((c) => c.id === id)?.code ?? "");
  }

  async function submit() {
    if (busy) return;

    // Mirrors of the server's checks, so the common mistakes are caught without a round trip.
    // The server re-checks all of it — it sees periods and codes this client may not have
    // refetched — and its answer is the one that counts.
    const dateProblem = actualDateError(occurredOn, periods);
    if (dateProblem) return setError(dateProblem);
    if (chartLoading) return;
    if (!effectiveCodeId) {
      return setError(
        `Pick a budget code from ${covering?.label ?? "the period"}'s chart — codes belong to a period, and the date decides which one.`,
      );
    }
    const amountProblem = actualAmountError(amount);
    if (amountProblem) return setError(amountProblem);
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
      const budgetCodeId = effectiveCodeId;
      const body = { budgetCodeId, amountCad, occurredOn, description: trimmed };
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
              r.budgetCodeId === budgetCodeId &&
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
          <ActionButton onClick={onClose} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton
            variant="primary"
            onClick={submit}
            disabled={busy || chartLoading || candidates.length === 0}
          >
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "RECORD TRANSACTION"}
          </ActionButton>
        </>
      }
    >
      {chartLoading ? (
        <Note>Loading {covering?.label ?? "the period"}&apos;s budget codes…</Note>
      ) : chart === null ? (
        <Note>
          {covering?.label ?? "That period"}&apos;s budget codes could not be loaded. Change the
          date back, or close this form and try again.
        </Note>
      ) : candidates.length > 0 ? (
        <SelectField
          label={covering ? `Budget code · ${covering.label}` : "Budget code"}
          value={effectiveCodeId}
          onChange={pickCode}
          options={
            effectiveCodeId === ""
              ? [{ value: "", label: "— Pick a code —" }, ...codeOptions]
              : codeOptions
          }
          hint={
            editing
              ? "Moving a transaction to another code is how a miscoded expense is fixed"
              : "Active codes — an expense is a positive amount on an expense code"
          }
        />
      ) : (
        <Note>
          {covering?.label ?? "This period"} has no active budget codes yet. Add one under Budget
          Codes (in that period) before recording what was spent, so the figure has something to
          roll up to.
        </Note>
      )}

      {covering && covering.id !== openPeriod.id && !chartLoading && chart !== null && (
        <Note>
          This date counts in {covering.label}, not {openPeriod.label}, so the code comes from{" "}
          {covering.label}&apos;s own chart
          {followedByString
            ? ` — its own ${codeString} (same code string, a different row) has been picked.${
                followedDiffers && selectedCode && pickedCode
                  ? ` In ${covering.label} it is “${selectedCode.name}” (${selectedCode.category.toLowerCase()}), not “${pickedCode.name}” (${pickedCode.category.toLowerCase()}) — check it is still the right code.`
                  : ""
              }`
            : codeString && effectiveCodeId === ""
              ? ` — it has no active ${codeString}, so pick a code.`
              : "."}
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
          step={0.01}
          placeholder="0.00"
          hint="To the cent — always positive; the code's category says which side of the ledger it lands on"
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

/** A quiet explanatory line under a field. */
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
