import type { StatusKind } from "./theme";

// Row types for the mock layer. The convention copied from Dispatcher/lib/types.ts: every row
// that renders a status carries its own StatusKind field (pk, vk, …) rather than having the
// screen derive one. Rendering stays a pure lookup, and — the reason it matters here — the
// colour can never be chosen without also choosing the glyph and label that travel with it.

/** Draft → Open → Locked. A locked period is closed to new allocations. */
export type PeriodState = "Draft" | "Open" | "Locked";

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

/** Zero-based budgeting works bottom-up, so a code is justified every period, not inherited. */
export type BudgetCodeCategory = "Revenue" | "Expense";

export interface BudgetCode {
  id: string;
  code: string;
  name: string;
  category: BudgetCodeCategory;
  /** Free-text owner — no user directory in the mock layer. */
  owner: string;
  active: boolean;
  /** Why this code exists at all — the ZBB justification, re-confirmed each period. */
  justification: string;
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
