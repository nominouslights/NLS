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
import { formatDeltaCad } from "@/lib/money";

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
  /** Source lines on a code retired since, or gone from the chart entirely. */
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

/** "1 line" / "3 lines" — used by every clause of copyOutcomeSummary. */
function lineCount(n: number): string {
  return `${n} ${n === 1 ? "line" : "lines"}`;
}

/**
 * What a completed copy is told back to the planner, in plain sentences. Every bucket the server
 * reports is named, because the invariant
 * `copied + skippedAlreadyPlanned + skippedRetiredCode === sourceLineCount` is only reassuring
 * if the user can see it adds up. Clauses for a zero skip are omitted rather than written as
 * "0 skipped", and an empty source says so rather than reading as a failure.
 */
export function copyOutcomeSummary(result: BudgetAllocationCopyResult): string {
  if (result.sourceLineCount === 0) {
    return "That period has no lines to copy — nothing was added.";
  }

  const skips: string[] = [];
  if (result.skippedAlreadyPlanned > 0) {
    skips.push(`${lineCount(result.skippedAlreadyPlanned)} already planned here and left untouched`);
  }
  if (result.skippedRetiredCode > 0) {
    skips.push(`${lineCount(result.skippedRetiredCode)} on retired codes`);
  }

  const head =
    result.copied === 0
      ? "Nothing was copied"
      : `Copied ${lineCount(result.copied)}, each with no justification yet`;
  const tail = skips.length > 0 ? ` — skipped ${skips.join(" and ")}.` : ".";
  return `${head}${tail} ${lineCount(result.sourceLineCount)} in the source period.`;
}

/**
 * Whether a line still has to be argued. Mirrors BudgetAllocation.NeedsJustification
 * (`Justification.Length == 0`), which is exactly what BudgetAllocation.CopyInto produces — but
 * trimmed here to match Validate's `IsNullOrWhiteSpace`, so a line of spaces counts as unargued
 * on both sides rather than only on the save that would refuse it.
 */
export function needsJustification(line: BudgetAllocationRecord): boolean {
  return line.justification.trim().length === 0;
}

/** The period's lines that still carry no argument — the checklist's count and the finalize warning. */
export function unjustifiedLines(lines: BudgetAllocationRecord[]): BudgetAllocationRecord[] {
  return lines.filter(needsJustification);
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

// ---------------------------------------------------------------------------
// The zero-based checklist. Four rows, mapped onto the five steps of zero-based
// budgeting as they apply to a shuttle and cargo company:
//
//   1. List income            → "Revenue planned"
//   2. List expenses          → "Expense budget set" (every line already
//                               carries a required justification, which is
//                               stricter than most ZBB tools)
//   3. Subtract to reach zero → "Every dollar assigned"  ← the row this file
//                               existed without for too long
//   4. Track all month        → NO ROW. Actuals are still mock (lib/data.ts);
//                               a row that could never turn green is worse
//                               than an honest gap.
//   5. New budget each period → the lifecycle stepper plus the copy panel
//
// "Every line argued" is the fifth row and belongs to step 2: a copied line
// arrives with its amount and no argument, so the count of unargued lines is
// what keeps the copy button from quietly turning ZBB into rollover budgeting.
//
// Ramsey's household scaffolding (Giving, the Four Walls, the Baby Steps) does
// NOT map onto a shuttle company, so there are deliberately no expense
// priority tiers here.
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
      detail: `${lineCount(count)} · ${cov.planned} of ${cov.active} active ${category.toLowerCase()} codes planned`,
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
    label: "Every line argued",
    detail:
      lines.length === 0
        ? "No lines yet — nothing to argue."
        : unargued > 0
          ? `${unargued} of ${lineCount(lines.length)} still need a justification — a copied line brings its amount, not its argument.`
          : `All ${lineCount(lines.length)} carry a justification.`,
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
    lineStep("revenue", "Revenue", "Revenue planned"),
    lineStep("expense", "Expense", "Expense budget set"),
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
