import type { StatusKind } from "./theme";

// Row types for the mock layer. The convention copied from Dispatcher/lib/types.ts: every row
// that renders a status carries its own StatusKind field (pk, vk, …) rather than having the
// screen derive one. Rendering stays a pure lookup, and — the reason it matters here — the
// colour can never be chosen without also choosing the glyph and label that travel with it.

/** Draft → Open → Locked. A locked period is closed to new allocations. */
export type PeriodState = "Draft" | "Open" | "Locked";

/**
 * The view shape the screens render. Periods are real data (lib/api/budgeting.ts maps the wire
 * record to this): pk is derived client-side from state, and planned/allocated are not on the
 * wire yet — they stay zeros until the allocations slice of Stage 6.1 lands.
 */
export interface BudgetPeriod {
  id: string;
  label: string;
  /** ISO date, inclusive. */
  startsOn: string;
  /** ISO date, inclusive. */
  endsOn: string;
  state: PeriodState;
  pk: StatusKind;
  /** Total planned, in whole CAD dollars. */
  planned: number;
  allocated: number;
}

/** Which side of the ledger a code governs. Mirrors C# BudgetCodeCategory. */
export type BudgetCodeCategory = "Revenue" | "Expense";

/**
 * Mirrors C# BudgetServiceLine. The first six are byte-identical to the backend's
 * TripServiceType, which is what lets revenue-mix reporting join on the value Trips and Billing
 * already emit — do not "tidy" `Nihb` into `NIHB`. The last three are overhead categories with
 * no counterpart anywhere else on the platform.
 */
export type BudgetServiceLine =
  | "ContractCrew"
  | "Community"
  | "Nihb"
  | "Charter"
  | "Cargo"
  | "Grocery"
  | "Fleet"
  | "Administrative"
  | "Apprenticeship";

/** Mirrors C# BudgetTaxTreatment. GST 5% is the only tax in play (no PST on transport in MB). */
export type BudgetTaxTreatment = "GstApplicable" | "ZeroRated" | "Exempt" | "NotApplicable";

/** Mirrors C# BudgetReviewFrequency. Required on every code; the server defaults it to Quarterly. */
export type BudgetReviewFrequency = "Monthly" | "Quarterly" | "Annual";

/**
 * The view shape the Budget Codes screen renders. Codes are real data — lib/api/budgeting.ts's
 * toBudgetCode maps the wire record to this, renaming only `isActive` → `active`.
 *
 * The `parentCode`/`parentName`/`…Email` companions to the id fields are resolved server-side on
 * every read, so a screen can render a parent or an owner without a second round trip and they
 * cannot go stale.
 */
export interface BudgetCode {
  id: string;
  code: string;
  name: string;
  /** What this code covers. Optional — the per-period justification lives on the allocation. */
  description: string | null;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  costCentre: string | null;
  /** One-level rollup. A code with a parent can never itself be a parent. */
  parentCodeId: string | null;
  parentCode: string | null;
  parentName: string | null;
  /** Free text — entered manually, never checked against QuickBooks. */
  glAccountCode: string | null;
  taxTreatment: BudgetTaxTreatment | null;
  budgetOwnerUserId: string | null;
  budgetOwnerEmail: string | null;
  reviewFrequency: BudgetReviewFrequency;
  active: boolean;
  createdByEmail: string | null;
  modifiedByEmail: string | null;
}

export interface Allocation {
  id: string;
  periodId: string;
  /** Matches BudgetCode.code, not its id — that is what a person reads and types. */
  code: string;
  amount: number;
  note: string;
}

export interface ActualLine {
  id: string;
  periodId: string;
  code: string;
  planned: number;
  actual: number;
}

export interface VarianceRow {
  id: string;
  periodId: string;
  code: string;
  name: string;
  planned: number;
  actual: number;
  /** actual − planned, in dollars. Sign is meaningful and is always rendered. */
  delta: number;
  /** delta as a share of planned, ×100. Null when planned is 0 (no baseline to vary from). */
  deltaPct: number | null;
  vk: StatusKind;
}
