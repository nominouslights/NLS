import { request } from "./transport";
import type {
  BudgetCode,
  BudgetCodeCategory,
  BudgetPeriod,
  BudgetReviewFrequency,
  BudgetServiceLine,
  BudgetTaxTreatment,
  PeriodState,
} from "@/lib/types";
import type { StatusKind } from "@/lib/theme";
// The checklist's balance row writes its own signed figure, so the sign is text in the tested
// derivation rather than something a component might forget to add.
import { formatCadPrecise, formatDeltaCad } from "@/lib/money";

// ---------------------------------------------------------------------------
// Budgeting API client — contract owned by Backend/ (Budgeting module,
// BudgetingEndpoints.cs). Shapes mirror the backend's BudgetPeriodResponse,
// BudgetAllocationResponse and the request records exactly (JSON camelCase;
// enums as PascalCase strings; money as JSON numbers). The server derives
// startsOn/endsOn/label from granularity + year + ordinal — nothing here
// invents a date range. Do not invent fields — extend only when the backend
// contract changes.
// ---------------------------------------------------------------------------

/** Wire enum (PascalCase, JsonStringEnumConverter server-side). */
export type PeriodGranularity = "Month" | "Quarter";

/**
 * Mirrors BudgetPeriodResponse — list rows come from rm_budget_periods, the two totals from a
 * grouped sum over rm_budget_allocations joined to the code's category at read time.
 */
export interface BudgetPeriodRecord {
  id: string;
  label: string;
  granularity: PeriodGranularity;
  year: number;
  ordinal: number;
  /** ISO date, inclusive. */
  startsOn: string;
  /** ISO date, inclusive. */
  endsOn: string;
  state: PeriodState;
  createdAtUtc: string;
  updatedAtUtc: string;
  /** Sum of the period's lines on Revenue codes. */
  plannedRevenueCad: number;
  /** Sum of the period's lines on Expense codes. */
  plannedExpenseCad: number;
}

/**
 * The route segment of each forward transition — POST periods/{id}/{action}. One command with a
 * discriminator server-side (TransitionBudgetPeriodCommand + PeriodTransition), four routes.
 */
export type PeriodTransitionAction = "finalize" | "open" | "begin-review" | "close";

/** POST /api/budgeting/periods body (CreateBudgetPeriodRequest). */
export interface BudgetPeriodInput {
  granularity: PeriodGranularity;
  year: number;
  /** 1–12 for Month, 1–4 for Quarter. */
  ordinal: number;
}

/** Ordered by startsOn ascending server-side. */
export function listBudgetPeriods(): Promise<BudgetPeriodRecord[]> {
  return request<BudgetPeriodRecord[]>("/api/budgeting/periods");
}

/** GET periods/{id} → 200, or 404 (Budgeting.Period.NotFound). The 201 Location target. */
export function getBudgetPeriod(id: string): Promise<BudgetPeriodRecord> {
  return request<BudgetPeriodRecord>(`/api/budgeting/periods/${id}`);
}

/** POST → 201 { id } (id only; the row lands on the next projection read). */
export async function createBudgetPeriod(input: BudgetPeriodInput): Promise<string> {
  const res = await request<{ id: string }>("/api/budgeting/periods", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/**
 * POST periods/{id}/{action} → 204. A 409 names the rule broken (Budgeting.Period.NotDraft /
 * NotFinalized / NotOpen / NotInReview) — surface its message verbatim. Forward only: there is no
 * route back to an earlier state.
 */
export function transitionBudgetPeriod(id: string, action: PeriodTransitionAction): Promise<void> {
  return request<void>(`/api/budgeting/periods/${id}/${action}`, { method: "POST" });
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible.
export { refetchUntil } from "./shared";

/**
 * Period state → status kind, in the data-source file for the same reason
 * varianceKind lives in lib/data.ts: the mapping is decided once, next to the
 * data, so every screen agrees. The theme has exactly five kinds and "over"
 * means "problem", so the two pre-live states share "info": an open period is
 * the live plan ("ontime"), one in review is pending a decision ("soon"), a
 * closed one is finished ("off"). The state label and the dashboard's stepper
 * disambiguate Draft from Finalized — colour + glyph + label, never colour alone.
 */
export function periodKind(state: PeriodState): StatusKind {
  switch (state) {
    case "Open":
      return "ontime";
    case "InReview":
      return "soon";
    case "Closed":
      return "off";
    case "Draft":
    case "Finalized":
    default:
      return "info";
  }
}

/** Human labels for the wire states. Typed Record so a new state is a compile error here. */
export const PERIOD_STATE_LABELS: Record<PeriodState, string> = {
  Draft: "Draft",
  Finalized: "Finalized",
  Open: "Open",
  InReview: "In review",
  Closed: "Closed",
};

/**
 * The lifecycle, in order — mirrors the C# PeriodState enum and the forward-only table in
 * BudgetPeriod.Transition. The stepper renders exactly this list.
 */
export const PERIOD_STATE_ORDER: PeriodState[] = ["Draft", "Finalized", "Open", "InReview", "Closed"];

/**
 * Whether a period's plan — its budget items AND its chart of budget codes — may change in this
 * state. Mirrors BudgetPeriod.AllowsPlanChanges (Draft or Open), which the server checks for
 * both: 409 Budgeting.Allocation.PeriodNotEditable for an item, 409
 * Budgeting.Code.PeriodNotEditable for a code (create, edit, retire, restore, delete, the starter
 * set, and the TARGET of a codes copy). The dashboard and the Budget Codes screen hide their
 * controls and say why rather than offering an action that will be refused.
 */
export function canEditPlan(state: PeriodState): boolean {
  return state === "Draft" || state === "Open";
}

/**
 * The budget-item reading of canEditPlan — kept under its old name because the dashboard and
 * the banner read it that way. One rule, one function: this never diverges from canEditPlan.
 */
export function canEditAllocations(state: PeriodState): boolean {
  return canEditPlan(state);
}

/** The one forward step available from a state, as the dashboard's context-sensitive button. */
export interface PeriodTransitionOption {
  action: PeriodTransitionAction;
  /** Button text at rest. */
  label: string;
  /** Button text once clicked, awaiting the confirming second click. */
  confirmLabel: string;
}

/**
 * Mirrors the transition table in BudgetPeriod.Transition: Finalize needs Draft, Open needs
 * Finalized, BeginReview needs Open, Close needs InReview; Closed is terminal (null).
 */
export function nextTransition(state: PeriodState): PeriodTransitionOption | null {
  switch (state) {
    case "Draft":
      return { action: "finalize", label: "FINALIZE PLAN", confirmLabel: "CONFIRM FINALIZE" };
    case "Finalized":
      return { action: "open", label: "OPEN PERIOD", confirmLabel: "CONFIRM OPEN" };
    case "Open":
      return { action: "begin-review", label: "BEGIN REVIEW", confirmLabel: "CONFIRM REVIEW" };
    case "InReview":
      return { action: "close", label: "CLOSE PERIOD", confirmLabel: "CONFIRM CLOSE" };
    case "Closed":
    default:
      return null;
  }
}

/** The state a successful transition lands in — the refetch predicate after the POST. */
export function stateAfter(action: PeriodTransitionAction): PeriodState {
  switch (action) {
    case "finalize":
      return "Finalized";
    case "open":
      return "Open";
    case "begin-review":
      return "InReview";
    case "close":
      return "Closed";
  }
}

/** Wire record → the view shape the screens render. Only the two totals drop their Cad suffix. */
export function toBudgetPeriod(r: BudgetPeriodRecord): BudgetPeriod {
  return {
    id: r.id,
    label: r.label,
    startsOn: r.startsOn,
    endsOn: r.endsOn,
    state: r.state,
    pk: periodKind(r.state),
    plannedRevenue: r.plannedRevenueCad,
    plannedExpense: r.plannedExpenseCad,
  };
}

// ---------------------------------------------------------------------------
// Budget items — a code's budget in a period is the SUM of its items, and a
// period holds any number of items per code. Each item is a small zero-based
// decision package (what, how much and how it was built up, why, how badly it
// is needed). Nothing stores a code-level figure any more: the old "one line
// per (period, code), upserted by code" shape — and the overwrite-one-figure
// button it implied — is gone.
//
// The backend keeps the aggregate name BudgetAllocation and the `allocations`
// route segment; the console calls them budget items. Mirrors
// BudgetAllocationResponse and BudgetItemRequest (BudgetingEndpoints.cs).
// Category, name and service line are resolved from the CODE at read time,
// never snapshotted, so a re-classified code moves its items between totals.
// Items follow the period lifecycle only — there is no per-item approval.
// ---------------------------------------------------------------------------

/** Mirrors C# BudgetSpendType (JsonStringEnumConverter — PascalCase names on the wire). */
export type BudgetSpendType = "Operating" | "Capital";

/** Mirrors C# BudgetRecurrence. */
export type BudgetRecurrence = "OneTime" | "Recurring";

/** Mirrors C# BudgetItemPriority — the zero-based ranking a planner cuts from the bottom of. */
export type BudgetItemPriority = "MustHave" | "ShouldHave" | "NiceToHave";

/** Mirrors BudgetAllocationResponse — rendered directly, no view rename. */
export interface BudgetAllocationRecord {
  id: string;
  periodId: string;
  budgetCodeId: string;
  /** The code string as it was when the item was assigned to its current code. */
  code: string;
  name: string;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  /**
   * False once the code is retired — the item stays and still counts, but every update is refused
   * (CodeRetired, checked on EVERY update) until it is moved to an active code, or it is removed.
   */
  isCodeActive: boolean;
  /** What the money is for. */
  title: string;
  /** Rounded to cents. Equal to round(quantity × unitCostCad) when the item is built up. */
  amountCad: number;
  /** Null on a lump-sum item; set iff unitCostCad is. */
  quantity: number | null;
  unitCostCad: number | null;
  /** Null on a lump-sum item (the server drops a unit sent without a quantity). */
  unit: string | null;
  /** "" on an item copied from an earlier period and not yet argued (NeedsJustification). */
  justification: string;
  spendType: BudgetSpendType;
  recurrence: BudgetRecurrence;
  vendor: string | null;
  /** Never null. */
  tags: string[];
  priority: BudgetItemPriority;
  assumptions: string | null;
  consequenceIfUnfunded: string | null;
  createdBy: string | null;
  createdByEmail: string | null;
  modifiedBy: string | null;
  modifiedByEmail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/**
 * POST periods/{id}/allocations and PUT periods/{id}/allocations/{allocationId} body
 * (BudgetItemRequest). Everything is nullable on the server's side of the wire; this app always
 * sends every key, with explicit nulls, so "cleared" never reads as "absent". Enums go as their
 * PascalCase names.
 *
 * Cost is EITHER a lump sum (`amountCad`, quantity and unit cost null) OR built up (`quantity` +
 * `unitCostCad`, both or neither) — in which case the server computes the amount and ignores any
 * amount sent, so this app sends `amountCad: null` for a built-up item.
 */
export interface BudgetItemInput {
  budgetCodeId: string;
  title: string;
  amountCad: number | null;
  quantity: number | null;
  unitCostCad: number | null;
  unit: string | null;
  justification: string;
  spendType: BudgetSpendType;
  recurrence: BudgetRecurrence;
  vendor: string | null;
  tags: string[];
  priority: BudgetItemPriority;
  assumptions: string | null;
  consequenceIfUnfunded: string | null;
}

/**
 * GET → 200, ordered server-side by code, then priority (MustHave first), then createdAtUtc, then
 * id; 404 when the period does not exist.
 */
export function listBudgetAllocations(periodId: string): Promise<BudgetAllocationRecord[]> {
  return request<BudgetAllocationRecord[]>(`/api/budgeting/periods/${periodId}/allocations`);
}

/**
 * POST → 201 { id } (id only; the row lands on the next projection read). 400 on validation (the
 * BUDGET_ITEM_MESSAGES below, among others), 404 for an unknown period or code, 409
 * Budgeting.Allocation.PeriodNotEditable / CodeRetired — show the server's message verbatim.
 */
export async function createBudgetItem(periodId: string, input: BudgetItemInput): Promise<string> {
  const res = await request<{ id: string }>(`/api/budgeting/periods/${periodId}/allocations`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/**
 * PUT → 204. Rewrites the whole item, and its code may change (moving an item to another code is
 * allowed; the new code must be active). 404 Budgeting.Allocation.NotFound when the item is not in
 * this period; 409 CodeRetired is checked on EVERY update, even when the code is unchanged — an
 * item on a since-retired code can only be moved to an active code or removed.
 */
export function updateBudgetItem(
  periodId: string,
  allocationId: string,
  input: BudgetItemInput,
): Promise<void> {
  return request<void>(`/api/budgeting/periods/${periodId}/allocations/${allocationId}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** DELETE → 204; 404 when the item is not in this period; 409 PeriodNotEditable outside Draft/Open. */
export function removeBudgetItem(periodId: string, allocationId: string): Promise<void> {
  return request<void>(`/api/budgeting/periods/${periodId}/allocations/${allocationId}`, {
    method: "DELETE",
  });
}

// ---------------------------------------------------------------------------
// Budget item rules — client mirrors of BudgetAllocation.Validate and
// BudgetAllocation.Round, so the common mistake is caught without a round
// trip. The server re-checks all of it and its answer is the one that counts;
// every message below is the server's own text, so the modal says the same
// thing whichever side caught it.
// ---------------------------------------------------------------------------

/** Mirrors the BudgetAllocation length and count constants. */
export const BUDGET_ITEM_LIMITS = {
  titleMaxLength: 120,
  justificationMaxLength: 1000,
  unitMaxLength: 32,
  vendorMaxLength: 120,
  assumptionsMaxLength: 1000,
  consequenceMaxLength: 1000,
  maxTags: 10,
  tagMaxLength: 32,
} as const;

/** Mirrors BudgetAllocation.JustificationMaxLength. */
export const ALLOCATION_JUSTIFICATION_MAX_LENGTH = BUDGET_ITEM_LIMITS.justificationMaxLength;

/**
 * Mirrors BudgetAllocation.AmountMax — the inclusive numeric(12,2) ceiling, applied to the amount,
 * the quantity and the unit cost alike.
 */
export const ALLOCATION_AMOUNT_MAX = 999_999_999.99;

/**
 * The server's messages, verbatim, keyed by the error code's suffix (every code is prefixed
 * "Budgeting.Allocation."). Mirrors BudgetAllocationErrors.
 */
export const BUDGET_ITEM_MESSAGES = {
  CodeRequired: "Choose the budget code this item is planned against.",
  TitleRequired: "Give the budget item a title — what is this money for?",
  TitleTooLong: "The title must be 120 characters or fewer.",
  QuantityWithoutUnitCost:
    "Quantity and unit cost go together: enter both, or leave both blank and enter a lump-sum amount.",
  QuantityNotPositive: "The quantity must be greater than zero.",
  QuantityTooLarge: "The quantity must be 999,999,999.99 or less.",
  UnitCostNegative: "The unit cost cannot be negative.",
  UnitCostTooLarge: "The unit cost must be 999,999,999.99 or less.",
  AmountRequired: "Enter an amount, or a quantity and a unit cost. Zero is allowed.",
  AmountNegative: "The amount cannot be negative.",
  AmountTooLarge: "The amount must be 999,999,999.99 or less.",
  JustificationRequired:
    "Every allocation needs a justification — this is zero-based budgeting, so each line is argued from zero.",
  JustificationTooLong: "The justification must be 1000 characters or fewer.",
  SpendTypeInvalid: "The spend type must be Operating or Capital.",
  RecurrenceInvalid: "The recurrence must be OneTime or Recurring.",
  PriorityInvalid: "The priority must be MustHave, ShouldHave or NiceToHave.",
  UnitTooLong: "The unit must be 32 characters or fewer.",
  VendorTooLong: "The vendor must be 120 characters or fewer.",
  TagInvalid: "Each tag must be 1 to 32 characters.",
  TooManyTags: "A budget item can carry at most 10 tags.",
  AssumptionsTooLong: "The assumptions must be 1000 characters or fewer.",
  ConsequenceTooLong: "The consequence if unfunded must be 1000 characters or fewer.",
} as const;

/**
 * A number as an integer count of hundredths, rounded half AWAY FROM ZERO — the integer half of
 * BudgetAllocation.Round (`Math.Round(value, 2, MidpointRounding.AwayFromZero)`).
 *
 * Never `Math.round(value * 100)`: `1.005 * 100` is 100.49999999999999 in binary floating point
 * and rounds the wrong way. Instead the decimal point is shifted in the number's own shortest
 * decimal spelling (`String(1.005)` is "1.005", so "1.005e2" parses to exactly 100.5). That
 * spelling is also exactly what JSON.stringify puts on the wire and what the server parses into a
 * C# decimal, so this rounds the same digits the server rounds. Math.round is half-UP, which is
 * half-away-from-zero only for non-negative values — hence the rounding on the absolute value.
 */
function toHundredths(value: number): number {
  const abs = Math.abs(value);
  const [mantissa, exponent] = String(abs).split("e");
  const shifted = Number(`${mantissa}e${Number(exponent ?? 0) + 2}`);
  const rounded = Math.round(shifted);
  return value < 0 && rounded !== 0 ? -rounded : rounded;
}

/**
 * Mirrors BudgetAllocation.Round: two decimal places, half away from zero. 1.005 → 1.01,
 * 0.005 → 0.01, 0.004 → 0.
 */
export function roundCad(value: number): number {
  return toHundredths(value) / 100;
}

/**
 * The amount the server computes for a built-up item, mirroring BudgetAllocation.Parse: quantity
 * and unit cost are EACH rounded to hundredths first (BudgetAllocation.Round), then
 * `Round(q × u)`. The product is taken in exact integer arithmetic (BigInt hundredths ×
 * hundredths = ten-thousandths), so no floating-point error reaches the final rounding. 3 × 1.005
 * is therefore 3 × 1.01 = 3.03, exactly as the server stores it.
 */
export function computeItemAmount(quantity: number, unitCostCad: number): number {
  const product = BigInt(toHundredths(quantity)) * BigInt(toHundredths(unitCostCad));
  const negative = product < BigInt(0);
  const abs = negative ? -product : product;
  // ten-thousandths → hundredths, half away from zero.
  const cents = (abs + BigInt(50)) / BigInt(100);
  const signed = Number(cents) / 100;
  return negative && signed !== 0 ? -signed : signed;
}

/**
 * The amount the server will store for this input, or null when it cannot be computed yet (a
 * missing figure). Built up → computeItemAmount; lump sum → roundCad(amountCad). Drives the
 * modal's live total and the post-save refetch predicate.
 */
export function itemAmount(input: Pick<BudgetItemInput, "amountCad" | "quantity" | "unitCostCad">): number | null {
  if (input.quantity !== null && input.unitCostCad !== null) {
    return computeItemAmount(input.quantity, input.unitCostCad);
  }
  if (input.quantity !== null || input.unitCostCad !== null) return null;
  return input.amountCad === null ? null : roundCad(input.amountCad);
}

/** Mirrors BudgetAllocation.Normalize: blank optional text is null, anything else is trimmed. */
function normalizeOptional(value: string | null): string | null {
  return value === null || value.trim() === "" ? null : value.trim();
}

/**
 * The tags the server will store, mirroring the tag loop in BudgetAllocation.Parse: each trimmed,
 * de-duplicated case-insensitively with the FIRST spelling winning. Returns null when a tag is
 * invalid (empty after trimming, or longer than 32) — TagInvalid. The count limit is checked
 * separately, after de-duplication, by budgetItemError.
 */
export function normalizeTags(tags: string[]): string[] | null {
  const kept: string[] = [];
  const seen = new Set<string>();
  for (const raw of tags) {
    const tag = raw.trim();
    if (tag.length === 0 || tag.length > BUDGET_ITEM_LIMITS.tagMaxLength) return null;
    // OrdinalIgnoreCase compares by upper-casing; toUpperCase is the closest JS equivalent.
    const key = tag.toUpperCase();
    if (!seen.has(key)) {
      seen.add(key);
      kept.push(tag);
    }
  }
  return kept;
}

/**
 * The tag field's text → a tag list. Comma-separated; blank segments (a trailing comma, ", ,")
 * are artefacts of typing a list, not tags anyone meant, so they are dropped here rather than
 * sent to be refused as TagInvalid. What is left goes to the server untouched, which trims and
 * de-duplicates it.
 */
export function parseTagText(text: string): string[] {
  return text
    .split(",")
    .map((t) => t.trim())
    .filter((t) => t.length > 0);
}

const SPEND_TYPES: readonly BudgetSpendType[] = ["Operating", "Capital"];
const RECURRENCES: readonly BudgetRecurrence[] = ["OneTime", "Recurring"];

/**
 * The first rule this item breaks, as the server's own message, or null when it would be
 * accepted. Mirrors the create/update handlers' CodeRequired check and then
 * BudgetAllocation.Validate (Parse) rule for rule, IN THE SAME ORDER — one error at a time, so
 * the message the modal shows before the round trip is the one the server would have sent:
 *
 *   code → title → cost (quantity/unit cost both-or-neither, quantity, unit cost, computed
 *   amount — or the lump sum) → justification → spend type → recurrence → priority → unit →
 *   vendor → tags (each, then the count after de-duplication) → assumptions → consequence.
 *
 * The quantity is rounded BEFORE it is checked (0.004 is refused as not positive, 0.005 is 0.01
 * and fine); the lump sum is checked BEFORE it is rounded (999,999,999.994 is too large even
 * though it would round to the ceiling). Both asymmetries are the server's.
 */
export function budgetItemError(input: BudgetItemInput): string | null {
  const m = BUDGET_ITEM_MESSAGES;
  const L = BUDGET_ITEM_LIMITS;

  if (!input.budgetCodeId) return m.CodeRequired;

  if (input.title.trim().length === 0) return m.TitleRequired;
  if (input.title.trim().length > L.titleMaxLength) return m.TitleTooLong;

  if ((input.quantity === null) !== (input.unitCostCad === null)) return m.QuantityWithoutUnitCost;

  if (input.quantity !== null && input.unitCostCad !== null) {
    const q = roundCad(input.quantity);
    if (q <= 0) return m.QuantityNotPositive;
    if (q > ALLOCATION_AMOUNT_MAX) return m.QuantityTooLarge;
    const u = roundCad(input.unitCostCad);
    if (u < 0) return m.UnitCostNegative;
    if (u > ALLOCATION_AMOUNT_MAX) return m.UnitCostTooLarge;
    if (computeItemAmount(input.quantity, input.unitCostCad) > ALLOCATION_AMOUNT_MAX) {
      return m.AmountTooLarge;
    }
  } else {
    if (input.amountCad === null) return m.AmountRequired;
    if (input.amountCad < 0) return m.AmountNegative;
    if (input.amountCad > ALLOCATION_AMOUNT_MAX) return m.AmountTooLarge;
  }

  if (input.justification.trim().length === 0) return m.JustificationRequired;
  if (input.justification.trim().length > L.justificationMaxLength) return m.JustificationTooLong;

  if (!SPEND_TYPES.includes(input.spendType)) return m.SpendTypeInvalid;
  if (!RECURRENCES.includes(input.recurrence)) return m.RecurrenceInvalid;
  if (!PRIORITY_ORDER.includes(input.priority)) return m.PriorityInvalid;

  if ((normalizeOptional(input.unit)?.length ?? 0) > L.unitMaxLength) return m.UnitTooLong;
  if ((normalizeOptional(input.vendor)?.length ?? 0) > L.vendorMaxLength) return m.VendorTooLong;

  const tags = normalizeTags(input.tags);
  if (tags === null) return m.TagInvalid;
  if (tags.length > L.maxTags) return m.TooManyTags;

  if ((normalizeOptional(input.assumptions)?.length ?? 0) > L.assumptionsMaxLength) {
    return m.AssumptionsTooLong;
  }
  if ((normalizeOptional(input.consequenceIfUnfunded)?.length ?? 0) > L.consequenceMaxLength) {
    return m.ConsequenceTooLong;
  }
  return null;
}

/** How the item modal's cost is entered. Client-only — the wire has no mode, only which fields are null. */
export type CostMode = "lump" | "builtUp";

/** The item modal's raw field text, before parsing. */
export interface BudgetItemDraft {
  budgetCodeId: string;
  title: string;
  costMode: CostMode;
  amount: string;
  quantity: string;
  unitCost: string;
  unit: string;
  justification: string;
  spendType: BudgetSpendType;
  recurrence: BudgetRecurrence;
  vendor: string;
  /** Comma-separated. */
  tags: string;
  priority: BudgetItemPriority;
  assumptions: string;
  consequenceIfUnfunded: string;
}

/**
 * Number field text → number, null for blank, or NaN for text that is not a number (the number
 * input can still hand over "1e" or "-" mid-typing). Client-only: the server never sees text.
 */
function parseNumberText(text: string): number | null {
  if (text.trim() === "") return null;
  const n = Number(text);
  return Number.isFinite(n) ? n : Number.NaN;
}

/**
 * The modal's draft → the exact request body, or the reason it cannot be built. Only the mode's
 * own fields are sent: a lump sum sends quantity, unit cost and unit as null; a built-up item sends
 * amountCad as null (the server would ignore it anyway). Text is trimmed and blank optionals are
 * null, matching what the server stores. The only messages of this app's own are for text that is
 * not a number at all; everything else is left to budgetItemError, in the server's words.
 */
export function draftToBudgetItemInput(
  draft: BudgetItemDraft,
): { ok: true; input: BudgetItemInput } | { ok: false; error: string } {
  const builtUp = draft.costMode === "builtUp";
  const amount = builtUp ? null : parseNumberText(draft.amount);
  const quantity = builtUp ? parseNumberText(draft.quantity) : null;
  const unitCost = builtUp ? parseNumberText(draft.unitCost) : null;
  if (Number.isNaN(amount)) return { ok: false, error: "Enter the amount as a number." };
  if (Number.isNaN(quantity)) return { ok: false, error: "Enter the quantity as a number." };
  if (Number.isNaN(unitCost)) return { ok: false, error: "Enter the unit cost as a number." };

  return {
    ok: true,
    input: {
      budgetCodeId: draft.budgetCodeId,
      title: draft.title.trim(),
      amountCad: amount,
      quantity,
      unitCostCad: unitCost,
      unit: builtUp ? normalizeOptional(draft.unit) : null,
      justification: draft.justification.trim(),
      spendType: draft.spendType,
      recurrence: draft.recurrence,
      vendor: normalizeOptional(draft.vendor),
      tags: parseTagText(draft.tags),
      priority: draft.priority,
      assumptions: normalizeOptional(draft.assumptions),
      consequenceIfUnfunded: normalizeOptional(draft.consequenceIfUnfunded),
    },
  };
}

/** An existing item → the modal's draft (edit mode). A copied item opens with an empty justification. */
export function recordToDraft(item: BudgetAllocationRecord): BudgetItemDraft {
  const builtUp = item.quantity !== null && item.unitCostCad !== null;
  return {
    budgetCodeId: item.budgetCodeId,
    title: item.title,
    costMode: builtUp ? "builtUp" : "lump",
    amount: builtUp ? "" : String(item.amountCad),
    quantity: builtUp ? String(item.quantity) : "",
    unitCost: builtUp ? String(item.unitCostCad) : "",
    unit: item.unit ?? "",
    justification: item.justification,
    spendType: item.spendType,
    recurrence: item.recurrence,
    vendor: item.vendor ?? "",
    tags: item.tags.join(", "),
    priority: item.priority,
    assumptions: item.assumptions ?? "",
    consequenceIfUnfunded: item.consequenceIfUnfunded ?? "",
  };
}

/**
 * The refetch predicate after a save: the projection row for THIS item id already carries the
 * values just sent. Values, not existence — on edit the row was always there, so "the id is in the
 * list" is satisfied by the stale read. The amount is compared within half a cent, because it is
 * the server's computation and a float that prints the same must not hang the retry loop.
 */
export function itemReflects(
  record: BudgetAllocationRecord,
  id: string,
  input: BudgetItemInput,
): boolean {
  const expected = itemAmount(input);
  return (
    record.id === id &&
    record.budgetCodeId === input.budgetCodeId &&
    record.title === input.title &&
    record.justification === input.justification &&
    record.priority === input.priority &&
    (expected === null || Math.abs(record.amountCad - expected) < 0.005)
  );
}

// ---------------------------------------------------------------------------
// Presentation helpers for items: labels, the priority chip, grouping by code
// and the by-priority breakdown.
// ---------------------------------------------------------------------------

/**
 * The server's order of priorities (the list is sorted MustHave → ShouldHave → NiceToHave within a
 * code). Also the order the by-priority breakdown reads in: top to bottom is keep to cut.
 */
export const PRIORITY_ORDER: readonly BudgetItemPriority[] = ["MustHave", "ShouldHave", "NiceToHave"];

export const PRIORITY_LABELS: Record<BudgetItemPriority, string> = {
  MustHave: "Must have",
  ShouldHave: "Should have",
  NiceToHave: "Nice to have",
};

/**
 * Priority → status kind. A priority is a ranking, not a verdict, so none of the three is "over"
 * (a problem) or "ontime" (a success): Must and Should stay informational, and Nice to have is
 * "off" — the grey of "first to cut". Two priorities share a colour, so each carries its own glyph
 * (the StatusChip `glyph` override exists for exactly this) and its written label: the chip is
 * never colour alone, and never colour plus a shared glyph.
 */
export const PRIORITY_KINDS: Record<BudgetItemPriority, StatusKind> = {
  MustHave: "info",
  ShouldHave: "info",
  NiceToHave: "off",
};

export const PRIORITY_GLYPHS: Record<BudgetItemPriority, string> = {
  MustHave: "M",
  ShouldHave: "S",
  NiceToHave: "N",
};

export const SPEND_TYPE_LABELS: Record<BudgetSpendType, string> = {
  Operating: "Operating",
  Capital: "Capital",
};

export const RECURRENCE_LABELS: Record<BudgetRecurrence, string> = {
  OneTime: "One-time",
  Recurring: "Recurring",
};

/**
 * A sum of CAD figures, added in integer cents so eleven items never total $1,234.5600000000002.
 * Only ever used for client-side sub-totals (a code group, a priority bucket, a section header) —
 * the dashboard's headline tiles stay the server's own totals.
 */
export function sumCad(amounts: number[]): number {
  return amounts.reduce((cents, a) => cents + toHundredths(a), 0) / 100;
}

/**
 * "12 month × $450" / "3 × $1.01" for a built-up item; null for a lump sum. The unit is shown only
 * when there is one.
 */
export function costBuildUpLabel(
  item: Pick<BudgetAllocationRecord, "quantity" | "unitCostCad" | "unit">,
): string | null {
  if (item.quantity === null || item.unitCostCad === null) return null;
  const qty = item.quantity.toLocaleString("en-CA", { maximumFractionDigits: 2 });
  const unit = item.unit ? ` ${item.unit}` : "";
  return `${qty}${unit} × ${formatCadPrecise(item.unitCostCad)}`;
}

/** One code's items on the dashboard: the code header row, then its items beneath. */
export interface ItemCodeGroup {
  budgetCodeId: string;
  code: string;
  name: string;
  serviceLine: BudgetServiceLine | null;
  isCodeActive: boolean;
  /** Client-side sum of the group's items — the code's budget, which is exactly that sum. */
  subtotalCad: number;
  items: BudgetAllocationRecord[];
}

/**
 * Items → one group per code, in first-appearance order (the server lists by code, so the groups
 * come out by code), each group's items in the server's order (priority, then creation). Nothing
 * is re-sorted here: the server's order is the contract.
 */
export function groupItemsByCode(items: BudgetAllocationRecord[]): ItemCodeGroup[] {
  const groups = new Map<string, ItemCodeGroup>();
  for (const item of items) {
    let group = groups.get(item.budgetCodeId);
    if (!group) {
      group = {
        budgetCodeId: item.budgetCodeId,
        code: item.code,
        name: item.name,
        serviceLine: item.serviceLine,
        isCodeActive: item.isCodeActive,
        subtotalCad: 0,
        items: [],
      };
      groups.set(item.budgetCodeId, group);
    }
    group.items.push(item);
  }
  return [...groups.values()].map((g) => ({
    ...g,
    subtotalCad: sumCad(g.items.map((i) => i.amountCad)),
  }));
}

/** One row of the by-priority breakdown. */
export interface PriorityBucket {
  priority: BudgetItemPriority;
  label: string;
  count: number;
  totalCad: number;
}

/**
 * Totals and counts per priority, in PRIORITY_ORDER, every priority present even at zero — so a
 * planner sees at a glance how much sits in "Nice to have", i.e. what to cut first. Summed
 * client-side from the loaded items; it is a breakdown, never a replacement for the server's
 * period totals.
 */
export function priorityBreakdown(items: BudgetAllocationRecord[]): PriorityBucket[] {
  return PRIORITY_ORDER.map((priority) => {
    const inBucket = items.filter((i) => i.priority === priority);
    return {
      priority,
      label: PRIORITY_LABELS[priority],
      count: inBucket.length,
      totalCad: sumCad(inBucket.map((i) => i.amountCad)),
    };
  });
}

// ---------------------------------------------------------------------------
// Copy an earlier period's plan into this one — Ramsey's step 5, "make the new
// budget before the month begins", without rebuilding eleven lines by hand.
// Mirrors CopyBudgetAllocationsCommandHandler and BudgetAllocationCopyResult.
// ---------------------------------------------------------------------------

/**
 * Mirrors BudgetAllocationCopyResult. The server guarantees
 * `copied + skippedAlreadyPlanned + skippedRetiredCode === sourceLineCount` — a skip nobody
 * counted reads as data loss, so copyOutcomeSummary reports every bucket.
 */
export interface BudgetAllocationCopyResult {
  copied: number;
  /** Source lines whose code this period already plans — skipped, never overwritten. */
  skippedAlreadyPlanned: number;
  /**
   * Source lines with no ACTIVE code of the same string in the target period — the code was
   * retired there, or was never copied into it. (Field name kept from when codes were
   * tenant-wide; the server kept it too.)
   */
  skippedRetiredCode: number;
  sourceLineCount: number;
}

/**
 * The source period, as an object rather than a bare second string. Two same-typed guids in a
 * `copy(a, b)` signature swap silently and copy backwards — with a 200 on the wire and no error
 * on either side — which is the same hazard setBudgetCodeActive's two-route shape exists to
 * catch. Naming the source at every call site is the whole point of the wrapper.
 */
export interface BudgetAllocationCopySource {
  sourcePeriodId: string;
}

/**
 * POST periods/{targetPeriodId}/allocations/copy → 200 with the four counts. Errors, all shown
 * verbatim: 400 Budgeting.Allocation.CopySourceRequired / CopySourceIsTarget, 404
 * Budgeting.Allocation.CopySourceNotFound (the SOURCE) or Budgeting.Period.NotFound (the TARGET),
 * 409 Budgeting.Allocation.PeriodNotEditable when the target is not Draft or Open.
 *
 * An empty source period is a 200 with all four counts zero, not an error — the button worked.
 */
export function copyBudgetAllocations(
  targetPeriodId: string,
  source: BudgetAllocationCopySource,
): Promise<BudgetAllocationCopyResult> {
  return request<BudgetAllocationCopyResult>(
    `/api/budgeting/periods/${targetPeriodId}/allocations/copy`,
    { method: "POST", body: JSON.stringify({ sourcePeriodId: source.sourcePeriodId }) },
  );
}

/**
 * The periods that may be offered as a copy source, mirroring
 * CopyBudgetAllocationsCommandHandler's guards. Only ONE thing is excluded: the target itself
 * (BudgetAllocationErrors.CopySourceIsTarget).
 *
 * **A Closed period IS offered, and so is every other state.** The server checks
 * `AllowsPlanChanges` on the TARGET only — there is a deliberate, documented absence of any
 * editability check on the source, because copying a closed period's plan into a fresh Draft is
 * the entire point of the feature. That asymmetry is the thing a reader gets backwards; do not
 * "fix" it by filtering on canEditAllocations here.
 *
 * Order is the caller's (the server lists periods by startsOn ascending).
 */
export function copySourceCandidates(
  periods: BudgetPeriod[],
  targetPeriodId: string,
): BudgetPeriod[] {
  return periods.filter((p) => p.id !== targetPeriodId);
}

/**
 * The source pre-selected in the picker: the latest candidate that starts before the target,
 * i.e. "last period" — falling back to the latest candidate of any date, then to none. Purely a
 * convenience; the planner can pick any candidate.
 */
export function defaultCopySource(
  periods: BudgetPeriod[],
  targetPeriodId: string,
): BudgetPeriod | null {
  const candidates = copySourceCandidates(periods, targetPeriodId);
  if (candidates.length === 0) return null;
  const target = periods.find((p) => p.id === targetPeriodId) ?? null;
  const earlier = target ? candidates.filter((p) => p.startsOn < target.startsOn) : [];
  const pool = earlier.length > 0 ? earlier : candidates;
  return pool.reduce((a, b) => (a.startsOn >= b.startsOn ? a : b));
}

/** "1 item" / "3 items" — used by every clause of copyOutcomeSummary and the checklist. */
export function itemCount(n: number): string {
  return `${n} ${n === 1 ? "item" : "items"}`;
}

/**
 * What a completed copy is told back to the planner, in plain sentences. Every bucket the server
 * reports is named, because the invariant
 * `copied + skippedAlreadyPlanned + skippedRetiredCode === sourceLineCount` is only reassuring
 * if the user can see it adds up. Clauses for a zero skip are omitted rather than written as
 * "0 skipped", and an empty source says so rather than reading as a failure.
 *
 * The counts are per ITEM. "Already planned" means the item's CODE already has at least one item
 * here — the whole code is skipped, which is what makes a second copy copy nothing.
 */
export function copyOutcomeSummary(result: BudgetAllocationCopyResult): string {
  if (result.sourceLineCount === 0) {
    return "That period has no items to copy — nothing was added.";
  }

  const skips: string[] = [];
  if (result.skippedAlreadyPlanned > 0) {
    skips.push(
      `${itemCount(result.skippedAlreadyPlanned)} on codes already planned here, left untouched`,
    );
  }
  if (result.skippedRetiredCode > 0) {
    // Codes belong to a period, so a source item maps to THIS period's code with the same string;
    // the bucket counts items with no active such code here (retired, or never copied over).
    skips.push(
      `${itemCount(result.skippedRetiredCode)} with no active code of the same string here`,
    );
  }

  const head =
    result.copied === 0
      ? "Nothing was copied"
      : `Copied ${itemCount(result.copied)}, each with no justification yet`;
  const tail = skips.length > 0 ? ` — skipped ${skips.join(" and ")}.` : ".";
  return `${head}${tail} ${itemCount(result.sourceLineCount)} in the source period.`;
}

/**
 * Whether an item still has to be argued. Mirrors BudgetAllocation.NeedsJustification
 * (`Justification.Length == 0`), which is exactly what BudgetAllocation.CopyInto produces — but
 * trimmed here to match Validate's `IsNullOrWhiteSpace`, so an item of spaces counts as unargued
 * on both sides rather than only on the save that would refuse it.
 */
export function needsJustification(line: BudgetAllocationRecord): boolean {
  return line.justification.trim().length === 0;
}

/** The period's items that still carry no argument — the checklist's count and the finalize warning. */
export function unjustifiedLines(lines: BudgetAllocationRecord[]): BudgetAllocationRecord[] {
  return lines.filter(needsJustification);
}

/**
 * The codes the item modal's picker may offer, mirroring the CodeRetired check in
 * Create/UpdateBudgetAllocationCommandHandler: active codes of the section's category, in the
 * caller's order (the chart, by code).
 *
 * **A code that already has items is offered.** A period holds any number of items per code (the
 * unique (period, code) index is gone), so excluding planned codes — as this did while a code had
 * exactly one line — would now make a second item on a code impossible. On edit, an item on a
 * since-retired code is deliberately NOT given its own code back: the server refuses every update
 * to it (CodeRetired, checked on every update), so the only saves that can succeed are a move to
 * one of these codes, or removing the item.
 */
export function allocationCandidates(
  codes: BudgetCode[],
  category: BudgetCodeCategory,
): BudgetCode[] {
  return codes.filter((c) => c.active && c.category === category);
}

// ---------------------------------------------------------------------------
// "Left to assign" — Ramsey's step 3, subtract until you reach zero.
//
// This REPLACES the old netCad / netKind / netLabel trio, which were deleted
// rather than adapted, because the semantics invert: netKind(0) was "info" (a
// balanced plan is the neutral case) and balanced is now "ontime" (under
// zero-based budgeting, $0 left is the GOAL). Two tiles doing the same
// arithmetic with opposite colour semantics — surplus = good vs. unassigned =
// work still to do — is the contradiction that makes a console untrustworthy,
// so there is one tile and one derivation. Their tests were deleted for the
// same reason: adapting an assertion through a semantic inversion hides the
// inversion.
// ---------------------------------------------------------------------------

/**
 * Planned revenue that has not been given a job yet, in CAD. Positive means dollars are still
 * unassigned; negative means the plan assigns more than it plans to earn. Arithmetically the old
 * `netCad`, read the other way round: zero is the target, not a happy surplus.
 */
export function leftToAssignCad(period: BudgetPeriod): number {
  return period.plannedRevenue - period.plannedExpense;
}

/**
 * The four states of a plan's balance. `left === 0` on its own is **ambiguous** — an empty
 * period and a perfectly balanced one both sit at zero — and a tile reading "All assigned ✓"
 * over a period with nothing in it is a lie. So `empty` is classified first, off the period's
 * own server totals, which also means the tile needs nothing from the async lines fetch.
 */
export type AssignmentState = "empty" | "balanced" | "unassigned" | "over";

export function assignmentState(period: BudgetPeriod): AssignmentState {
  if (period.plannedRevenue === 0 && period.plannedExpense === 0) return "empty";
  const left = leftToAssignCad(period);
  if (left === 0) return "balanced";
  return left > 0 ? "unassigned" : "over";
}

/**
 * State → status kind. Typed Record so a new state is a compile error here rather than a blank
 * tile. `balanced → "ontime"` is the deliberate inversion of the deleted `netKind(0) === "info"`.
 */
export const ASSIGNMENT_KINDS: Record<AssignmentState, StatusKind> = {
  empty: "info",
  balanced: "ontime",
  unassigned: "soon",
  over: "over",
};

/** The written label beside the colour and the glyph — the tile is never colour alone. */
export const ASSIGNMENT_LABELS: Record<AssignmentState, string> = {
  empty: "Nothing planned yet",
  balanced: "All assigned",
  unassigned: "To assign",
  over: "Over-assigned",
};

/**
 * Whether the plan balances to exactly zero. True for `balanced` ONLY — an **empty plan is not
 * balanced**, even though its arithmetic also lands on zero. That is precisely what makes the
 * finalize warning fire on an untouched period instead of congratulating it.
 */
export function planBalanced(period: BudgetPeriod): boolean {
  return assignmentState(period) === "balanced";
}

/**
 * How much of a category's chart is planned: `planned` active codes carry at least one item, out
 * of `active` codes in the category. A code with five items counts once — coverage is about codes,
 * not items. Items on retired codes are not counted — they cannot be updated, only moved or
 * removed, so they are not "coverage" a planner can still act on.
 */
export function coverage(
  lines: BudgetAllocationRecord[],
  codes: BudgetCode[],
  category: BudgetCodeCategory,
): { planned: number; active: number } {
  const activeIds = new Set(codes.filter((c) => c.active && c.category === category).map((c) => c.id));
  const planned = new Set(
    lines
      .filter((l) => l.category === category && activeIds.has(l.budgetCodeId))
      .map((l) => l.budgetCodeId),
  ).size;
  return { planned, active: activeIds.size };
}

// ---------------------------------------------------------------------------
// The zero-based checklist. Four rows, mapped onto the five steps of zero-based
// budgeting as they apply to a shuttle and cargo company:
//
//   1. List income            → "Revenue items planned"
//   2. List expenses          → "Expense items planned" (every item carries
//                               a required justification, which is stricter
//                               than most ZBB tools; a code's budget is the
//                               sum of its items — nothing is "set")
//   3. Subtract to reach zero → "Every dollar assigned"  ← the row this file
//                               existed without for too long
//   4. Track all month        → NO ROW. Actuals are still mock (lib/data.ts);
//                               a row that could never turn green is worse
//                               than an honest gap.
//   5. New budget each period → the lifecycle stepper plus the copy panel
//
// "Every item argued" is the fifth row and belongs to step 2: a copied item
// arrives with its amount and no argument, so the count of unargued items is
// what keeps the copy button from quietly turning ZBB into rollover budgeting.
//
// Ramsey's household scaffolding (Giving, the Four Walls, the Baby Steps) does
// NOT map onto a shuttle company, so there are no household tiers here. The
// item's own priority (Must / Should / Nice to have) is a different thing — a
// zero-based decision-package ranking — and is shown as a breakdown on the
// dashboard (priorityBreakdown), not as a checklist row.
//
// Each row carries its own `kind` and `status`, so the colour decision lives in
// this one tested function and PlanningChecklist renders branch-free.
// ---------------------------------------------------------------------------

export type ChecklistStepId = "revenue" | "expense" | "balance" | "argued";

/** A row of the zero-based checklist. */
export interface ChecklistStep {
  group: "checklist";
  id: ChecklistStepId;
  label: string;
  /** The sentence beside the label — counts, coverage, and what is left to do. */
  detail: string;
  /** Status kind for the row's chip. Decided here, never in the component. */
  kind: StatusKind;
  /** The chip's written label, so the row survives grayscale. */
  status: string;
}

export type LifecycleStepStatus = "done" | "current" | "pending";

/** One stepper node per lifecycle state, in PERIOD_STATE_ORDER. */
export interface LifecycleStep {
  group: "lifecycle";
  id: PeriodState;
  label: string;
  status: LifecycleStepStatus;
}

export type PlanningStep = ChecklistStep | LifecycleStep;

/** The balance row's sentence, one per assignment state. */
const BALANCE_DETAILS: Record<AssignmentState, (left: string) => string> = {
  empty: () => "Nothing planned yet — list the income first, then give every dollar a job.",
  balanced: (left) => `Left to assign ${left} — every planned dollar has a job.`,
  unassigned: (left) => `Left to assign ${left} — give the rest a job.`,
  over: (left) => `Left to assign ${left} — this plan assigns more than it plans to earn.`,
};

/**
 * Where the planner stands: the four zero-based checklist rows (revenue, expense, balance,
 * argued) followed by the five lifecycle states, each done / current / pending relative to the
 * period's state in PERIOD_STATE_ORDER. Pure, so the dashboard's checklist and stepper are one
 * tested derivation rather than several ad hoc ones.
 */
export function planningProgress(
  period: BudgetPeriod,
  lines: BudgetAllocationRecord[],
  codes: BudgetCode[],
): PlanningStep[] {
  const lineStep = (
    id: "revenue" | "expense",
    category: BudgetCodeCategory,
    label: string,
  ): ChecklistStep => {
    const count = lines.filter((l) => l.category === category).length;
    const cov = coverage(lines, codes, category);
    return {
      group: "checklist",
      id,
      label,
      detail: `${itemCount(count)} · ${cov.planned} of ${cov.active} active ${category.toLowerCase()} codes planned`,
      kind: count > 0 ? "ontime" : "info",
      status: count > 0 ? "Done" : "Pending",
    };
  };

  const state = assignmentState(period);
  const balanceStep: ChecklistStep = {
    group: "checklist",
    id: "balance",
    label: "Every dollar assigned",
    detail: BALANCE_DETAILS[state](formatDeltaCad(leftToAssignCad(period))),
    kind: ASSIGNMENT_KINDS[state],
    status: ASSIGNMENT_LABELS[state],
  };

  const unargued = unjustifiedLines(lines).length;
  const arguedStep: ChecklistStep = {
    group: "checklist",
    id: "argued",
    label: "Every item argued",
    detail:
      lines.length === 0
        ? "No items yet — nothing to argue."
        : unargued > 0
          ? `${unargued} of ${itemCount(lines.length)} still need a justification — a copied item brings its amount, not its argument.`
          : `All ${itemCount(lines.length)} carry a justification.`,
    kind: lines.length === 0 ? "info" : unargued > 0 ? "soon" : "ontime",
    status: lines.length === 0 ? "Pending" : unargued > 0 ? "Needs work" : "Done",
  };

  const currentIndex = PERIOD_STATE_ORDER.indexOf(period.state);
  const lifecycle: LifecycleStep[] = PERIOD_STATE_ORDER.map((state, i) => ({
    group: "lifecycle",
    id: state,
    label: PERIOD_STATE_LABELS[state],
    status: i < currentIndex ? "done" : i === currentIndex ? "current" : "pending",
  }));

  return [
    lineStep("revenue", "Revenue", "Revenue items planned"),
    lineStep("expense", "Expense", "Expense items planned"),
    balanceStep,
    arguedStep,
    ...lifecycle,
  ];
}

/** English month names, matching the backend's invariant-culture labels. */
export const MONTH_NAMES: string[] = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

/**
 * Client-side mirror of the server's derivation, for the modal's live preview
 * line only — the server result is what gets stored. Returns null when the
 * inputs are out of range (year 2020–2100; ordinal 1–12 / 1–4), which the
 * modal treats as "not submittable yet".
 */
export function previewPeriod(
  granularity: PeriodGranularity,
  year: number,
  ordinal: number,
): { label: string; startsOn: string; endsOn: string } | null {
  if (!Number.isInteger(year) || year < 2020 || year > 2100) return null;
  if (!Number.isInteger(ordinal)) return null;

  const iso = (y: number, m: number, d: number) =>
    `${y}-${String(m).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
  // Day 0 of the next month = the last day of this one. Local Date math only —
  // toISOString would shift the day west of UTC (see lib/period.ts).
  const lastDay = (y: number, m: number) => new Date(y, m, 0).getDate();

  if (granularity === "Month") {
    if (ordinal < 1 || ordinal > 12) return null;
    return {
      label: `${MONTH_NAMES[ordinal - 1]} ${year}`,
      startsOn: iso(year, ordinal, 1),
      endsOn: iso(year, ordinal, lastDay(year, ordinal)),
    };
  }

  if (ordinal < 1 || ordinal > 4) return null;
  const firstMonth = (ordinal - 1) * 3 + 1;
  const endMonth = firstMonth + 2;
  return {
    label: `FY${year} Q${ordinal}`,
    startsOn: iso(year, firstMonth, 1),
    endsOn: iso(year, endMonth, lastDay(year, endMonth)),
  };
}

// ---------------------------------------------------------------------------
// Budget codes — the chart of accounts every dollar is tagged to. Mirrors the
// backend's BudgetCodeResponse and the Create/UpdateBudgetCodeRequest records.
//
// Each period has its OWN chart (routes under periods/{id}/codes): the same
// code string — FUEL, say — exists once per period, as a separate row with its
// own id. The code STRING is the cross-period identity (items copy by it,
// reports and future actuals join on it), which is why it stays immutable.
// The response shape carries no periodId: the caller always knows the period,
// because it is in the route it asked.
// ---------------------------------------------------------------------------

/** Mirrors BudgetCodeResponse — list rows come from rm_budget_codes. */
export interface BudgetCodeRecord {
  id: string;
  code: string;
  name: string;
  description: string | null;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  costCentre: string | null;
  parentCodeId: string | null;
  /** Resolved server-side from parentCodeId on every read, so it cannot go stale. */
  parentCode: string | null;
  parentName: string | null;
  glAccountCode: string | null;
  taxTreatment: BudgetTaxTreatment | null;
  budgetOwnerUserId: string | null;
  /** Null when that user has not set a profile name — render the email instead. */
  budgetOwnerName: string | null;
  budgetOwnerEmail: string | null;
  reviewFrequency: BudgetReviewFrequency;
  isActive: boolean;
  createdBy: string | null;
  createdByName: string | null;
  createdByEmail: string | null;
  modifiedBy: string | null;
  modifiedByName: string | null;
  modifiedByEmail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/**
 * POST /api/budgeting/periods/{id}/codes body (CreateBudgetCodeRequest) — the period comes
 * from the route, never the body. Optional fields go on the wire as
 * null rather than being omitted, so a cleared field reads as "cleared" and not "unchanged".
 */
export interface BudgetCodeInput {
  code: string;
  name: string;
  description: string | null;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  costCentre: string | null;
  parentCodeId: string | null;
  glAccountCode: string | null;
  taxTreatment: BudgetTaxTreatment | null;
  budgetOwnerUserId: string | null;
  reviewFrequency: BudgetReviewFrequency;
}

/**
 * PUT /api/budgeting/periods/{id}/codes/{codeId} body (UpdateBudgetCodeRequest). No `code`: the code string is
 * set once at creation and is not renameable server-side, because allocations and actuals
 * reference it by string. A mistyped code is retire-and-recreate, not a rename.
 */
export type BudgetCodeUpdateInput = Omit<BudgetCodeInput, "code">;

/** Mirrors BudgetOwnerOptionResponse — the owner picker's options. */
export interface BudgetOwnerOption {
  userId: string;
  /** Always present, and the only identifier guaranteed to be unique. */
  email: string;
  role: string;
  /** Null until that user sets one in Settings -> Profile. */
  fullName: string | null;
}

/**
 * How one owner reads in the picker: "Lea Fontaine (lea@northernlink.ca)", or the bare email for
 * anyone who has not set a name. Both halves stay visible on purpose — the name is what a person
 * recognizes, and the email is what tells two similar names apart.
 */
export function ownerLabel(owner: BudgetOwnerOption): string {
  const name = owner.fullName?.trim();
  return name ? `${name} (${owner.email})` : owner.email;
}

/**
 * The display value for a user id resolved server-side: their name if they have one, else their
 * email, else a caller-supplied placeholder. The one place the fallback is spelled out for the
 * budget-code screen's owner and audit rows.
 */
export function userDisplay(
  name: string | null,
  email: string | null,
  fallback: string,
): string {
  return name?.trim() || email?.trim() || fallback;
}

/**
 * GET periods/{periodId}/codes — that period's chart, ordered by code ascending server-side,
 * retired codes included. Allowed in every period state (reads are never gated). 404
 * Budgeting.Period.NotFound for an unknown period.
 */
export function listBudgetCodes(periodId: string): Promise<BudgetCodeRecord[]> {
  return request<BudgetCodeRecord[]>(`/api/budgeting/periods/${periodId}/codes`);
}

/**
 * The tenant's users, from Budgeting's replica of Identity's accounts. Tenant-wide on purpose —
 * it lists the PEOPLE who can own a code, not codes, so it is the one codes route that did not
 * move under a period.
 */
export function listBudgetOwnerCandidates(): Promise<BudgetOwnerOption[]> {
  return request<BudgetOwnerOption[]>("/api/budgeting/codes/owners");
}

// Every code write below answers, in this order: 404 Budgeting.Period.NotFound, then 409
// Budgeting.Code.PeriodNotEditable ("A period's budget codes can only change while it is Draft
// or Open.") — the console hides the controls first (canEditPlan), and shows the server's words
// verbatim if it ever gets there anyway.

/** POST periods/{periodId}/codes → 201 { id } (id only; the row lands on the next projection read). */
export async function createBudgetCode(periodId: string, input: BudgetCodeInput): Promise<string> {
  const res = await request<{ id: string }>(`/api/budgeting/periods/${periodId}/codes`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT periods/{periodId}/codes/{codeId} → 204. */
export function updateBudgetCode(
  periodId: string,
  codeId: string,
  input: BudgetCodeUpdateInput,
): Promise<void> {
  return request<void>(`/api/budgeting/periods/${periodId}/codes/${codeId}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/**
 * POST periods/{periodId}/codes/{codeId}/activate|deactivate → 204. Two routes rather than a
 * body flag, matching the backend: retiring a code is a flag flip, never a delete, so the
 * period's items on it keep resolving.
 */
export function setBudgetCodeActive(
  periodId: string,
  codeId: string,
  active: boolean,
): Promise<void> {
  return request<void>(
    `/api/budgeting/periods/${periodId}/codes/${codeId}/${active ? "activate" : "deactivate"}`,
    { method: "POST" },
  );
}

/**
 * DELETE periods/{periodId}/codes/{codeId} → 204, or 409 when the code has children or has
 * budget items in this period (Budgeting.Code.InUse). Retirement is the normal path; this is only
 * for a code created in error. The server's 409 message names retirement as the alternative, so
 * surfacing it verbatim is the right handling.
 */
export function deleteBudgetCode(periodId: string, codeId: string): Promise<void> {
  return request<void>(`/api/budgeting/periods/${periodId}/codes/${codeId}`, { method: "DELETE" });
}

/**
 * POST periods/{periodId}/codes/starter-set → 200 { created }. Idempotent per period: a second
 * call creates nothing and returns 0.
 */
export function seedStarterBudgetCodes(periodId: string): Promise<{ created: number }> {
  return request<{ created: number }>(`/api/budgeting/periods/${periodId}/codes/starter-set`, {
    method: "POST",
  });
}

// ---------------------------------------------------------------------------
// Copy codes from another period. Codes belong to a period (re-justified from
// zero each period, architecture §5.3), so a fresh period starts with an empty
// chart — this seeds it from any other period's chart in one step.
// Mirrors CopyBudgetCodesCommandHandler and BudgetCodeCopyResponse.
// ---------------------------------------------------------------------------

/**
 * Mirrors BudgetCodeCopyResponse. The server guarantees
 * `copied + skippedExisting + skippedRetired === sourceCodeCount` — codeCopyOutcomeSummary
 * reports every bucket so the planner can see it add up.
 */
export interface BudgetCodeCopyResult {
  /** Copied as ACTIVE codes with new ids, every descriptive field and the hierarchy included. */
  copied: number;
  /** Source codes whose string this period already has (active or retired) — never overwritten. */
  skippedExisting: number;
  /** Retired source codes — not copied. A code both retired and existing counts here. */
  skippedRetired: number;
  sourceCodeCount: number;
}

/**
 * The source period as an object, for the reason BudgetAllocationCopySource gives: two
 * same-typed guids in a bare `(a, b)` signature swap silently and copy backwards with a 200.
 */
export interface BudgetCodeCopySource {
  sourcePeriodId: string;
}

/**
 * POST periods/{targetPeriodId}/codes/copy → 200 with the four counts. Errors, all shown
 * verbatim, in the server's guard order: 400 Budgeting.Code.CopySourceRequired, 400
 * Budgeting.Code.CopySourceIsTarget, then the TARGET's 404 Budgeting.Period.NotFound / 409
 * Budgeting.Code.PeriodNotEditable, then 404 Budgeting.Code.CopySourceNotFound (the SOURCE).
 * A Closed source is allowed — editability is checked on the target only, as for items.
 *
 * An empty source chart is a 200 with all four counts zero, not an error.
 */
export function copyBudgetCodes(
  targetPeriodId: string,
  source: BudgetCodeCopySource,
): Promise<BudgetCodeCopyResult> {
  return request<BudgetCodeCopyResult>(`/api/budgeting/periods/${targetPeriodId}/codes/copy`, {
    method: "POST",
    body: JSON.stringify({ sourcePeriodId: source.sourcePeriodId }),
  });
}

/** "1 code" / "3 codes" — every clause of codeCopyOutcomeSummary. */
export function codeCount(n: number): string {
  return `${n} ${n === 1 ? "code" : "codes"}`;
}

/**
 * What a completed codes copy is told back to the planner. Same shape as copyOutcomeSummary:
 * every non-zero bucket named, zero skips omitted, an empty source reported as a success.
 *
 * The source picker and its default are copySourceCandidates / defaultCopySource — the codes copy
 * has exactly the items copy's guards (only CopySourceIsTarget narrows the list; a Closed source
 * is legal), so the two share one tested rule rather than two that could drift.
 */
export function codeCopyOutcomeSummary(result: BudgetCodeCopyResult): string {
  if (result.sourceCodeCount === 0) {
    return "That period has no budget codes to copy — nothing was added.";
  }

  const skips: string[] = [];
  if (result.skippedExisting > 0) {
    skips.push(`${codeCount(result.skippedExisting)} already in this period, left untouched`);
  }
  if (result.skippedRetired > 0) {
    skips.push(`${codeCount(result.skippedRetired)} retired there, not copied`);
  }

  const head =
    result.copied === 0
      ? "Nothing was copied"
      : `Copied ${codeCount(result.copied)} as active codes, hierarchy included`;
  const tail = skips.length > 0 ? ` — skipped ${skips.join(" and ")}.` : ".";
  return `${head}${tail} ${codeCount(result.sourceCodeCount)} in the source period.`;
}

/**
 * Client-side mirror of the server's code normalization (BudgetCode.NormalizeCode), for the
 * modal's live preview only — the server result is what gets stored. Trim + upper case, so a
 * planner typing "fleet-maint" sees the "FLEET-MAINT" that will actually be saved.
 */
export function normalizeBudgetCode(code: string): string {
  return code.trim().toUpperCase();
}

/**
 * Whether a code string can be saved at all, mirroring BudgetCode.ValidateCode: 1–32 characters,
 * letters/digits/hyphens only, no leading or trailing hyphen. Client-side so the modal can
 * explain the problem before a round trip; the server re-checks and is authoritative.
 */
export const BUDGET_CODE_MAX_LENGTH = 32;

export function budgetCodeFormatError(code: string): string | null {
  const normalized = normalizeBudgetCode(code);
  if (normalized.length === 0) return "Enter a code.";
  if (normalized.length > BUDGET_CODE_MAX_LENGTH) {
    return `The code must be ${BUDGET_CODE_MAX_LENGTH} characters or fewer.`;
  }
  if (!hasValidCodeFormat(normalized)) {
    return "Use letters, digits and hyphens only, starting and ending with a letter or digit.";
  }
  return null;
}

/**
 * Mirrors BudgetCode.HasValidCodeFormat, which takes an ALREADY-NORMALIZED code: non-empty, ASCII
 * letters/digits/hyphens only (char.IsAsciiLetterOrDigit — "É" is refused), and no leading or
 * trailing hyphen. Upper-case letters only, because the input is normalized first. Shared by the
 * code form and the vendor form's default budget code, which the server checks with the same
 * method.
 */
export function hasValidCodeFormat(normalizedCode: string): boolean {
  return /^[A-Z0-9]([A-Z0-9-]*[A-Z0-9])?$/.test(normalizedCode);
}

/**
 * Whether a cost centre applies to a code of this category. Mirrors the
 * CostCentreNotAllowedForRevenue rule in BudgetCode.Validate
 * (BudgetCodeErrors.CostCentreNotAllowedForRevenue) — a cost centre attributes cost, so revenue
 * codes do not carry one. The form hides the field, and the detail panel hides the row, on the
 * strength of this.
 *
 * Written as === "Expense" rather than !== "Revenue" deliberately: a future third category has
 * to make a deliberate choice here rather than silently inherit a cost-centre field.
 */
export function costCentreApplies(category: BudgetCodeCategory): boolean {
  return category === "Expense";
}

/**
 * Category → status kind, here rather than in the screen for the same reason as periodKind: the
 * mapping is decided once, next to the data, so every screen agrees. Revenue is money coming in
 * ("ontime"); an expense is neither good nor bad on its own, so it stays informational ("info").
 * Retirement is rendered separately — an inactive code shows its own "off" chip beside this one.
 */
export function budgetCodeCategoryKind(category: BudgetCodeCategory): StatusKind {
  return category === "Revenue" ? "ontime" : "info";
}

// Label maps, typed as Record<Union, string> on purpose: adding a member to one of the wire
// unions then becomes a compile error here rather than a blank cell at runtime. These are the
// highest-drift-risk lines in the app.

export const SERVICE_LINE_LABELS: Record<BudgetServiceLine, string> = {
  ContractCrew: "Mine crew shuttle",
  Community: "Community passenger",
  Nihb: "NIHB medical",
  Charter: "Charter",
  Cargo: "Parcel / cargo",
  Grocery: "Grocery run",
  Fleet: "Fleet",
  Administrative: "Administrative",
  Apprenticeship: "Apprenticeship",
};

export const TAX_TREATMENT_LABELS: Record<BudgetTaxTreatment, string> = {
  GstApplicable: "GST applicable",
  ZeroRated: "Zero-rated",
  Exempt: "Exempt",
  NotApplicable: "N/A",
};

export const REVIEW_FREQUENCY_LABELS: Record<BudgetReviewFrequency, string> = {
  Monthly: "Monthly",
  Quarterly: "Quarterly",
  Annual: "Annual",
};

/**
 * The codes that may legally be picked as a parent, mirroring BudgetCodeParentRule: never the
 * code being edited, and never a code that already has a parent (the hierarchy is one level
 * deep). Pure and exported so it can be unit-tested without a DOM — and so the picker cannot
 * offer an option the server will reject.
 */
export function parentCandidates(codes: BudgetCode[], editingId: string | null): BudgetCode[] {
  return codes.filter((c) => c.id !== editingId && c.parentCodeId === null);
}

/** Wire record → the view shape the screens render. The only rename is isActive → active. */
export function toBudgetCode(r: BudgetCodeRecord): BudgetCode {
  return {
    id: r.id,
    code: r.code,
    name: r.name,
    description: r.description,
    category: r.category,
    serviceLine: r.serviceLine,
    costCentre: r.costCentre,
    parentCodeId: r.parentCodeId,
    parentCode: r.parentCode,
    parentName: r.parentName,
    glAccountCode: r.glAccountCode,
    taxTreatment: r.taxTreatment,
    budgetOwnerUserId: r.budgetOwnerUserId,
    budgetOwnerName: r.budgetOwnerName,
    budgetOwnerEmail: r.budgetOwnerEmail,
    reviewFrequency: r.reviewFrequency,
    active: r.isActive,
    createdByName: r.createdByName,
    createdByEmail: r.createdByEmail,
    modifiedByName: r.modifiedByName,
    modifiedByEmail: r.modifiedByEmail,
  };
}

// ---------------------------------------------------------------------------
// Vendors — the tenant's vendor register. TENANT-WIDE, NOT PER PERIOD: a vendor is the same
// counterparty in every period (unlike a budget code), so these routes sit directly under
// /api/budgeting, never under periods/{id}. Same BudgetAccess group as every other route here.
// Shapes mirror VendorRequest / VendorResponse in BudgetingEndpoints.cs and
// Application/Vendors/VendorResponse.cs. The pure mirrors of Vendor's rules live in
// lib/vendors.ts.
// ---------------------------------------------------------------------------

/** Mirrors VendorResponse. Optional text is null when blank, never "". */
export interface VendorRecord {
  id: string;
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  notes: string | null;
  /** Reference data only — the platform never computes tax from it. */
  gstRegistrationNumber: string | null;
  /** The vendor's DisplayName in QuickBooks, reserved as the match key for a future import. */
  qboDisplayName: string | null;
  /** A budget code STRING (normalized), not an id — not required to exist in any period. */
  defaultBudgetCode: string | null;
  isActive: boolean;
  /** User ids only — the response resolves no names; the screen looks them up in codes/owners. */
  createdBy: string | null;
  modifiedBy: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/**
 * VendorRequest — the body of both POST vendors and PUT vendors/{id}. PUT is a FULL REPLACE: an
 * omitted optional field is cleared server-side, so every key is always sent, null when blank.
 */
export interface VendorInput {
  name: string;
  contactName: string | null;
  email: string | null;
  phone: string | null;
  address: string | null;
  notes: string | null;
  gstRegistrationNumber: string | null;
  qboDisplayName: string | null;
  defaultBudgetCode: string | null;
}

/**
 * GET vendors?includeInactive= — ordered by name (case-insensitively) server-side. The server
 * lists active vendors only unless asked; the screen asks for everything, because the duplicate-
 * name check has to see retired vendors too (a retired name still blocks a new one).
 */
export function listVendors(includeInactive = false): Promise<VendorRecord[]> {
  return request<VendorRecord[]>(`/api/budgeting/vendors?includeInactive=${includeInactive}`);
}

/** GET vendors/{id} → 200, or 404 Budgeting.Vendor.NotFound. The 201 Location target. */
export function getVendor(id: string): Promise<VendorRecord> {
  return request<VendorRecord>(`/api/budgeting/vendors/${id}`);
}

/**
 * POST vendors → 201 { id } (the row lands on the next projection read). 400 for a field rule
 * (lib/vendors.ts VENDOR_MESSAGES), 409 Budgeting.Vendor.DuplicateName naming the vendor that
 * already holds the name — surface it verbatim.
 */
export async function createVendor(input: VendorInput): Promise<string> {
  const res = await request<{ id: string }>("/api/budgeting/vendors", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT vendors/{id} → 204. Full replace — see VendorInput. 400 / 404 / 409 DuplicateName. */
export function updateVendor(id: string, input: VendorInput): Promise<void> {
  return request<void>(`/api/budgeting/vendors/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/**
 * POST vendors/{id}/activate|deactivate → 204. A route built from a boolean, like
 * setBudgetCodeActive: an inverted ternary would answer 204 either way and silently retire the
 * vendor the planner asked to restore — the request test pins both directions.
 */
export function setVendorActive(id: string, active: boolean): Promise<void> {
  return request<void>(`/api/budgeting/vendors/${id}/${active ? "activate" : "deactivate"}`, {
    method: "POST",
  });
}

/**
 * DELETE vendors/{id} → 204, or 409 Budgeting.Vendor.InUse once anything references the vendor.
 * The console cannot know usage client-side, so it offers Delete and shows that 409 verbatim —
 * its message names retirement as the alternative.
 */
export function deleteVendor(id: string): Promise<void> {
  return request<void>(`/api/budgeting/vendors/${id}`, { method: "DELETE" });
}

// ---------------------------------------------------------------------------
// Cost centres — the tenant's register of organisational units and bases that
// cost is attributed to. TENANT-WIDE, not under a period: a cost centre
// outlives every period's chart. A budget code still carries its cost centre as
// the register entry's CODE STRING (BudgetCode.CostCentre), validated against
// this register on create/edit (BudgetCodeCostCentreRule). Mirrors
// CostCentreResponse, CostCentreRequest and CostCentreRollupResponse
// (BudgetingEndpoints.cs, Application/CostCentres/). The pure client-side
// mirrors of the register's rules live in lib/costCentres.ts.
// ---------------------------------------------------------------------------

/** Mirrors CostCentreResponse. The `…Code`/`…Name`/`…Email` companions are resolved server-side on every read. */
export interface CostCentreRecord {
  id: string;
  /** Trimmed, case PRESERVED (never upper-cased); set once at creation. */
  code: string;
  name: string;
  description: string | null;
  ownerUserId: string | null;
  /** Null until that user sets a name — render the email instead (userDisplay). */
  ownerName: string | null;
  ownerEmail: string | null;
  parentId: string | null;
  parentCode: string | null;
  parentName: string | null;
  isActive: boolean;
  createdBy: string | null;
  createdByName: string | null;
  createdByEmail: string | null;
  modifiedBy: string | null;
  modifiedByName: string | null;
  modifiedByEmail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/**
 * POST /api/budgeting/cost-centres body (CostCentreRequest). Optional fields go as explicit
 * nulls, the BudgetCodeInput convention.
 */
export interface CostCentreInput {
  code: string;
  name: string;
  description: string | null;
  ownerUserId: string | null;
  parentId: string | null;
}

/**
 * PUT /api/budgeting/cost-centres/{id} body. No `code`: the server accepts it omitted (and refuses
 * any DIFFERENT code with 400 Budgeting.CostCentre.CodeImmutable), so this app never sends it —
 * there is nothing to round-trip wrongly.
 */
export type CostCentreUpdateInput = Omit<CostCentreInput, "code">;

/**
 * GET cost-centres?includeInactive= — ordered by code. Retired entries are left out unless
 * `includeInactive` is true. The query is always written out, so the wire never relies on the
 * server's default.
 */
export function listCostCentres(options: { includeInactive: boolean }): Promise<CostCentreRecord[]> {
  return request<CostCentreRecord[]>(
    `/api/budgeting/cost-centres?includeInactive=${options.includeInactive ? "true" : "false"}`,
  );
}

/**
 * POST cost-centres → 201 { id }. 400 CodeRequired / CodeTooLong / NameRequired / NameTooLong /
 * DescriptionTooLong / ParentIsNotTopLevel, 404 OwnerNotFound / ParentNotFound, 409 DuplicateCode /
 * ParentRetired — every message shown verbatim.
 */
export async function createCostCentre(input: CostCentreInput): Promise<string> {
  const res = await request<{ id: string }>("/api/budgeting/cost-centres", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/**
 * PUT cost-centres/{id} → 204. Beyond create's refusals: 404 NotFound, 400 ParentIsSelf,
 * 409 HasChildrenCannotHaveParent. Keeping a parent that has since been retired is accepted.
 */
export function updateCostCentre(id: string, input: CostCentreUpdateInput): Promise<void> {
  return request<void>(`/api/budgeting/cost-centres/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/**
 * POST cost-centres/{id}/activate|deactivate → 204. Built from a boolean like setBudgetCodeActive,
 * so the request test pins both directions. Deactivating a parent with active children is 409
 * Budgeting.CostCentre.HasActiveChildren (refused, never cascaded).
 */
export function setCostCentreActive(id: string, active: boolean): Promise<void> {
  return request<void>(`/api/budgeting/cost-centres/${id}/${active ? "activate" : "deactivate"}`, {
    method: "POST",
  });
}

/**
 * DELETE cost-centres/{id} → 204; 409 HasChildren (checked first), then 409 InUse when any budget
 * code in any period carries the code string. Retiring is the normal end of life; the server's
 * InUse message says so.
 */
export function deleteCostCentre(id: string): Promise<void> {
  return request<void>(`/api/budgeting/cost-centres/${id}`, { method: "DELETE" });
}

/** Mirrors CostCentreRollupRow. `costCentreId` is null only for a string no register entry matches. */
export interface CostCentreRollupRow {
  costCentreId: string | null;
  code: string;
  name: string;
  isActive: boolean;
  parentId: string | null;
  parentCode: string | null;
  ownerUserId: string | null;
  ownerName: string | null;
  ownerEmail: string | null;
  budgetCodeCount: number;
  itemCount: number;
  plannedCad: number;
}

/** Mirrors NoCostCentreRollup — expense planned on codes with no cost centre. */
export interface NoCostCentreRollup {
  budgetCodeCount: number;
  itemCount: number;
  plannedCad: number;
}

/**
 * Mirrors CostCentreRollupResponse. PLANNED ONLY — there is deliberately no actual field until
 * the actuals slice lands. `totalPlannedExpenseCad` is every row plus `noCostCentre`, and equals
 * the period's own `plannedExpenseCad`.
 */
export interface CostCentreRollup {
  periodId: string;
  /** Ordered by code (ordinal). Every active entry, plus retired ones this period still carries. */
  costCentres: CostCentreRollupRow[];
  noCostCentre: NoCostCentreRollup;
  totalPlannedExpenseCad: number;
}

/** GET periods/{periodId}/rollups/cost-centres → 200; 404 Budgeting.Period.NotFound. */
export function getCostCentreRollup(periodId: string): Promise<CostCentreRollup> {
  return request<CostCentreRollup>(`/api/budgeting/periods/${periodId}/rollups/cost-centres`);
}
