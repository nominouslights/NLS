// MOCK DATA — the not-yet-real remainder of the Budgeting backend (Stage 6.1). Budget periods
// are already real (lib/api/budgeting.ts); each remaining array here is replaced as its own
// Stage 6.1 slice lands. Nothing in this file invents an API shape, and no screen should reach
// past these exports to imagine one. Allocation/actual/variance rows are keyed to mock period
// ids that no real period will ever match, so screens on real periods show their empty states
// rather than a fake figure.
//
// Conventions copied from Dispatcher/lib/data.ts: flat exported const arrays of plain objects,
// string ids, ISO date strings (never Date objects), amounts as plain whole-dollar numbers
// rendered through formatCad, and a StatusKind carried on every status-bearing row.
//
// The four ZBB-* codes are the same strings already mocked in Dispatcher's Settings screen
// (components/screens/Settings.tsx, "Budget Codes" tab), so the two apps tell one story.
//
// `budgetCodes` below is NOT what the Budget Codes screen renders any more — that screen is on
// the real API (GET/POST/PUT/DELETE /api/budgeting/codes). The array survives only as the
// name-and-category lookup for the allocation, actual and variance rows further down, which are
// still mock and are keyed to these code strings. It goes when those slices land, not before.

import type { Allocation, ActualLine, BudgetCode, VarianceRow } from "./types";
import type { StatusKind } from "./theme";

/**
 * Exactly the four fields the remaining mock screens read — Reports and ActualsVsBudget want the
 * name and category, Allocations and Variance want the id to jump to the real screen with. Typed
 * as a Pick rather than a full BudgetCode so this stays a lookup and does not have to grow a
 * plausible-looking value for every field the real entity gains.
 */
type CodeLookupRow = Pick<BudgetCode, "id" | "code" | "name" | "category">;

export const budgetCodes: CodeLookupRow[] = [
  {
    id: "BC-1001",
    code: "ZBB-CREW-01",
    name: "Alamos crew shuttle",
    category: "Revenue",
  },
  {
    id: "BC-1002",
    code: "ZBB-NIHB-01",
    name: "NIHB medical transport",
    category: "Revenue",
  },
  {
    id: "BC-1003",
    code: "ZBB-CHTR-02",
    name: "Charter runs",
    category: "Revenue",
  },
  {
    id: "BC-1004",
    code: "ZBB-COMM-01",
    name: "Community fare runs",
    category: "Revenue",
  },
  {
    id: "BC-2001",
    code: "ZBB-FUEL-01",
    name: "Fuel",
    category: "Expense",
  },
  {
    id: "BC-2002",
    code: "ZBB-MAINT-01",
    name: "Fleet maintenance & parts",
    category: "Expense",
  },
  {
    id: "BC-2003",
    code: "ZBB-WAGE-01",
    name: "Driver wages & benefits",
    category: "Expense",
  },
  {
    id: "BC-2004",
    code: "ZBB-INS-01",
    name: "Insurance & licensing",
    category: "Expense",
  },
];

export const allocations: Allocation[] = [
  { id: "AL-4001", periodId: "BP-2603", code: "ZBB-CREW-01", amount: 612_000, note: "14 rotations confirmed" },
  { id: "AL-4002", periodId: "BP-2603", code: "ZBB-NIHB-01", amount: 244_000, note: "Trailing 3-period mean" },
  { id: "AL-4003", periodId: "BP-2603", code: "ZBB-CHTR-02", amount: 96_500, note: "6 charters booked" },
  { id: "AL-4004", periodId: "BP-2603", code: "ZBB-COMM-01", amount: 71_000, note: "Backlog-sized" },
  { id: "AL-4005", periodId: "BP-2603", code: "ZBB-FUEL-01", amount: 118_000, note: "142,000 km @ contracted rate" },
  { id: "AL-4006", periodId: "BP-2603", code: "ZBB-MAINT-01", amount: 74_500, note: "PM + parts float" },
  { id: "AL-4007", periodId: "BP-2603", code: "ZBB-WAGE-01", amount: 52_500, note: "Roster coverage" },
];

export const actuals: ActualLine[] = [
  { id: "AC-7001", periodId: "BP-2603", code: "ZBB-CREW-01", planned: 612_000, actual: 604_800 },
  { id: "AC-7002", periodId: "BP-2603", code: "ZBB-NIHB-01", planned: 244_000, actual: 261_300 },
  { id: "AC-7003", periodId: "BP-2603", code: "ZBB-CHTR-02", planned: 96_500, actual: 71_200 },
  { id: "AC-7004", periodId: "BP-2603", code: "ZBB-COMM-01", planned: 71_000, actual: 70_100 },
  { id: "AC-7005", periodId: "BP-2603", code: "ZBB-FUEL-01", planned: 118_000, actual: 139_400 },
  { id: "AC-7006", periodId: "BP-2603", code: "ZBB-MAINT-01", planned: 74_500, actual: 78_050 },
  { id: "AC-7007", periodId: "BP-2603", code: "ZBB-WAGE-01", planned: 52_500, actual: 52_100 },
];

/**
 * Variance bands, in one place so the Variance screen and any future report agree:
 * within 5% is on plan, 5–15% needs a look, over 15% needs a decision, and no baseline
 * (planned = 0) is "off" rather than an infinite percentage.
 */
export function varianceKind(deltaPct: number | null): StatusKind {
  if (deltaPct === null) return "off";
  const magnitude = Math.abs(deltaPct);
  if (magnitude <= 5) return "ontime";
  if (magnitude <= 15) return "soon";
  return "over";
}

/** Human label for a variance band — always rendered alongside the colour and glyph. */
export function varianceLabel(deltaPct: number | null): string {
  if (deltaPct === null) return "No baseline";
  const magnitude = Math.abs(deltaPct);
  if (magnitude <= 5) return "On plan";
  if (magnitude <= 15) return "Watch";
  return "Over threshold";
}

/** Signed percentage, always carrying an explicit + or − so the sign never rests on colour. */
export function formatDeltaPct(deltaPct: number | null): string {
  if (deltaPct === null) return "—";
  const sign = deltaPct > 0 ? "+" : deltaPct < 0 ? "−" : "";
  return `${sign}${Math.abs(deltaPct).toFixed(1)}%`;
}

/** Signed dollar delta, same rule as formatDeltaPct: the sign is text, not a colour. */
export function formatDeltaCad(delta: number): string {
  const sign = delta > 0 ? "+" : delta < 0 ? "−" : "";
  return `${sign}$${Math.abs(delta).toLocaleString("en-CA")}`;
}

/** Derived from `actuals` so the two can never disagree. */
export const variance: VarianceRow[] = actuals.map((line, i) => {
  const delta = line.actual - line.planned;
  const deltaPct = line.planned === 0 ? null : (delta / line.planned) * 100;
  return {
    id: `VR-${8001 + i}`,
    periodId: line.periodId,
    code: line.code,
    name: budgetCodes.find((c) => c.code === line.code)?.name ?? line.code,
    planned: line.planned,
    actual: line.actual,
    delta,
    deltaPct,
    vk: varianceKind(deltaPct),
  };
});
