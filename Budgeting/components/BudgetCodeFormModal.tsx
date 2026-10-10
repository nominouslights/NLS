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
  costCentreApplies,
  createBudgetCode,
  listBudgetCodes,
  normalizeBudgetCode,
  ownerLabel,
  parentCandidates,
  refetchUntil,
  updateBudgetCode,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type BudgetCodeRecord,
  type BudgetOwnerOption,
  type CostCentreRecord,
} from "@/lib/api/budgeting";
import {
  budgetCodeCostCentreError,
  costCentreOptions,
  normalizeCostCentreCode,
} from "@/lib/costCentres";
import CostCentreFormModal, { type CostCentreApi } from "@/components/CostCentreFormModal";
import { StatusChip } from "@/components/ui/Chip";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { usePeriodHold } from "@/lib/periodHold";

// Create-and-edit modal for a budget code, following BudgetPeriodFormModal: one useState per
// field, server-side validation is authoritative, and a 409 or 400 surfaces as the backend's own
// message in the vermillion banner.
//
// Three rules this form has to carry visibly, because all three are enforced server-side and
// none is guessable from the UI:
//
//   1. **The code string is set once.** There is no rename endpoint — allocations and actuals
//      reference a code by string, so renaming would orphan every row already tagged. In edit
//      mode the code is shown as read-only text rather than a disabled input, because a greyed
//      field reads as "not right now" when the truth is "not ever".
//   2. **The hierarchy is one level deep.** The parent picker only offers top-level codes, and
//      says so — a user who cannot find a code in the list should not have to guess why.
//   3. **A revenue code has no cost centre.** A cost centre attributes cost, so the field is
//      absent rather than disabled when the category is Revenue, and a value stored before the
//      rule existed is cleared on save. `costCentreApplies` is the mirror of the server's rule.
//   4. **An expense code's cost centre comes from the register.** The field is a picker of the
//      tenant's ACTIVE cost centres (BudgetCodeCostCentreRule refuses anything else with
//      CostCentreNotFound / CostCentreRetired). The one exception the server makes is an
//      UNCHANGED value, so a code already carrying a retired or unregistered string keeps it
//      selectable and marked (lib/costCentres costCentreOptions). "+ New cost centre…" opens the
//      register's own create modal on top of this one and selects what it creates.
//
// Every code belongs to a period, so the modal writes to that period's chart
// (periods/{periodId}/codes) and says which one in its eyebrow. The parent picker is fed that
// same period's chart — a parent from another period is refused server-side (ParentNotFound). The
// save holds the period (lib/periodHold.ts) and the modal ignores ✕ and CANCEL while it runs,
// exactly as the budget-item modal does.

const CATEGORY_OPTIONS: { value: BudgetCodeCategory; label: string }[] = [
  { value: "Revenue", label: "Revenue" },
  { value: "Expense", label: "Expense" },
];

const NONE = "";
/** The picker option that opens the inline create modal instead of selecting anything. */
const NEW_COST_CENTRE = "__new_cost_centre__";

/** The requests the modal makes. A prop so the component test can inject vi.fn()s. */
export interface BudgetCodeApi {
  create: typeof createBudgetCode;
  update: typeof updateBudgetCode;
  list: typeof listBudgetCodes;
}

const DEFAULT_API: BudgetCodeApi = {
  create: createBudgetCode,
  update: updateBudgetCode,
  list: listBudgetCodes,
};

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
  periodId,
  periodLabel,
  code,
  allCodes,
  owners,
  costCentres,
  onCostCentresChanged,
  onClose,
  onSaved,
  api = DEFAULT_API,
  costCentreApi,
}: {
  /** The period whose chart this code belongs to — the entered period. */
  periodId: string;
  periodLabel: string;
  /** null → create mode; a code → edit mode (its code string is fixed). */
  code: BudgetCode | null;
  /** This period's whole chart, for the parent picker. Filtered by parentCandidates. */
  allCodes: BudgetCode[];
  owners: BudgetOwnerOption[];
  /**
   * The tenant's cost-centre register, retired entries included (the picker offers the active
   * ones and needs the retired ones to name a kept value); null while loading or after a failed
   * load — the picker then offers only the code's current value.
   */
  costCentres: CostCentreRecord[] | null;
  /** The fresh register after an inline create, so the caller's copy follows. */
  onCostCentresChanged?: (records: CostCentreRecord[]) => void;
  onClose: () => void;
  /** Fresh list (already reflecting the change) plus the affected code's id. */
  onSaved: (records: BudgetCodeRecord[], id: string) => void;
  api?: BudgetCodeApi;
  /** Passed to the inline cost-centre modal; a test seam. */
  costCentreApi?: CostCentreApi;
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
  /**
   * The register as of an inline create here — kept so the new entry is offered at once, even
   * before the caller's copy (fed back via onCostCentresChanged) arrives as the prop.
   */
  const [createdHere, setCreatedHere] = useState<CostCentreRecord[] | null>(null);
  // Derived, never copied once at mount: the caller's listCostCentres runs in parallel with the
  // modal opening and may resolve AFTER it, and the picker must follow when it does.
  const register = mergeRegister(costCentres, createdHere);
  const [creatingCostCentre, setCreatingCostCentre] = useState(false);
  usePeriodHold(busy);
  /** Inert while saving, so a result can never land on a modal that is gone. */
  const close = () => {
    if (!busy) onClose();
  };

  const normalized = normalizeBudgetCode(codeText);
  const codeError = editing ? null : budgetCodeFormatError(codeText);

  const parentOptions = [
    { value: NONE, label: "— No parent —" },
    ...parentCandidates(allCodes, code?.id ?? null).map((c) => ({
      value: c.id,
      label: `${c.code} · ${c.name}`,
    })),
  ];

  // The code's stored value, kept selectable when it is retired or unregistered — the server
  // accepts an unchanged value unconditionally (BudgetCodeCostCentreRule).
  const ccOptions = costCentreOptions(register ?? [], code?.costCentre ?? null);
  const ccSelected = ccOptions.find((o) => o.value === normalizeCostCentreCode(costCentre)) ?? null;
  const costCentreSelectOptions = [
    { value: NONE, label: "— No cost centre —" },
    ...ccOptions.map((o) => ({ value: o.value, label: o.label })),
    { value: NEW_COST_CENTRE, label: "+ New cost centre…" },
  ];

  function pickCostCentre(value: string) {
    if (value === NEW_COST_CENTRE) {
      setCreatingCostCentre(true);
      return;
    }
    setCostCentre(value);
  }

  function handleCostCentreCreated(records: CostCentreRecord[], id: string) {
    setCreatedHere(records);
    onCostCentresChanged?.(records);
    const created = records.find((r) => r.id === id);
    if (created) setCostCentre(created.code);
  }

  const ownerOptions = [
    { value: NONE, label: "— Unassigned —" },
    ...owners.map((o) => ({ value: o.userId, label: ownerLabel(o) })),
  ];

  async function submit() {
    if (busy) return;

    // Mirror of the server's required set, so the common mistake is caught without a round trip.
    // The server re-checks all of it and its answer is the one that counts.
    if (!editing && codeError) return setError(codeError);
    if (!name.trim()) return setError("Enter a name for the code.");
    // BudgetCodeCostCentreRule, caught before the round trip — only when the register loaded
    // (without it there is nothing to check against, and the server decides).
    if (costCentreApplies(category) && register !== null) {
      const ccError = budgetCodeCostCentreError(
        costCentre,
        normalizeCostCentreCode(code?.costCentre) || null,
        register,
      );
      if (ccError) return setError(ccError);
    }

    setBusy(true);
    setError(null);

    // Cleared optional fields go as null, not "" — the server stores null for blank anyway, and
    // sending null makes "cleared" unambiguous on the wire.
    const details = {
      name: name.trim(),
      description: description.trim() || null,
      category,
      serviceLine: (serviceLine || null) as BudgetServiceLine | null,
      // Revenue codes never carry a cost centre (BudgetCode.Validate rejects one). Sending null
      // rather than the hidden state also clears a value stored before this rule existed. Never
      // re-cased: cost-centre codes match case-sensitively.
      costCentre: costCentreApplies(category) ? normalizeCostCentreCode(costCentre) || null : null,
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
        await api.update(periodId, id, details);
      } else {
        id = await api.create(periodId, { code: normalized, ...details });
      }

      // The read side is a projection and trails the write by well under a second — refetch until
      // the change is visible rather than assuming it already is. On edit that means waiting for
      // the new name, not merely for the row to exist: the row was always there.
      const records = await refetchUntil(() => api.list(periodId), (rows) =>
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
    <>
    <ModalShell
      eyebrow={`Budget Codes · ${periodLabel}`}
      title={editing ? `Edit ${code.code}` : "New Budget Code"}
      onClose={close}
      error={error}
      maxWidth={680}
      footer={
        <>
          <ActionButton onClick={close} disabled={busy}>
            CANCEL
          </ActionButton>
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
          hint="Planning classification only — QuickBooks owns the rate"
        />
      </Row>

      <Row top>
        {/* A cost centre attributes cost, so a revenue code never has one — the field is not
            offered at all, and GL account code simply takes the first column. */}
        {costCentreApplies(category) && (
          <div>
            <SelectField
              label="Cost centre"
              value={ccSelected?.value ?? NONE}
              onChange={pickCostCentre}
              options={costCentreSelectOptions}
              hint={register === null ? "Optional — the register did not load" : "Optional — from the register"}
            />
            {ccSelected && ccSelected.status !== "active" && (
              <div
                data-testid="cost-centre-kept"
                style={{
                  display: "flex",
                  alignItems: "flex-start",
                  gap: 8,
                  marginTop: 6,
                  fontFamily: fonts.body,
                  fontSize: 11.5,
                  color: colors.textSecondary,
                  lineHeight: 1.5,
                }}
              >
                <StatusChip
                  kind="off"
                  label={ccSelected.status === "retired" ? "Retired" : "Not in register"}
                />
                <span>
                  Kept because it is this code&apos;s current cost centre — saving it unchanged is
                  allowed, but once changed it cannot be chosen again.
                </span>
              </div>
            )}
          </div>
        )}
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
    {/* A sibling, not a child: the shell's backdrop-filter would otherwise contain the nested
        fixed overlay inside this card. Rendered later, so it stacks on top. */}
    {creatingCostCentre && (
      <CostCentreFormModal
        entry={null}
        register={register ?? []}
        owners={owners}
        eyebrow={`Cost Centres · for ${editing ? code.code : "a new budget code"}`}
        onClose={() => setCreatingCostCentre(false)}
        onSaved={handleCostCentreCreated}
        api={costCentreApi}
      />
    )}
    </>
  );
}

/**
 * The caller's register plus any entry an inline create added that the caller's copy does not
 * have yet (matched by id; the caller's copy wins otherwise), in register (code, ordinal) order.
 * null only when neither has loaded.
 */
function mergeRegister(
  fromProp: CostCentreRecord[] | null,
  createdHere: CostCentreRecord[] | null,
): CostCentreRecord[] | null {
  if (createdHere === null) return fromProp;
  if (fromProp === null) return createdHere;
  const known = new Set(fromProp.map((c) => c.id));
  const extra = createdHere.filter((c) => !known.has(c.id));
  if (extra.length === 0) return fromProp;
  return [...fromProp, ...extra].sort((a, b) => (a.code < b.code ? -1 : a.code > b.code ? 1 : 0));
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
