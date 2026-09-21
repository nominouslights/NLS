"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { BudgetCode, BudgetCodeCategory } from "@/lib/types";
import { ApiError } from "@/lib/api/transport";
import {
  allocationAmountError,
  allocationCandidates,
  allocationJustificationError,
  listBudgetAllocations,
  refetchUntil,
  setBudgetAllocation,
  ALLOCATION_JUSTIFICATION_MAX_LENGTH,
  type BudgetAllocationRecord,
} from "@/lib/api/budgeting";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Set-or-edit modal for one allocation line, shared by the period dashboard and the Allocations
// screen. Follows BudgetCodeFormModal: one useState per field, server-side validation is
// authoritative, and a 400 or 409 surfaces as the backend's own message in the banner.
//
// Two rules this form carries visibly because the server enforces both:
//
//   1. **One line per code per period, upserted by code.** The picker only offers active codes
//      of the section's category that do not already carry a line (allocationCandidates); in
//      edit mode the code is fixed text, not a disabled input — to plan a different code you add
//      another line, or remove this one.
//   2. **Justification is required.** Zero-based means the argument is made fresh each period,
//      so an empty justification is refused before the round trip, and the server refuses it
//      again (Budgeting.Allocation.JustificationRequired). This is also what a line copied from
//      an earlier period meets: CopyInto brings the amount and leaves the justification empty,
//      so the copied line opens here already needing one — that is the feature, not a bug.
//
// The whole-dollar check is this app's convention rather than the server's rule — see
// allocationAmountError.

export default function BudgetAllocationFormModal({
  periodId,
  periodLabel,
  category,
  codes,
  lines,
  line,
  onClose,
  onSaved,
}: {
  periodId: string;
  periodLabel: string;
  category: BudgetCodeCategory;
  /** The whole chart; filtered by allocationCandidates. */
  codes: BudgetCode[];
  /** The period's current lines, so the picker skips codes already planned. */
  lines: BudgetAllocationRecord[];
  /** null → set a new line; a line → edit it (its code is fixed). */
  line: BudgetAllocationRecord | null;
  onClose: () => void;
  /** Fresh lines (already reflecting the change) plus the affected code's id. */
  onSaved: (lines: BudgetAllocationRecord[], codeId: string) => void;
}) {
  const editing = line !== null;
  const revenue = category === "Revenue";
  const candidates = allocationCandidates(codes, lines, category, line?.budgetCodeId ?? null);

  const [codeId, setCodeId] = useState(line?.budgetCodeId ?? candidates[0]?.id ?? "");
  const [amount, setAmount] = useState(line ? String(line.amountCad) : "");
  const [justification, setJustification] = useState(line?.justification ?? "");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const codeOptions = candidates.map((c) => ({ value: c.id, label: `${c.code} · ${c.name}` }));
  const noun = revenue ? "revenue" : "budget";

  async function submit() {
    if (busy) return;

    // Mirror of the server's checks, so the common mistake is caught without a round trip. The
    // server re-checks all of it and its answer is the one that counts.
    if (!codeId) {
      return setError(
        `Every active ${category.toLowerCase()} code already has a line in ${periodLabel}.`,
      );
    }
    const amountProblem = allocationAmountError(amount);
    if (amountProblem) return setError(amountProblem);
    const justificationProblem = allocationJustificationError(justification);
    if (justificationProblem) return setError(justificationProblem);

    const amountCad = Number(amount);
    const trimmed = justification.trim();

    setBusy(true);
    setError(null);

    try {
      await setBudgetAllocation(periodId, codeId, { amountCad, justification: trimmed });

      // The read side is a projection and trails the write — refetch until the change is visible
      // rather than assuming it already is. Compare values, not existence: on edit the row was
      // always there, so "the row exists" would be satisfied by the stale read.
      const rows = await refetchUntil(
        () => listBudgetAllocations(periodId),
        (rows) =>
          rows.some(
            (r) =>
              r.budgetCodeId === codeId && r.amountCad === amountCad && r.justification === trimmed,
          ),
      );
      onSaved(rows, codeId);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError ? e.message : `Failed to save the ${noun} line — please try again.`,
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Planning · ${periodLabel}`}
      title={editing ? `Edit ${line.code}` : revenue ? "Set revenue" : "Set budget"}
      onClose={onClose}
      error={error}
      maxWidth={620}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton
            variant="primary"
            onClick={submit}
            disabled={busy || (!editing && candidates.length === 0)}
          >
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : revenue ? "SET REVENUE" : "SET BUDGET"}
          </ActionButton>
        </>
      }
    >
      {editing ? (
        <FixedLine code={line.code} name={line.name} retired={!line.isCodeActive} />
      ) : candidates.length > 0 ? (
        <SelectField
          label="Budget code"
          value={codeId}
          onChange={setCodeId}
          options={codeOptions}
          hint={`Active ${category.toLowerCase()} codes without a line in this period`}
        />
      ) : (
        <Note>
          Every active {category.toLowerCase()} code already has a line in {periodLabel}. Edit an
          existing line instead, or add a code under Budget Codes.
        </Note>
      )}

      <div style={{ marginTop: 14 }}>
        <NumberField
          label="Amount (CAD)"
          value={amount}
          onChange={setAmount}
          min={0}
          step={1}
          placeholder="0"
          hint="Whole dollars — zero is allowed"
        />
      </div>

      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Justification"
          value={justification}
          onChange={setJustification}
          rows={4}
          placeholder={
            revenue
              ? "What this revenue rests on — contracts signed, trips booked, trailing volume."
              : "What this spend buys and why this much — kilometres, rosters, quotes in hand."
          }
          hint={`Why this amount, from zero — required · ${justification.trim().length}/${ALLOCATION_JUSTIFICATION_MAX_LENGTH}`}
        />
      </div>

      <Note>
        Zero-based: the amount may be seeded from an earlier period as a starting position, but
        the argument for it never is — last period&apos;s reasoning is not this period&apos;s.
        A copied line arrives with no justification and cannot be saved until you write one. The
        justification is stored with the line and shown on the period&apos;s dashboard.
      </Note>
    </ModalShell>
  );
}

/**
 * The code in edit mode. Deliberately not a disabled picker: a greyed field reads as "not right
 * now" when the truth is that a line is tied to its code — plan a different code as another
 * line, or remove this one.
 */
function FixedLine({ code, name, retired }: { code: string; name: string; retired: boolean }) {
  return (
    <div>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
          marginBottom: 6,
        }}
      >
        Budget code
      </div>
      <div style={{ display: "flex", alignItems: "baseline", gap: 10, padding: "8px 0" }}>
        <span style={{ fontFamily: fonts.mono, fontSize: 13, color: colors.textPrimary }}>{code}</span>
        <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>
          {name}
        </span>
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        {retired
          ? "This code is retired — restore it under Budget Codes before changing the line, or remove the line."
          : "Fixed for this line — to plan a different code, set another line or remove this one."}
      </div>
    </div>
  );
}

/** A quiet explanatory line, matching BudgetCodeFormModal's trailing note. */
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
