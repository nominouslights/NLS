"use client";

import type { BudgetPeriod } from "@/lib/types";
import type { BudgetPeriodRecord } from "@/lib/api/budgeting";
import { Screen } from "@/components/screens/shared";
import PeriodDashboard from "@/components/screens/periods/PeriodDashboard";

// The Period Dashboard — the one place a period is planned. It shows exactly one period: the one
// the planner entered (Console, lib/workingPeriod.ts). There is no list and no picker here any
// more; choosing a period happens on the chooser, and leaving it takes the banner's SWITCH
// PERIOD, so nothing on this screen can quietly change which period the next action lands on.
//
// Console owns the period list (GET /api/budgeting/periods), the load states, and the New Period
// modal; the dashboard owns the entered period's lines, which only it reads. The lifecycle is
// five states, forward only — Draft → Finalized → Open → In review → Closed — mapped onto the
// shared status kinds in lib/api/budgeting.ts (periodKind) and carried on each row as
// BudgetPeriod.pk, so nothing here picks a colour itself.

export default function BudgetPeriods({
  period,
  periods,
  onPeriodsRefreshed,
}: {
  /** The entered period. */
  period: BudgetPeriod;
  /** The whole list, for the copy panel's source picker. */
  periods: BudgetPeriod[];
  /** After a transition or a line change: Console's applyLoaded, which replaces the list. */
  onPeriodsRefreshed: (records: BudgetPeriodRecord[]) => void;
}) {
  return (
    <Screen eyebrow={`Planning · ${period.label}`} title="Period Dashboard">
      <PeriodDashboard period={period} periods={periods} onPeriodsRefreshed={onPeriodsRefreshed} />
    </Screen>
  );
}
