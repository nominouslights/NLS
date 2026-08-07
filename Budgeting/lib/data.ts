// MOCK DATA — there is no Budgeting backend yet (Stage 6.1). Every array here is replaced by
// lib/api/budgeting.ts when it lands; nothing in this file invents an API shape, and no screen
// should reach past these exports to imagine one.
//
// Conventions copied from Dispatcher/lib/data.ts: flat exported const arrays of plain objects,
// string ids, ISO date strings (never Date objects), amounts as plain whole-dollar numbers
// rendered through formatCad, and a StatusKind carried on every status-bearing row.
//
// The four ZBB-* codes are the same strings already mocked in Dispatcher's Settings screen
// (components/screens/Settings.tsx, "Budget Codes" tab), so the two apps tell one story.

import type { Allocation, ActualLine, BudgetCode, BudgetPeriod, VarianceRow } from "./types";
import type { StatusKind } from "./theme";

export const periods: BudgetPeriod[] = [
  {
    id: "BP-2601",
    label: "FY2026 Q1",
    startsOn: "2026-01-01",
    endsOn: "2026-03-31",
    state: "Locked",
    pk: "off",
    planned: 1_284_000,
    allocated: 1_284_000,
  },
  {
    id: "BP-2602",
    label: "FY2026 Q2",
    startsOn: "2026-04-01",
    endsOn: "2026-06-30",
    state: "Locked",
    pk: "off",
    planned: 1_340_000,
    allocated: 1_340_000,
  },
  {
    id: "BP-2603",
    label: "FY2026 Q3",
    startsOn: "2026-07-01",
    endsOn: "2026-09-30",
    state: "Open",
    pk: "ontime",
    planned: 1_412_000,
    allocated: 1_268_500,
  },
  {
    id: "BP-2604",
    label: "FY2026 Q4",
    startsOn: "2026-10-01",
    endsOn: "2026-12-31",
    state: "Draft",
    pk: "info",
    planned: 1_455_000,
    allocated: 0,
  },
];

/** The period every screen opens on — the one currently accepting allocations. */
export const currentPeriodId = "BP-2603";

export const budgetCodes: BudgetCode[] = [
  {
    id: "BC-1001",
    code: "ZBB-CREW-01",
    name: "Alamos crew shuttle",
    category: "Revenue",
    owner: "R. Kelsey",
    active: true,
    justification:
      "Contracted crew rotation runs under the Alamos master agreement. Rebuilt each period from the confirmed rotation calendar, not last period's figure.",
  },
  {
    id: "BC-1002",
    code: "ZBB-NIHB-01",
    name: "NIHB medical transport",
    category: "Revenue",
    owner: "L. Fontaine",
    active: true,
    justification:
      "Direct-billed medical transport. Volume is demand-driven, so the period figure is built from the trailing three-period trip count, not a flat carry-forward.",
  },
  {
    id: "BC-1003",
    code: "ZBB-CHTR-02",
    name: "Charter runs",
    category: "Revenue",
    owner: "R. Kelsey",
    active: true,
    justification:
      "Ad-hoc charters. Zero-based by nature: only booked-and-confirmed charters enter the period plan.",
  },
  {
    id: "BC-1004",
    code: "ZBB-COMM-01",
    name: "Community fare runs",
    category: "Revenue",
    owner: "S. Okimaw",
    active: true,
    justification:
      "Demand-activated community service at a fixed seat fare. Justified per period against the community booking backlog.",
  },
  {
    id: "BC-2001",
    code: "ZBB-FUEL-01",
    name: "Fuel",
    category: "Expense",
    owner: "L. Fontaine",
    active: true,
    justification:
      "Largest single variable cost. Built from planned corridor kilometres × the period's contracted fuel rate.",
  },
  {
    id: "BC-2002",
    code: "ZBB-MAINT-01",
    name: "Fleet maintenance & parts",
    category: "Expense",
    owner: "L. Fontaine",
    active: true,
    justification:
      "Scheduled PM plus a parts float sized to the open work-order backlog at period start.",
  },
  {
    id: "BC-2003",
    code: "ZBB-WAGE-01",
    name: "Driver wages & benefits",
    category: "Expense",
    owner: "R. Kelsey",
    active: true,
    justification:
      "Driven by the rotation roster and HOS-legal shift coverage for the period's committed trips.",
  },
  {
    id: "BC-2004",
    code: "ZBB-INS-01",
    name: "Insurance & licensing",
    category: "Expense",
    owner: "L. Fontaine",
    active: false,
    justification:
      "Annual premium, recognised in Q1 only. Inactive for the remaining periods rather than spread, so no period carries a figure it cannot act on.",
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
