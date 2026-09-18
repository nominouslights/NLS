import { request } from "./transport";
import { formatCad } from "./format";
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
 * Whether allocation lines may be set or removed in this state. Mirrors
 * BudgetPeriod.AllowsPlanChanges (Draft or Open) — the server answers 409
 * Budgeting.Allocation.PeriodNotEditable otherwise, so the dashboard hides the controls and
 * says why rather than offering an action that will be refused.
 */
export function canEditAllocations(state: PeriodState): boolean {
  return state === "Draft" || state === "Open";
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
// Allocations — one line per (period, code), upserted by code. Mirrors the
// backend's BudgetAllocationResponse and SetBudgetAllocationRequest. Category,
// name and service line are resolved from the code at read time, never
// snapshotted, so a re-classified code moves its lines between the totals.
// ---------------------------------------------------------------------------

/** Mirrors BudgetAllocationResponse — rendered directly, no view rename. */
export interface BudgetAllocationRecord {
  id: string;
  periodId: string;
  budgetCodeId: string;
  /** Immutable string snapshot of the code, so a line still reads after the code is retired. */
  code: string;
  name: string;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  /** False once the code is retired — the line stays, but cannot be re-set until restored. */
  isCodeActive: boolean;
  amountCad: number;
  justification: string;
  createdBy: string | null;
  createdByEmail: string | null;
  modifiedBy: string | null;
  modifiedByEmail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** PUT periods/{id}/allocations/{codeId} body (SetBudgetAllocationRequest). */
export interface BudgetAllocationInput {
  /** 0 ≤ x ≤ 999,999,999.99; the server rounds to 2 dp. */
  amountCad: number;
  /** Required, ≤ 1000 characters — zero-based means every line is justified from nothing. */
  justification: string;
}

/** GET → 200, ordered by code server-side; 404 when the period does not exist. */
export function listBudgetAllocations(periodId: string): Promise<BudgetAllocationRecord[]> {
  return request<BudgetAllocationRecord[]>(`/api/budgeting/periods/${periodId}/allocations`);
}

/**
 * PUT → 200 { id, created }. Upsert by code: the first call for a code creates the line, later
 * calls update it in place. 400 on validation, 404 for an unknown period or code, 409
 * Budgeting.Allocation.PeriodNotEditable / CodeRetired — show the server's message verbatim.
 */
export function setBudgetAllocation(
  periodId: string,
  codeId: string,
  input: BudgetAllocationInput,
): Promise<{ id: string; created: boolean }> {
  return request<{ id: string; created: boolean }>(
    `/api/budgeting/periods/${periodId}/allocations/${codeId}`,
    { method: "PUT", body: JSON.stringify(input) },
  );
}

/** DELETE → 204; 404 when there is no such line; 409 PeriodNotEditable outside Draft/Open. */
export function removeBudgetAllocation(periodId: string, codeId: string): Promise<void> {
  return request<void>(`/api/budgeting/periods/${periodId}/allocations/${codeId}`, {
    method: "DELETE",
  });
}

/** Mirrors BudgetAllocation.JustificationMaxLength. */
export const ALLOCATION_JUSTIFICATION_MAX_LENGTH = 1000;

/** Mirrors the upper bound behind BudgetAllocationErrors.AmountTooLarge (decimal(12,2)). */
export const ALLOCATION_AMOUNT_MAX = 999_999_999.99;

/**
 * Client-side check of the amount text before a round trip, mirroring BudgetAllocation.Validate
 * (AmountRequired / AmountNegative / AmountTooLarge). One rule is deliberately stricter than the
 * server's: the server accepts cents, this app plans in whole dollars (every figure renders
 * through formatCad at 0 dp), so a fractional amount is refused here rather than silently shown
 * rounded. The server re-checks and its answer is the one that counts.
 */
export function allocationAmountError(text: string): string | null {
  if (text.trim() === "") return "Enter an amount.";
  const n = Number(text);
  if (!Number.isFinite(n)) return "Enter an amount.";
  if (!Number.isInteger(n)) return "Enter whole dollars — no cents.";
  if (n < 0) return "The amount cannot be negative.";
  if (n > ALLOCATION_AMOUNT_MAX) return "The amount is too large.";
  return null;
}

/**
 * Mirrors BudgetAllocation.Validate's JustificationRequired / JustificationTooLong. Required
 * because zero-based budgeting means every line earns its place from nothing, each period.
 */
export function allocationJustificationError(text: string): string | null {
  const trimmed = text.trim();
  if (trimmed.length === 0) return "Enter a justification — every line is planned from zero.";
  if (trimmed.length > ALLOCATION_JUSTIFICATION_MAX_LENGTH) {
    return `The justification must be ${ALLOCATION_JUSTIFICATION_MAX_LENGTH} characters or fewer.`;
  }
  return null;
}

/**
 * The codes the allocation picker may offer, mirroring two server rules at once: the retired
 * check in SetBudgetAllocationCommandHandler (BudgetAllocationErrors.CodeRetired) and the unique
 * (tenant, period, code) index behind upsert-by-code. Active codes of the requested category
 * that do not already carry a line in this period — except the line being edited, whose own
 * code must stay selectable. Order is the caller's (the chart, by code).
 */
export function allocationCandidates(
  codes: BudgetCode[],
  lines: BudgetAllocationRecord[],
  category: BudgetCodeCategory,
  editingCodeId: string | null,
): BudgetCode[] {
  const planned = new Set(lines.map((l) => l.budgetCodeId));
  return codes.filter(
    (c) =>
      c.active &&
      c.category === category &&
      (c.id === editingCodeId || !planned.has(c.id)),
  );
}

/** The zero-based verdict on a period's plan. */
export type ZbbBalanceState = "balanced" | "unallocated" | "over";

export interface ZbbBalance {
  /** plannedRevenue − plannedExpense, in CAD. Positive is revenue with no job yet. */
  amount: number;
  state: ZbbBalanceState;
}

/**
 * The zero-based rule, as the console states it. There is no top-down budget amount anywhere in
 * this app — the pool *is* the period's planned revenue lines, and the plan is balanced when
 * every dollar of that pool has been assigned to an expense line
 * (plannedExpense === plannedRevenue).
 *
 * This replaced netCad / netKind / netLabel, which read a surplus as good news ("ontime"). Under
 * zero-based a surplus is not good news: it is money with no job, which is precisely the failure
 * the rule exists to catch. Hence "unallocated", carrying the caution kind, not the healthy one.
 *
 * Both totals are the server's own sums over the period's lines (BudgetPeriodResponse), so
 * nothing is re-added here — only judged.
 */
export function zbbBalance(period: BudgetPeriod): ZbbBalance {
  const amount = period.plannedRevenue - period.plannedExpense;
  if (amount > 0) return { amount, state: "unallocated" };
  if (amount < 0) return { amount, state: "over" };
  return { amount, state: "balanced" };
}

/**
 * Balance state → status kind. Never the only channel: every render pairs it with the glyph
 * StatusMeta carries, the words below, and the signed figure from formatDeltaCad.
 */
export const ZBB_BALANCE_KINDS: Record<ZbbBalanceState, StatusKind> = {
  balanced: "ontime",
  unallocated: "soon",
  over: "over",
};

export interface ZbbBalanceDisplay {
  kind: StatusKind;
  /** "Balanced" / "Unallocated $12,000" / "Over budget by $12,000" / "Nothing planned yet". */
  headline: string;
  /** One sentence naming what that means and what to do about it. */
  note: string;
}

/**
 * Everything a view needs to render the balance, decided here so the banner, the list row and
 * the checklist all say the same thing.
 *
 * `hasLines` exists because zero equals zero is arithmetically balanced and the wrong thing to
 * tell someone staring at an empty plan — an unplanned period reads "Nothing planned yet" and
 * carries the neutral kind, not the healthy one.
 */
export function zbbBalanceDisplay(balance: ZbbBalance, hasLines: boolean): ZbbBalanceDisplay {
  if (!hasLines) {
    return {
      kind: "info",
      headline: "Nothing planned yet",
      note: "No lines have been planned against this period — an empty plan is not a balanced one.",
    };
  }
  switch (balance.state) {
    case "unallocated":
      return {
        kind: ZBB_BALANCE_KINDS.unallocated,
        headline: `Unallocated ${formatCad(balance.amount)}`,
        note: "Planned revenue with no job yet — assign it to expense lines until the plan balances.",
      };
    case "over":
      return {
        kind: ZBB_BALANCE_KINDS.over,
        headline: `Over budget by ${formatCad(-balance.amount)}`,
        note: "Assigned expense exceeds planned revenue — cut expense lines, or plan the revenue that pays for them.",
      };
    default:
      return {
        kind: ZBB_BALANCE_KINDS.balanced,
        headline: "Balanced",
        note: "Every planned dollar of revenue is assigned. That is the zero-based bar.",
      };
  }
}

/**
 * Is anything planned against this period? The period list carries the two totals but not the
 * lines, so it answers from the totals; the dashboard, which fetches the lines, answers exactly
 * (lines.length > 0). The two disagree only for a plan made entirely of $0 lines, and "nothing
 * planned yet" is the honest thing to say about that too.
 */
export function hasPlannedAmounts(period: BudgetPeriod): boolean {
  return period.plannedRevenue !== 0 || period.plannedExpense !== 0;
}

/**
 * How much of a category's chart is planned: `planned` active codes carry a line, out of
 * `active` codes in the category. Lines on retired codes are not counted — they cannot be
 * re-set, so they are not "coverage" a planner can still act on.
 */
export function coverage(
  lines: BudgetAllocationRecord[],
  codes: BudgetCode[],
  category: BudgetCodeCategory,
): { planned: number; active: number } {
  const activeIds = new Set(codes.filter((c) => c.active && c.category === category).map((c) => c.id));
  const planned = lines.filter((l) => l.category === category && activeIds.has(l.budgetCodeId)).length;
  return { planned, active: activeIds.size };
}

/**
 * A checklist row. `detail` is the whole supporting sentence, already written out, so
 * PlanningChecklist stays a pure render of what this file decides rather than re-phrasing it.
 */
export interface ChecklistStepBase {
  label: string;
  detail: string;
  done: boolean;
}

/** A checklist row: are there any lines of this category yet? */
export interface LineStep extends ChecklistStepBase {
  group: "lines";
  id: "revenue" | "expense";
  category: BudgetCodeCategory;
  /** Lines of this category in the period, retired codes included. */
  count: number;
  /** "2 lines · 3 of 5 active revenue codes planned". */
  detail: string;
}

/**
 * The zero-based rule as a checklist row: every planned dollar of revenue has a job. Its own
 * step kind rather than a third LineStep, because it is not about one category's lines — it is
 * the relationship between both totals, and it is done or not for reasons a line count cannot
 * express (an empty plan is not a balanced one).
 */
export interface BalanceStep extends ChecklistStepBase {
  group: "balance";
  id: "balance";
  balance: ZbbBalance;
  /** No lines at all — the reason zero-versus-zero is not reported as balanced. */
  empty: boolean;
}

/** The rows PlanningChecklist renders: everything from planningProgress that is not lifecycle. */
export type ChecklistStep = LineStep | BalanceStep;

export type LifecycleStepStatus = "done" | "current" | "pending";

/** One stepper node per lifecycle state, in PERIOD_STATE_ORDER. */
export interface LifecycleStep {
  group: "lifecycle";
  id: PeriodState;
  label: string;
  status: LifecycleStepStatus;
}

export type PlanningStep = ChecklistStep | LifecycleStep;

/**
 * Where the planner stands: the two line checklist rows (done once at least one line exists),
 * then the zero-based balance row, then the five lifecycle states, each done / current / pending
 * relative to the period's state in PERIOD_STATE_ORDER. Pure, so the dashboard's checklist and
 * stepper are one tested derivation rather than two ad hoc ones.
 *
 * The balance row is a checklist row and nothing more: transitions stay ungated, so an unbalanced
 * plan can still be finalized. Making it loud here is the whole mechanism.
 */
export function planningProgress(
  period: BudgetPeriod,
  lines: BudgetAllocationRecord[],
  codes: BudgetCode[],
): PlanningStep[] {
  const lineStep = (id: LineStep["id"], category: BudgetCodeCategory, label: string): LineStep => {
    const count = lines.filter((l) => l.category === category).length;
    const cov = coverage(lines, codes, category);
    const plural = count === 1 ? "line" : "lines";
    return {
      group: "lines",
      id,
      category,
      label,
      count,
      detail: `${count} ${plural} · ${cov.planned} of ${cov.active} active ${category.toLowerCase()} codes planned`,
      done: count > 0,
    };
  };

  const balance = zbbBalance(period);
  const empty = lines.length === 0;
  const balanceStep: BalanceStep = {
    group: "balance",
    id: "balance",
    label: "Every dollar assigned",
    detail: zbbBalanceDisplay(balance, !empty).headline,
    balance,
    empty,
    done: balance.state === "balanced" && !empty,
  };

  const currentIndex = PERIOD_STATE_ORDER.indexOf(period.state);
  const lifecycle: LifecycleStep[] = PERIOD_STATE_ORDER.map((state, i) => ({
    group: "lifecycle",
    id: state,
    label: PERIOD_STATE_LABELS[state],
    status: i < currentIndex ? "done" : i === currentIndex ? "current" : "pending",
  }));

  return [
    lineStep("revenue", "Revenue", "Revenue planned"),
    lineStep("expense", "Expense", "Expense budget set"),
    balanceStep,
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
 * POST /api/budgeting/codes body (CreateBudgetCodeRequest). Optional fields go on the wire as
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
 * PUT /api/budgeting/codes/{id} body (UpdateBudgetCodeRequest). No `code`: the code string is
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

/** Ordered by code ascending server-side. Includes retired codes. */
export function listBudgetCodes(): Promise<BudgetCodeRecord[]> {
  return request<BudgetCodeRecord[]>("/api/budgeting/codes");
}

/** The tenant's users, from Budgeting's replica of Identity's accounts. */
export function listBudgetOwnerCandidates(): Promise<BudgetOwnerOption[]> {
  return request<BudgetOwnerOption[]>("/api/budgeting/codes/owners");
}

/** POST → 201 { id } (id only; the row lands on the next projection read). */
export async function createBudgetCode(input: BudgetCodeInput): Promise<string> {
  const res = await request<{ id: string }>("/api/budgeting/codes", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT → 204. */
export function updateBudgetCode(id: string, input: BudgetCodeUpdateInput): Promise<void> {
  return request<void>(`/api/budgeting/codes/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/**
 * POST → 204. Two routes rather than a body flag, matching the backend: retiring a code is a
 * flag flip, never a delete, so last period's allocations keep resolving.
 */
export function setBudgetCodeActive(id: string, active: boolean): Promise<void> {
  return request<void>(`/api/budgeting/codes/${id}/${active ? "activate" : "deactivate"}`, {
    method: "POST",
  });
}

/**
 * DELETE → 204, or 409 when the code has children or has ever been used. Retirement is the
 * normal path; this is only for a code created in error. The server's 409 message names
 * retirement as the alternative, so surfacing it verbatim is the right handling.
 */
export function deleteBudgetCode(id: string): Promise<void> {
  return request<void>(`/api/budgeting/codes/${id}`, { method: "DELETE" });
}

/** POST → 200 { created }. Idempotent: a second call creates nothing and returns 0. */
export function seedStarterBudgetCodes(): Promise<{ created: number }> {
  return request<{ created: number }>("/api/budgeting/codes/starter-set", { method: "POST" });
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
  if (!/^[A-Z0-9]([A-Z0-9-]*[A-Z0-9])?$/.test(normalized)) {
    return "Use letters, digits and hyphens only, starting and ending with a letter or digit.";
  }
  return null;
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
// Actual transactions — what was really spent or received, and the rollup that
// sets a period's plan against it. Mirrors the backend's
// ActualTransactionResponse, BudgetRollupRowResponse and
// RecordActualTransactionRequest.
//
// The single design point to carry over from the backend: **there is no period
// id on a transaction.** The period is derived from occurredOn and never
// stored, so moving a transaction between periods is a date correction and
// nothing else. That is why every write here is addressed to /actuals rather
// than to a period, and why the date field needs its own client-side check
// (actualDateError) — a date in no period is refused, not stored orphaned.
// ---------------------------------------------------------------------------

/** Wire enum. Fixed at creation server-side; nothing in this app can set it. */
export type ActualTransactionSource = "Manual" | "QuickBooks";

/**
 * Mirrors ActualTransactionResponse. Rendered directly, field for field, with no view type of
 * its own — the same reason BudgetAllocationRecord has none: nothing on it needs renaming, and a
 * mapper that only copied fields would be one more place for the contract to drift.
 *
 * `name`, `category`, `serviceLine` and `isCodeActive` describe the *code* and are resolved from
 * the chart on every read; `code` is the transaction's own snapshot of the code string, which is
 * what lets a row whose code was deleted and recreated still read.
 */
export interface ActualTransactionRecord {
  id: string;
  budgetCodeId: string;
  /** Immutable string snapshot of the code, re-taken only when the row is moved to another code. */
  code: string;
  name: string;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  /** False once the code is retired — the row stays correctable, but cannot move onto that code. */
  isCodeActive: boolean;
  amountCad: number;
  /** ISO date. The only thing that says which period this counts in. */
  occurredOn: string;
  description: string;
  source: ActualTransactionSource;
  /** The originating system's id — null for everything entered here. */
  externalReference: string | null;
  createdBy: string | null;
  createdByEmail: string | null;
  modifiedBy: string | null;
  modifiedByEmail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/**
 * Mirrors BudgetRollupRowResponse — one row per code, both sides of the outer join included: a
 * planned code with no spend comes back with actualCad 0, and a code spent against but never
 * planned comes back with plannedCad 0. That second row is the one zero-based budgeting exists
 * to surface, so nothing here filters it out.
 *
 * `deltaCad` is the server's own actual − planned. **No band, no percentage, no status** — those
 * thresholds are the console's (varianceKind in lib/data.ts), and a server that shipped its own
 * would put two answers to one question in the product.
 */
export interface BudgetRollupRowRecord {
  budgetCodeId: string;
  code: string;
  name: string;
  category: BudgetCodeCategory;
  plannedCad: number;
  actualCad: number;
  /** actualCad − plannedCad, as the server computed it. */
  deltaCad: number;
}

/**
 * POST /api/budgeting/actuals and PUT /api/budgeting/actuals/{id} body
 * (RecordActualTransactionRequest) — one shape for both, because a correction rewrites exactly
 * the fields an entry supplies.
 *
 * No period id (the date decides it), no source and no external reference (everything entered
 * here is Manual, and a caller that could name its own source could claim a QuickBooks id and
 * defeat the re-import guard). **And no tax field of any kind** — the platform never computes
 * tax; amountCad is the amount exactly as recorded.
 */
export interface ActualTransactionInput {
  budgetCodeId: string;
  /** 0 ≤ x ≤ 999,999,999.99; the server rounds to 2 dp. */
  amountCad: number;
  /** ISO date (YYYY-MM-DD). Must fall inside an existing period — see actualDateError. */
  occurredOn: string;
  description: string;
}

/** GET → 200, ordered by date then code server-side; 404 when the period does not exist. */
export function listActualTransactions(periodId: string): Promise<ActualTransactionRecord[]> {
  return request<ActualTransactionRecord[]>(`/api/budgeting/periods/${periodId}/actuals`);
}

/** GET → 200, one row per code, ordered by code; 404 when the period does not exist. */
export function budgetRollup(periodId: string): Promise<BudgetRollupRowRecord[]> {
  return request<BudgetRollupRowRecord[]>(`/api/budgeting/periods/${periodId}/rollup`);
}

/**
 * POST → 201 { id } (id only; the row lands on the next projection read). 400 on validation
 * (Budgeting.Actual.AmountRequired / AmountNegative / AmountTooLarge / OccurredOnRequired /
 * DescriptionRequired / DescriptionTooLong), 404 for an unknown code, 409
 * Budgeting.Actual.NoPeriodCoversDate / CodeRetired — show the server's message verbatim.
 */
export async function recordActualTransaction(input: ActualTransactionInput): Promise<string> {
  const res = await request<{ id: string }>("/api/budgeting/actuals", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/**
 * PUT → 204. Same body and the same refusals as recording, plus 404
 * Budgeting.Actual.NotFound — with one narrowing: CodeRetired applies only when the edit moves
 * the transaction *onto* a retired code. A row already sitting on one stays correctable in
 * place, because a retired code must never stand between anyone and fixing a fact.
 */
export function updateActualTransaction(id: string, input: ActualTransactionInput): Promise<void> {
  return request<void>(`/api/budgeting/actuals/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** DELETE → 204; 404 Budgeting.Actual.NotFound. */
export function removeActualTransaction(id: string): Promise<void> {
  return request<void>(`/api/budgeting/actuals/${id}`, { method: "DELETE" });
}

/** Mirrors ActualTransaction.DescriptionMaxLength. */
export const ACTUAL_DESCRIPTION_MAX_LENGTH = 300;

/** Mirrors ActualTransaction.AmountMax (decimal(12,2)). */
export const ACTUAL_AMOUNT_MAX = 999_999_999.99;

/** How a transaction's origin reads. Manual is everything this console records. */
export const ACTUAL_SOURCE_LABELS: Record<ActualTransactionSource, string> = {
  Manual: "Entered here",
  QuickBooks: "QuickBooks",
};

/**
 * Client-side check of the amount text before a round trip, mirroring ActualTransaction.Validate
 * (AmountRequired / AmountNegative / AmountTooLarge). Same shape as allocationAmountError,
 * including the one rule stricter than the server's: this app enters whole dollars, because
 * every figure renders through formatCad at 0 dp and a silently-rounded entry is worse than a
 * refused one. The server re-checks and its answer is the one that counts.
 *
 * "Negative" is worth its own message rather than a generic one: the server refuses a minus sign
 * because the code's category — not the sign — says which side of the ledger a figure lands on,
 * so an expense is a positive amount on an expense code.
 */
export function actualAmountError(text: string): string | null {
  if (text.trim() === "") return "Enter an amount.";
  const n = Number(text);
  if (!Number.isFinite(n)) return "Enter an amount.";
  if (!Number.isInteger(n)) return "Enter whole dollars — no cents.";
  if (n < 0) {
    return "The amount cannot be negative — record an expense as a positive amount on an expense code.";
  }
  if (n > ACTUAL_AMOUNT_MAX) return "The amount is too large.";
  return null;
}

/** Mirrors ActualTransaction.Validate's DescriptionRequired / DescriptionTooLong. */
export function actualDescriptionError(text: string): string | null {
  const trimmed = text.trim();
  if (trimmed.length === 0) return "Say what this transaction was for.";
  if (trimmed.length > ACTUAL_DESCRIPTION_MAX_LENGTH) {
    return `The description must be ${ACTUAL_DESCRIPTION_MAX_LENGTH} characters or fewer.`;
  }
  return null;
}

/**
 * The period a date counts in, or null. Mirrors BudgetPeriod.Covers — inclusive on both ends —
 * and relies, exactly as CoveringPeriodRule does, on CreateBudgetPeriodCommandHandler refusing
 * any period that shares a day with an existing one: a date is covered at most once, so "the
 * first match" is "the match".
 *
 * ISO date strings are compared as strings on purpose. YYYY-MM-DD sorts lexicographically the
 * same way it sorts chronologically, and constructing a Date here would reintroduce the
 * timezone shift lib/period.ts exists to avoid.
 */
export function periodCovering(periods: BudgetPeriod[], occurredOn: string): BudgetPeriod | null {
  if (!occurredOn) return null;
  return periods.find((p) => p.startsOn <= occurredOn && occurredOn <= p.endsOn) ?? null;
}

/**
 * The date rule, said before the save rather than discovered by a 409. Mirrors
 * CoveringPeriodRule.ValidateAsync (ActualTransactionErrors.NoPeriodCoversDate): a transaction
 * whose date falls in no period is refused, because the period view is the only place actuals
 * are ever listed and an orphan row is one nothing in the product could ever show again.
 *
 * The form knows every period, so it can say this while the date is being typed. The server
 * still re-checks — it sees periods this client may not have refetched — and its message is
 * shown verbatim when it does.
 */
export function actualDateError(occurredOn: string, periods: BudgetPeriod[]): string | null {
  if (occurredOn.trim() === "") return "Enter the date the money moved.";
  if (periodCovering(periods, occurredOn) === null) {
    return "No budget period covers that date, so this transaction would never show up anywhere. Create the period first, or check the date.";
  }
  return null;
}

/**
 * The codes the transaction picker may offer, mirroring the two handlers' asymmetric retired
 * check. RecordActualTransactionCommandHandler refuses a retired code outright
 * (ActualTransactionErrors.CodeRetired): new spend is a new decision against a code nobody
 * offers any more. UpdateActualTransactionCommandHandler refuses only a *move onto* a retired
 * code, so the row's own code stays selectable however long ago it was retired — otherwise a
 * typo in a description on historical data would be unfixable without restoring a code the
 * tenant deliberately took out of use.
 *
 * Both categories are offered: a transaction is revenue or expense according to its code, and
 * the entry form does not make the user pick a side first. Order is the caller's (the chart, by
 * code).
 */
export function actualCodeCandidates(
  codes: BudgetCode[],
  currentCodeId: string | null,
): BudgetCode[] {
  return codes.filter((c) => c.active || c.id === currentCodeId);
}

/** One category's rollup rows and their totals. */
export interface BudgetRollupGroup {
  category: BudgetCodeCategory;
  /** In the server's order (by code). */
  rows: BudgetRollupRowRecord[];
  plannedCad: number;
  actualCad: number;
  /** Sum of the rows' own deltas, so the total can never disagree with the column above it. */
  deltaCad: number;
}

/** The rollup as the screen renders it: revenue and expense, never summed together. */
export interface BudgetRollup {
  revenue: BudgetRollupGroup;
  expense: BudgetRollupGroup;
  /** Nothing planned and nothing spent — the screen's empty state. */
  empty: boolean;
}

/**
 * Groups the rollup by category, the way the period dashboard groups its lines, and totals each
 * side separately.
 *
 * **The two sides are never added together.** A single "planned" figure spanning revenue and
 * expense is a number with no meaning — it would read as a budget while being the sum of money
 * coming in and money going out — and the ZBB balance banner on the period dashboard is the one
 * place those two totals are compared, deliberately, as a difference rather than a sum.
 *
 * Every row lands in one of the two groups: the wire category is a two-member union, and the
 * server falls back to Expense for a row whose code the chart no longer has.
 */
export function toBudgetRollup(rows: BudgetRollupRowRecord[]): BudgetRollup {
  const group = (category: BudgetCodeCategory): BudgetRollupGroup => {
    const groupRows = rows.filter((r) => r.category === category);
    return {
      category,
      rows: groupRows,
      plannedCad: groupRows.reduce((sum, r) => sum + r.plannedCad, 0),
      actualCad: groupRows.reduce((sum, r) => sum + r.actualCad, 0),
      deltaCad: groupRows.reduce((sum, r) => sum + r.deltaCad, 0),
    };
  };

  return { revenue: group("Revenue"), expense: group("Expense"), empty: rows.length === 0 };
}

/**
 * A rollup figure's delta as a share of what was planned, ×100 — the input to varianceKind's
 * bands. Null when nothing was planned, which is the case that matters: a code spent against
 * but never planned has no baseline to vary from, so it reports "no baseline" rather than an
 * infinite overrun. The other end — planned but never spent — is a plain −100%, which is a real
 * and reportable figure, not a missing one.
 *
 * Client-side arithmetic with no server counterpart: the rollup endpoint ships the two amounts
 * and the signed difference and deliberately ships no band or percentage.
 */
export function rollupDeltaPct(row: {
  plannedCad: number;
  deltaCad: number;
}): number | null {
  return row.plannedCad === 0 ? null : (row.deltaCad / row.plannedCad) * 100;
}
