// MOCK DATA — the last not-yet-real screen in this console (Stage 6.1). Budget periods, codes,
// allocations and now **actual transactions** are all real (lib/api/budgeting.ts); variance is
// what is left here. It is not waiting on QuickBooks: actuals became real by manual entry
// (POST/PUT/DELETE /api/budgeting/actuals), and a QuickBooks pull is a later slice of its own.
// Variance goes the moment it is rebuilt on GET periods/{id}/rollup, which already ships the
// planned/actual/delta triple every row below carries — only the bands are missing there, and
// the bands live in this file.
//
// Nothing in this file invents an API shape, and no screen should reach past these exports to
// imagine one. Variance rows are keyed to mock period ids that no real period will ever match,
// so screens on real periods show their empty states rather than a fake figure.
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
// name-and-category lookup for the variance rows further down and for Reports' revenue mix,
// which are still mock and are keyed to these code strings. It goes when that slice lands.
//
// varianceKind / varianceLabel are NOT mock. They are the console's variance bands, and Actuals
// vs Budget — which is on real data now — reads them from here so the two screens and any future
// report can never disagree about what "on plan" means. They move out of this file when the last
// mock array does, not before.
//
// The signed formatters (formatDeltaCad / formatDeltaPct) live in lib/money.ts so a screen on
// real data can print a signed figure without importing this module.

import type { BudgetCode, VarianceRow } from "./types";
import type { StatusKind } from "./theme";

/**
 * Exactly the four fields the remaining mock screens read — Reports wants the name and category,
 * Variance wants the id to jump to the real Budget Codes screen with. Typed as a Pick rather
 * than a full BudgetCode so this stays a lookup and does not have to grow a plausible-looking
 * value for every field the real entity gains.
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

/**
 * The mock variance rows. Literal facts — what was planned and what was spent, per code — with
 * the three derived fields filled by one function rather than typed out: delta, its percentage
 * and the band are arithmetic on the two figures above them, and a hand-written percentage is a
 * row whose colour can disagree with its own numbers.
 *
 * These used to be derived from an `actuals` array in this file. That array is gone: actuals are
 * real now, recorded by hand against a period and read back through the rollup endpoint.
 */
export const variance: VarianceRow[] = [
  { id: "VR-8001", code: "ZBB-CREW-01", planned: 612_000, actual: 604_800 },
  { id: "VR-8002", code: "ZBB-NIHB-01", planned: 244_000, actual: 261_300 },
  { id: "VR-8003", code: "ZBB-CHTR-02", planned: 96_500, actual: 71_200 },
  { id: "VR-8004", code: "ZBB-COMM-01", planned: 71_000, actual: 70_100 },
  { id: "VR-8005", code: "ZBB-FUEL-01", planned: 118_000, actual: 139_400 },
  { id: "VR-8006", code: "ZBB-MAINT-01", planned: 74_500, actual: 78_050 },
  { id: "VR-8007", code: "ZBB-WAGE-01", planned: 52_500, actual: 52_100 },
].map(({ id, code, planned, actual }) => {
  const delta = actual - planned;
  const deltaPct = planned === 0 ? null : (delta / planned) * 100;
  return {
    id,
    // A mock period id no real period will ever match, so a real period shows the empty state.
    periodId: "BP-2603",
    code,
    name: budgetCodes.find((c) => c.code === code)?.name ?? code,
    planned,
    actual,
    delta,
    deltaPct,
    vk: varianceKind(deltaPct),
  };
});
