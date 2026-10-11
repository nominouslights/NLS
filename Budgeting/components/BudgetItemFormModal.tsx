"use client";

import { useState, type ReactNode } from "react";
import { colors, fonts } from "@/lib/theme";
import type { BudgetCode, BudgetCodeCategory } from "@/lib/types";
import { ApiError } from "@/lib/api/transport";
import {
  allocationCandidates,
  budgetItemError,
  costBuildUpLabel,
  createBudgetItem,
  draftToBudgetItemInput,
  itemAmount,
  itemReflects,
  listBudgetAllocations,
  recordToDraft,
  refetchUntil,
  updateBudgetItem,
  BUDGET_ITEM_LIMITS,
  PRIORITY_LABELS,
  PRIORITY_ORDER,
  RECURRENCE_LABELS,
  SPEND_TYPE_LABELS,
  type BudgetAllocationRecord,
  type BudgetItemDraft,
  type BudgetItemPriority,
  type BudgetRecurrence,
  type BudgetSpendType,
  type CostMode,
  type VendorRecord,
  type VendorShareDraft,
} from "@/lib/api/budgeting";
import { PENDING_VENDOR_NAME, vendorInactiveMessage, vendorRegisterError } from "@/lib/vendorShares";
import { formatCadPrecise } from "@/lib/money";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField, SelectField, TextAreaField, TextField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { usePeriodHold } from "@/lib/periodHold";
import VendorSharesEditor from "@/components/VendorSharesEditor";
import VendorFormModal, { type VendorFormApi } from "@/components/VendorFormModal";

// Add-or-edit modal for one BUDGET ITEM, opened from the Period Dashboard's two item sections —
// the one place a period is planned. A code's budget in a period is the sum of its items, so
// there is nothing here to "set": each save adds or rewrites one item, and the code's figure
// follows.
//
// The groups, in the order a planner argues an item from zero:
//
//   What           — the code it is planned against, a title, tags — and on a REVENUE item the
//                    payer (free text; the wire field is still called `vendor`).
//   Cost           — a lump sum, OR quantity × unit cost (with a unit). The built-up total is
//                    computed live with the server's own rounding (computeItemAmount mirrors
//                    BudgetAllocation.Round), and the server computes it again and ignores any
//                    amount sent — so the modal sends amountCad: null for a built-up item.
//   Vendors        — EXPENSE items only (VendorSharesEditor): register vendors with dollar shares
//                    that cover the amount exactly, or none at all ("No vendor"). Below Cost, so
//                    the amount the split must meet is right above it; a changed amount only moves
//                    the live Remaining / Over line — it never rebalances the shares. Every save
//                    sends `vendorShares` explicitly: the whole list, or [] (also on revenue), and
//                    an expense item always sends `vendor: null` (the server refuses new free text
//                    there). "+ New vendor…" opens VendorFormModal inline and picks what it creates.
//                    A REVENUE item that still carries shares from before its code was reclassified
//                    shows them read-only ("Kept shares") and sends `vendorShares: null` — keep —
//                    until REMOVE SHARES, after which it sends [].
//   Why            — the justification (required: zero-based means argued from nothing, each
//                    period), the priority (what gets cut first), assumptions, and what happens
//                    if the item goes unfunded.
//   Classification — Operating or Capital, one-time or recurring.
//
// Validation runs client-side first (budgetItemError, a rule-for-rule mirror of
// BudgetAllocation.Validate that returns the server's own messages), and any 400/404/409 the
// server still sends is shown verbatim — its wording names the rule.
//
// The code is editable on edit too: moving an item to another active code is allowed. An item
// on a since-retired code opens with no code chosen, because the server refuses every update to
// it (CodeRetired, checked on every update) — it can only be moved or removed.
//
// While a save is in flight the modal holds the entered period (lib/periodHold.ts) and ignores
// ✕ and CANCEL: closing it mid-save used to let the save land after the planner had moved on,
// on a period they were no longer looking at, with its error shown nowhere.

/** The three requests the modal makes. A prop so the component test can inject vi.fn()s. */
export interface BudgetItemApi {
  create: typeof createBudgetItem;
  update: typeof updateBudgetItem;
  list: typeof listBudgetAllocations;
}

const DEFAULT_API: BudgetItemApi = {
  create: createBudgetItem,
  update: updateBudgetItem,
  list: listBudgetAllocations,
};

function blankDraft(budgetCodeId: string): BudgetItemDraft {
  return {
    budgetCodeId,
    title: "",
    costMode: "lump",
    amount: "",
    quantity: "",
    unitCost: "",
    unit: "",
    justification: "",
    // The server's defaults when a field is null (BudgetItemDetails): Operating, OneTime,
    // ShouldHave. Sent explicitly rather than as null so what the planner sees is what is stored.
    spendType: "Operating",
    recurrence: "OneTime",
    vendor: "",
    vendorShares: [],
    tags: "",
    priority: "ShouldHave",
    assumptions: "",
    consequenceIfUnfunded: "",
  };
}

export default function BudgetItemFormModal({
  periodId,
  periodLabel,
  category,
  codes,
  item,
  presetCodeId = null,
  onOpenCodes,
  onClose,
  onSaved,
  vendors = null,
  onVendorsChanged,
  api = DEFAULT_API,
  vendorApi,
}: {
  periodId: string;
  periodLabel: string;
  category: BudgetCodeCategory;
  /** The entered period's whole chart (codes belong to a period); filtered by allocationCandidates. */
  codes: BudgetCode[];
  /** null → add a new item; an item → edit it. */
  item: BudgetAllocationRecord | null;
  /** "+ ITEM" on a code header preselects that code. */
  presetCodeId?: string | null;
  /**
   * To Budget Codes for this period — offered when the period has no active code of this
   * category, instead of an empty picker. Optional so the modal renders on its own in a test.
   */
  onOpenCodes?: () => void;
  onClose: () => void;
  /** Fresh items, already reflecting the change. */
  onSaved: (items: BudgetAllocationRecord[]) => void;
  /**
   * The tenant's vendor register, retired included — a PROP, derived each render, because the
   * dashboard loads it in parallel and it may land after the modal opened. null while loading or
   * after a failed load: the picker then offers only the item's own vendors, and the register
   * check before submit is skipped (the server decides).
   */
  vendors?: VendorRecord[] | null;
  /** The fresh register after an inline create, so the caller's copy follows. */
  onVendorsChanged?: (records: VendorRecord[]) => void;
  api?: BudgetItemApi;
  /** Passed to the inline vendor modal; a test seam. */
  vendorApi?: VendorFormApi;
}) {
  const editing = item !== null;
  const revenue = category === "Revenue";
  const candidates = allocationCandidates(codes, category);
  const candidateIds = new Set(candidates.map((c) => c.id));

  const [draft, setDraft] = useState<BudgetItemDraft>(() => {
    if (item) {
      const d = recordToDraft(item);
      // A retired code is not a legal target for any update — start with no code chosen.
      return candidateIds.has(d.budgetCodeId) ? d : { ...d, budgetCodeId: "" };
    }
    const preset =
      presetCodeId && candidateIds.has(presetCodeId)
        ? presetCodeId
        : candidates.length === 1
          ? candidates[0].id
          : "";
    return blankDraft(preset);
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  usePeriodHold(busy);
  /** Inert while saving — see the note above. */
  const close = () => {
    if (!busy) onClose();
  };

  /** ONLY vendors created inline here — merged into the register prop by id (the cost-centre precedent). */
  const [createdHere, setCreatedHere] = useState<VendorRecord[]>([]);
  const register = mergeVendors(vendors, createdHere);
  /** The row an inline-created vendor goes into; "append" for a new row; null when closed. */
  const [creatingVendorFor, setCreatingVendorFor] = useState<number | "append" | null>(null);

  const set = <K extends keyof BudgetItemDraft>(key: K) => (value: BudgetItemDraft[K]) =>
    setDraft((d) => ({ ...d, [key]: value }));

  /** Puts a vendor into the row the inline modal was opened for (or a new row). */
  function placeVendor(vendorId: string) {
    const target = creatingVendorFor;
    setDraft((d) => {
      const rows: VendorShareDraft[] =
        typeof target === "number" && target < d.vendorShares.length
          ? d.vendorShares.map((r, i) => (i === target ? { ...r, vendorId } : r))
          : [...d.vendorShares, { vendorId, amount: "" }];
      return { ...d, vendorShares: rows };
    });
  }

  function handleVendorCreated(records: VendorRecord[] | null, id: string) {
    const created = records?.find((r) => r.id === id);
    if (!records || !created) {
      // The vendor exists, but the register could not be re-read, so there is no name to offer
      // and nothing for the pre-submit register check to find it in.
      setError(
        "The vendor was created, but the vendor list could not be reloaded. Close this item and open it again to choose it.",
      );
      return;
    }
    onVendorsChanged?.(records);
    setCreatedHere((prev) => [...prev.filter((v) => v.id !== created.id), created]);
    placeVendor(created.id);
  }

  /** The inline modal's duplicate link: pick the existing vendor — or say why a retired one cannot be. */
  function handleExistingVendor(existing: VendorRecord) {
    setCreatingVendorFor(null);
    if (existing.isActive) {
      placeVendor(existing.id);
    } else {
      setError(vendorInactiveMessage(existing.name));
    }
  }

  const parsed = draftToBudgetItemInput(draft, category);
  const liveAmount = parsed.ok ? itemAmount(parsed.input) : null;
  const buildUp =
    parsed.ok && draft.costMode === "builtUp"
      ? costBuildUpLabel({
          quantity: parsed.input.quantity,
          unitCostCad: parsed.input.unitCostCad,
          unit: parsed.input.unit,
        })
      : null;

  const codeOptions = [
    ...(draft.budgetCodeId === "" ? [{ value: "", label: "Choose a code…" }] : []),
    ...candidates.map((c) => ({ value: c.id, label: `${c.code} · ${c.name}` })),
  ];

  async function submit() {
    if (busy) return;
    if (!parsed.ok) return setError(parsed.error);
    const input = parsed.input;
    // The server's own rules and messages, in the server's order. It re-checks all of it.
    const problem = budgetItemError(input, category, item);
    if (problem) return setError(problem);
    // BudgetItemVendorRule: a vendor this save ADDS must be in the register and active — checked
    // against the loaded register (skipped while it is null), so a vendor retired since the
    // picker opened is refused in the server's words before the round trip. Kept shares (null)
    // add nothing.
    const registerProblem = vendorRegisterError(
      input.vendorShares ?? [],
      item?.vendorShares ?? [],
      register,
      category,
    );
    if (registerProblem) return setError(registerProblem);

    setBusy(true);
    setError(null);
    try {
      let id: string;
      if (item) {
        await api.update(periodId, item.id, input);
        id = item.id;
      } else {
        id = await api.create(periodId, input);
      }

      // The read side is a projection and trails the write — refetch until THIS item's new
      // values are visible rather than assuming they already are. Keyed by id and compared by
      // value: on edit the row was always there, so existence alone is satisfied by a stale read.
      const rows = await refetchUntil(
        () => api.list(periodId),
        (rows) => rows.some((r) => itemReflects(r, id, input)),
      );
      onSaved(rows);
      onClose();
    } catch (e) {
      setError(
        e instanceof ApiError ? e.message : "Failed to save the budget item — please try again.",
      );
      setBusy(false);
    }
  }

  const retiredOnEdit = item !== null && !item.isCodeActive;
  /**
   * A revenue item still carrying the shares it was split into while its code was an expense
   * code. Shown read-only and KEPT on save (vendorShares: null) until the planner removes them —
   * an unrelated edit must never wipe them silently.
   */
  const keptRevenueShares =
    revenue && item !== null && draft.vendorShares.length > 0 ? item.vendorShares : [];

  return (
    <>
    <ModalShell
      eyebrow={`Planning · ${periodLabel}`}
      title={editing ? `Edit budget item · ${item.code}` : `New ${category.toLowerCase()} item`}
      onClose={close}
      error={error}
      maxWidth={680}
      footer={
        <>
          <ActionButton onClick={close} disabled={busy}>
            CANCEL
          </ActionButton>
          <ActionButton
            variant="primary"
            onClick={() => void submit()}
            disabled={busy || candidates.length === 0}
          >
            {busy ? "SAVING…" : editing ? "SAVE CHANGES" : "ADD ITEM"}
          </ActionButton>
        </>
      }
    >
      {/* ---------------- What ---------------- */}
      <Group title="What">
        {candidates.length > 0 ? (
          <SelectField
            label="Budget code"
            value={draft.budgetCodeId}
            onChange={set("budgetCodeId")}
            options={codeOptions}
            hint={`Active ${category.toLowerCase()} codes — a code's budget is the sum of its items`}
          />
        ) : (
          <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
            <Note>
              {periodLabel} has no active {category.toLowerCase()} code to plan against. Set one
              up under Budget Codes — copy codes from an earlier period, load the starter set, or
              add or restore a code.
            </Note>
            {onOpenCodes && (
              <ActionButton
                onClick={() => {
                  if (busy) return;
                  onClose();
                  onOpenCodes();
                }}
                disabled={busy}
              >
                OPEN BUDGET CODES
              </ActionButton>
            )}
          </div>
        )}
        {retiredOnEdit && (
          <Note>
            This item is on {item.code}, which is retired. A retired code cannot take changes —
            move the item to an active code above, or remove it from the dashboard.
          </Note>
        )}
        <Row>
          <TextField
            label="Title"
            value={draft.title}
            onChange={set("title")}
            placeholder={
              revenue ? "e.g. Alamos crew rotations, October" : "e.g. Winter tires, unit NL-04"
            }
            hint={`What is this money for? · ${draft.title.trim().length}/${BUDGET_ITEM_LIMITS.titleMaxLength}`}
          />
        </Row>
        <Grid>
          {/* The payer is free text and belongs to revenue only; an expense item's vendors are
              register entries with dollar shares, in the Vendors group below Cost. */}
          {revenue && (
            <TextField
              label="Payer"
              value={draft.vendor}
              onChange={set("vendor")}
              placeholder="e.g. Alamos Gold"
              hint="Optional — who pays this revenue"
            />
          )}
          <TextField
            label="Tags"
            value={draft.tags}
            onChange={set("tags")}
            placeholder="e.g. winter, safety"
            hint={`Optional — comma-separated, up to ${BUDGET_ITEM_LIMITS.maxTags}`}
          />
        </Grid>
      </Group>

      {/* ---------------- Cost ---------------- */}
      <Group title="Cost">
        <Segmented<CostMode>
          label="How is the amount built?"
          value={draft.costMode}
          onChange={set("costMode")}
          options={[
            { value: "lump", label: "Lump sum" },
            { value: "builtUp", label: "Quantity × unit cost" },
          ]}
        />
        <Row>
          {draft.costMode === "lump" ? (
            <NumberField
              label="Amount (CAD)"
              value={draft.amount}
              onChange={set("amount")}
              min={0}
              step={0.01}
              placeholder="0.00"
              hint="Zero is allowed"
            />
          ) : (
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "1fr 1fr 1fr",
                gap: 12,
              }}
            >
              <NumberField
                label="Quantity"
                value={draft.quantity}
                onChange={set("quantity")}
                min={0}
                step={0.01}
                placeholder="e.g. 12"
              />
              <TextField
                label="Unit"
                value={draft.unit}
                onChange={set("unit")}
                placeholder="e.g. month, litre, trip"
                hint="Optional"
              />
              <NumberField
                label="Unit cost (CAD)"
                value={draft.unitCost}
                onChange={set("unitCost")}
                min={0}
                step={0.01}
                placeholder="e.g. 450"
              />
            </div>
          )}
        </Row>
        <div
          aria-live="polite"
          data-testid="item-total"
          style={{
            marginTop: 10,
            display: "flex",
            alignItems: "baseline",
            gap: 10,
            fontFamily: fonts.body,
            fontSize: 12,
            color: colors.textSecondary,
          }}
        >
          <span>Planned amount</span>
          <span
            style={{
              fontFamily: fonts.mono,
              fontSize: 14,
              fontWeight: 600,
              color: colors.textPrimary,
              fontVariantNumeric: "tabular-nums",
            }}
          >
            {liveAmount === null ? "—" : formatCadPrecise(liveAmount)}
          </span>
          {buildUp && <span style={{ color: colors.textDim }}>= {buildUp}</span>}
        </div>
        {draft.costMode === "builtUp" && (
          <Note>
            Quantity and unit cost are each rounded to two decimals, then multiplied and rounded to
            the cent — the server computes the same figure and stores it.
          </Note>
        )}
      </Group>

      {/* ---------------- Vendors (expense only) ---------------- */}
      {!revenue && (
        <Group title="Vendors">
          <VendorSharesEditor
            rows={draft.vendorShares}
            onChange={set("vendorShares")}
            amountCad={liveAmount}
            register={register}
            original={item?.vendorShares ?? []}
            onNewVendor={(rowIndex) => {
              if (!busy) setCreatingVendorFor(rowIndex ?? "append");
            }}
            disabled={busy}
          />
          {editing && item.vendor && (
            <Note>
              This item still carries the old free-text vendor &ldquo;{item.vendor}&rdquo;. Saving
              clears it — an expense item&apos;s vendors come from the vendor register, so choose
              it above instead.
            </Note>
          )}
        </Group>
      )}

      {/* ------- Vendor shares left from an expense code (revenue, read-only) ------- */}
      {keptRevenueShares.length > 0 && (
        <Group title="Vendor shares">
          <div data-testid="kept-revenue-shares">
            <div
              style={{
                display: "flex",
                alignItems: "flex-start",
                gap: 8,
                fontFamily: fonts.body,
                fontSize: 11.5,
                color: colors.textSecondary,
                lineHeight: 1.5,
              }}
            >
              <StatusChip kind="soon" label="Kept shares" />
              <span>
                Shares from when this code was an expense code — kept unless you remove them. A
                revenue item cannot take new shares, and moving it to another code needs them
                removed first.
              </span>
            </div>
            <ul
              style={{
                margin: "8px 0 0",
                paddingLeft: 18,
                fontFamily: fonts.body,
                fontSize: 12,
                color: colors.textPrimary,
                lineHeight: 1.6,
              }}
            >
              {keptRevenueShares.map((s) => (
                <li key={s.vendorId}>
                  {s.vendorName ?? PENDING_VENDOR_NAME}
                  {s.vendorName !== null && !s.isActive ? " — retired" : ""} ·{" "}
                  <span style={{ fontFamily: fonts.mono, fontVariantNumeric: "tabular-nums" }}>
                    {formatCadPrecise(s.amountCad)}
                  </span>
                </li>
              ))}
            </ul>
            <div style={{ marginTop: 8 }}>
              <ActionButton
                variant="destructive"
                onClick={() => set("vendorShares")([])}
                disabled={busy}
                style={{ padding: "5px 11px", fontSize: 12 }}
              >
                REMOVE SHARES
              </ActionButton>
            </div>
          </div>
        </Group>
      )}

      {revenue && item !== null && item.vendorShares.length > 0 && keptRevenueShares.length === 0 && (
        <Note>
          The vendor shares left from the expense code will be removed when you save. Cancel to
          keep them.
        </Note>
      )}

      {/* ---------------- Why ---------------- */}
      <Group title="Why">
        <TextAreaField
          label="Justification"
          value={draft.justification}
          onChange={set("justification")}
          rows={3}
          placeholder={
            revenue
              ? "What this revenue rests on — contracts signed, trips booked, trailing volume."
              : "What this spend buys and why this much — kilometres, rosters, quotes in hand."
          }
          hint={`Why this amount, from zero — required · ${draft.justification.trim().length}/${BUDGET_ITEM_LIMITS.justificationMaxLength}`}
        />
        <Row>
          <Segmented<BudgetItemPriority>
            label="Priority — what gets cut first"
            value={draft.priority}
            onChange={set("priority")}
            options={PRIORITY_ORDER.map((p) => ({ value: p, label: PRIORITY_LABELS[p] }))}
          />
        </Row>
        <Grid>
          <TextAreaField
            label="Assumptions"
            value={draft.assumptions}
            onChange={set("assumptions")}
            rows={2}
            placeholder="e.g. Diesel at $1.85/L, 14 rotations"
            hint="Optional — the cost drivers behind the number"
          />
          <TextAreaField
            label="If unfunded"
            value={draft.consequenceIfUnfunded}
            onChange={set("consequenceIfUnfunded")}
            rows={2}
            placeholder="e.g. NL-04 runs summer tires into November"
            hint="Optional — what happens if this is cut"
          />
        </Grid>
        {editing && item.justification.trim() === "" && (
          <Note>
            Copied from an earlier period: the amount and details came across, the argument did
            not. Write this period&apos;s justification before saving.
          </Note>
        )}
      </Group>

      {/* ---------------- Classification ---------------- */}
      <Group title="Classification" last>
        <Grid>
          <Segmented<BudgetSpendType>
            label="Spend type"
            value={draft.spendType}
            onChange={set("spendType")}
            options={(["Operating", "Capital"] as const).map((v) => ({
              value: v,
              label: SPEND_TYPE_LABELS[v],
            }))}
          />
          <Segmented<BudgetRecurrence>
            label="Recurrence"
            value={draft.recurrence}
            onChange={set("recurrence")}
            options={(["OneTime", "Recurring"] as const).map((v) => ({
              value: v,
              label: RECURRENCE_LABELS[v],
            }))}
          />
        </Grid>
        <Note>
          Stored on {periodLabel} only — other periods keep their own items. No tax is entered or
          computed here; QuickBooks owns tax.
        </Note>
      </Group>
    </ModalShell>
    {/* A sibling, not a child: the shell's backdrop-filter would otherwise contain the nested
        fixed overlay inside this card. Rendered later, so it stacks on top. */}
    {creatingVendorFor !== null && (
      <VendorFormModal
        vendor={null}
        vendors={register ?? []}
        onClose={() => setCreatingVendorFor(null)}
        onSaved={handleVendorCreated}
        onOpenExisting={handleExistingVendor}
        api={vendorApi}
      />
    )}
    </>
  );
}

/**
 * The caller's register plus any vendor created inline here that the caller's copy does not have
 * yet (matched by id; the caller's copy wins otherwise), in name order. null only when the
 * register has not loaded and nothing was created here.
 */
function mergeVendors(fromProp: VendorRecord[] | null, createdHere: VendorRecord[]): VendorRecord[] | null {
  if (createdHere.length === 0) return fromProp;
  if (fromProp === null) return createdHere;
  const known = new Set(fromProp.map((v) => v.id));
  const extra = createdHere.filter((v) => !known.has(v.id));
  if (extra.length === 0) return fromProp;
  return [...fromProp, ...extra].sort((a, b) =>
    a.name.toUpperCase() < b.name.toUpperCase() ? -1 : a.name.toUpperCase() > b.name.toUpperCase() ? 1 : 0,
  );
}

/** One titled group of the form (What / Cost / Why / Classification). */
function Group({ title, children, last = false }: { title: string; children: ReactNode; last?: boolean }) {
  return (
    <section
      aria-label={title}
      style={{
        paddingBottom: last ? 0 : 16,
        marginBottom: last ? 0 : 16,
        borderBottom: last ? undefined : `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
          marginBottom: 10,
        }}
      >
        {title}
      </div>
      {children}
    </section>
  );
}

function Row({ children }: { children: ReactNode }) {
  return <div style={{ marginTop: 12 }}>{children}</div>;
}

function Grid({ children }: { children: ReactNode }) {
  return (
    <div style={{ marginTop: 12, display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
      {children}
    </div>
  );
}

/**
 * A small segmented choice built from real buttons with aria-pressed, so the selected option is
 * announced and not carried by the fill alone. The selected segment also reads in bold.
 */
function Segmented<T extends string>({
  label,
  value,
  onChange,
  options,
}: {
  label: string;
  value: T;
  onChange: (v: T) => void;
  options: { value: T; label: string }[];
}) {
  return (
    <div>
      <div
        style={{
          fontFamily: fonts.body,
          fontSize: 11.5,
          color: colors.textLabel,
          marginBottom: 5,
        }}
      >
        {label}
      </div>
      <div role="group" aria-label={label} style={{ display: "inline-flex", flexWrap: "wrap", gap: 6 }}>
        {options.map((o) => {
          const selected = o.value === value;
          return (
            <button
              key={o.value}
              type="button"
              aria-pressed={selected}
              onClick={() => onChange(o.value)}
              style={{
                fontFamily: fonts.body,
                fontSize: 12.5,
                fontWeight: selected ? 700 : 500,
                padding: "7px 12px",
                borderRadius: 8,
                cursor: "pointer",
                border: `1px solid ${selected ? colors.blue : colors.borderStrong}`,
                background: selected ? "rgba(31,111,178,.10)" : colors.inputBg,
                color: selected ? colors.blue : colors.textSecondary,
              }}
            >
              {selected ? "✓ " : ""}
              {o.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}

/** A quiet explanatory line, matching BudgetCodeFormModal's trailing note. */
function Note({ children }: { children: ReactNode }) {
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
