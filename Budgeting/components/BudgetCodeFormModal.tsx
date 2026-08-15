"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type {
  BudgetCode,
  BudgetCodeCategory,
  BudgetReviewFrequency,
  BudgetServiceLine,
  BudgetTaxTreatment,
} from "@/lib/types";
import { ApiError } from "@/lib/api/transport";
import {
  budgetCodeFormatError,
  createBudgetCode,
  listBudgetCodes,
  normalizeBudgetCode,
  parentCandidates,
  refetchUntil,
  updateBudgetCode,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type BudgetCodeRecord,
  type BudgetOwnerOption,
} from "@/lib/api/budgeting";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";

// Create-and-edit modal for a budget code, following BudgetPeriodFormModal: one useState per
// field, server-side validation is authoritative, and a 409 or 400 surfaces as the backend's own
// message in the vermillion banner.
//
// Two rules this form has to carry visibly, because both are enforced server-side and neither is
// guessable from the UI:
//
//   1. **The code string is set once.** There is no rename endpoint — allocations and actuals
//      reference a code by string, so renaming would orphan every row already tagged. In edit
//      mode the code is shown as read-only text rather than a disabled input, because a greyed
//      field reads as "not right now" when the truth is "not ever".
//   2. **The hierarchy is one level deep.** The parent picker only offers top-level codes, and
//      says so — a user who cannot find a code in the list should not have to guess why.

const CATEGORY_OPTIONS: { value: BudgetCodeCategory; label: string }[] = [
  { value: "Revenue", label: "Revenue" },
  { value: "Expense", label: "Expense" },
];

const NONE = "";

/** Builds a "— None —" first option for the optional enum pickers. */
function optionalOptions<T extends string>(labels: Record<T, string>) {
  return [
    { value: NONE, label: "— None —" },
    ...(Object.keys(labels) as T[]).map((value) => ({ value, label: labels[value] })),
  ];
}

const SERVICE_LINE_OPTIONS = optionalOptions(SERVICE_LINE_LABELS);
const TAX_TREATMENT_OPTIONS = optionalOptions(TAX_TREATMENT_LABELS);
const REVIEW_FREQUENCY_OPTIONS = (
  Object.keys(REVIEW_FREQUENCY_LABELS) as BudgetReviewFrequency[]
).map((value) => ({ value, label: REVIEW_FREQUENCY_LABELS[value] }));

export default function BudgetCodeFormModal({
  code,
  allCodes,
  owners,
  onClose,
  onSaved,
}: {
  /** null → create mode; a code → edit mode (its code string is fixed). */
  code: BudgetCode | null;
  /** Every code, for the parent picker. Filtered by parentCandidates. */
  allCodes: BudgetCode[];
  owners: BudgetOwnerOption[];
  onClose: () => void;
  /** Fresh list (already reflecting the change) plus the affected code's id. */
  onSaved: (records: BudgetCodeRecord[], id: string) => void;
}) {
  const editing = code !== null;

  const [codeText, setCodeText] = useState(code?.code ?? "");
  const [name, setName] = useState(code?.name ?? "");
  const [description, setDescription] = useState(code?.description ?? "");
  const [category, setCategory] = useState<BudgetCodeCategory>(code?.category ?? "Expense");
  const [serviceLine, setServiceLine] = useState<string>(code?.serviceLine ?? NONE);
  const [costCentre, setCostCentre] = useState(code?.costCentre ?? "");
  const [parentCodeId, setParentCodeId] = useState<string>(code?.parentCodeId ?? NONE);
  const [glAccountCode, setGlAccountCode] = useState(code?.glAccountCode ?? "");
  const [taxTreatment, setTaxTreatment] = useState<string>(code?.taxTreatment ?? NONE);
  const [budgetOwnerUserId, setBudgetOwnerUserId] = useState<string>(code?.budgetOwnerUserId ?? NONE);
  const [reviewFrequency, setReviewFrequency] = useState<BudgetReviewFrequency>(
    code?.reviewFrequency ?? "Quarterly",
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const normalized = normalizeBudgetCode(codeText);
  const codeError = editing ? null : budgetCodeFormatError(codeText);

  const parentOptions = [
    { value: NONE, label: "— No parent —" },
    ...parentCandidates(allCodes, code?.id ?? null).map((c) => ({
      value: c.id,
      label: `${c.code} · ${c.name}`,
    })),
  ];

  const ownerOptions = [
    { value: NONE, label: "— Unassigned —" },
    ...owners.map((o) => ({ value: o.userId, label: o.email })),
  ];

  async function submit() {
    if (busy) return;

    // Mirror of the server's required set, so the common mistake is caught without a round trip.
    // The server re-checks all of it and its answer is the one that counts.
    if (!editing && codeError) return setError(codeError);
    if (!name.trim()) return setError("Enter a name for the code.");

    setBusy(true);
    setError(null);

    // Cleared optional fields go as null, not "" — the server stores null for blank anyway, and
    // sending null makes "cleared" unambiguous on the wire.
    const details = {
      name: name.trim(),
      description: description.trim() || null,
      category,
      serviceLine: (serviceLine || null) as BudgetServiceLine | null,
      costCentre: costCentre.trim() || null,
      parentCodeId: parentCodeId || null,
      glAccountCode: glAccountCode.trim() || null,
      taxTreatment: (taxTreatment || null) as BudgetTaxTreatment | null,
      budgetOwnerUserId: budgetOwnerUserId || null,
      reviewFrequency,
    };

    try {
      let id: string;
      if (editing) {
        id = code.id;
        await updateBudgetCode(id, details);
      } else {
        id = await createBudgetCode({ code: normalized, ...details });
      }

      // The read side is a projection and trails the write by well under a second — refetch until
      // the change is visible rather than assuming it already is. On edit that means waiting for
      // the new name, not merely for the row to exist: the row was always there.
      const records = await refetchUntil(listBudgetCodes, (rows) =>
        editing
          ? rows.some((r) => r.id === id && r.name === details.name)
          : rows.some((r) => r.id === id),
      );
      onSaved(records, id);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError
          ? e.message
          : `Failed to ${editing ? "save the" : "create the"} budget code — please try again.`,
      );
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow="Planning · Budget Codes"
      title={editing ? `Edit ${code.code}` : "New Budget Code"}
      onClose={onClose}
      error={error}
      maxWidth={680}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "CREATE CODE"}
          </ActionButton>
        </>
      }
    >
      <Row>
        {editing ? (
          <FixedCode code={code.code} />
        ) : (
          <TextField
            label="Code"
            value={codeText}
            onChange={setCodeText}
            mono
            maxLength={32}
            placeholder="FLEET-MAINT"
            hint="Set once — codes cannot be renamed"
          />
        )}
        <SelectField
          label="Category"
          value={category}
          onChange={(v) => setCategory(v as BudgetCodeCategory)}
          options={CATEGORY_OPTIONS}
        />
      </Row>

      {!editing && (
        <div
          style={{
            marginTop: 6,
            fontFamily: fonts.body,
            fontSize: 11.5,
            color: codeError && codeText.length > 0 ? colors.textSecondary : colors.textDim,
          }}
        >
          {codeText.length === 0 ? (
            "Letters, digits and hyphens — saved in upper case."
          ) : codeError ? (
            codeError
          ) : (
            <>
              Saves as{" "}
              <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>{normalized}</span>
            </>
          )}
        </div>
      )}

      <Row top>
        <TextField
          label="Name"
          value={name}
          onChange={setName}
          maxLength={120}
          placeholder="Alamos crew shuttle"
        />
        <SelectField
          label="Review frequency"
          value={reviewFrequency}
          onChange={(v) => setReviewFrequency(v as BudgetReviewFrequency)}
          options={REVIEW_FREQUENCY_OPTIONS}
          hint="How often this code is re-examined"
        />
      </Row>

      <Row top>
        <SelectField
          label="Service line"
          value={serviceLine}
          onChange={setServiceLine}
          options={SERVICE_LINE_OPTIONS}
          hint="Groups the code for revenue-mix reporting"
        />
        <SelectField
          label="Tax treatment"
          value={taxTreatment}
          onChange={setTaxTreatment}
          options={TAX_TREATMENT_OPTIONS}
          hint="GST 5% — no PST on transport in Manitoba"
        />
      </Row>

      <Row top>
        <TextField
          label="Cost centre"
          value={costCentre}
          onChange={setCostCentre}
          maxLength={32}
          placeholder="OPS-01"
          hint="Optional"
        />
        <TextField
          label="GL account code"
          value={glAccountCode}
          onChange={setGlAccountCode}
          mono
          maxLength={32}
          placeholder="4000"
          hint="Free text — entered manually, not checked against QuickBooks"
        />
      </Row>

      <Row top>
        <SelectField
          label="Parent code"
          value={parentCodeId}
          onChange={setParentCodeId}
          options={parentOptions}
          hint="Only top-level codes can be parents — the hierarchy is one level deep"
        />
        <SelectField
          label="Budget owner"
          value={budgetOwnerUserId}
          onChange={setBudgetOwnerUserId}
          options={ownerOptions}
          hint="Accountable person"
        />
      </Row>

      <div style={{ marginTop: 14 }}>
        <TextAreaField
          label="Description"
          value={description}
          onChange={setDescription}
          rows={3}
          placeholder="What this code covers, and what it doesn't."
        />
      </div>

      <div
        style={{
          marginTop: 10,
          fontFamily: fonts.body,
          fontSize: 11.5,
          color: colors.textDim,
        }}
      >
        The description is a standing note about what the code covers. Zero-based budgeting still
        means a code earns its place every cycle — but that justification is entered against the
        allocation each period, not here, because it is the period decision that changes.
      </div>
    </ModalShell>
  );
}

/** The modal's two-column row, matching BudgetPeriodFormModal's grid. */
function Row({ children, top = false }: { children: React.ReactNode; top?: boolean }) {
  return (
    <div
      style={{
        display: "grid",
        gridTemplateColumns: "1fr 1fr",
        gap: 14,
        marginTop: top ? 14 : 0,
      }}
    >
      {children}
    </div>
  );
}

/**
 * The code string in edit mode. Deliberately not a disabled TextField: disabled reads as
 * temporarily unavailable, and this is permanent.
 */
function FixedCode({ code }: { code: string }) {
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
        Code
      </div>
      <div
        style={{
          fontFamily: fonts.mono,
          fontSize: 13,
          color: colors.textPrimary,
          padding: "8px 0",
        }}
      >
        {code}
      </div>
      <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
        Cannot be renamed — retire this code and create a new one instead.
      </div>
    </div>
  );
}
