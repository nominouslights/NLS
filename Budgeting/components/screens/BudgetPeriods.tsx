"use client";

import { useState } from "react";
import type { BudgetPeriod } from "@/lib/types";
import { ActionButton } from "@/components/ui/Button";
import type { BudgetPeriodRecord } from "@/lib/api/budgeting";
import { ErrorNotice } from "@/components/ErrorNotice";
import BudgetPeriodFormModal from "@/components/BudgetPeriodFormModal";
import { EmptyNote, Screen } from "@/components/screens/shared";
import PeriodList from "@/components/screens/periods/PeriodList";
import PeriodDashboard from "@/components/screens/periods/PeriodDashboard";

// Budget periods and where each one stands. Two modes in one screen, chosen by openPeriodId:
//
//   list mode   — every period at full width, one row each, with the planned totals and the
//                 zero-based balance right there on the row.
//   period mode — the whole content area *is* that one period: its header, lifecycle, ZBB
//                 balance, planning checklist and its Revenue and Expense lines, with
//                 "← ALL PERIODS" to come back.
//
// This replaced a 38%/1fr master/detail grid. A period is not a preview beside a list — it is
// where all the work happens, and at 38% the list was paying for width the dashboard needed.
// It is a take-over inside the console, not a browser window: neither console has URL routing,
// so a literal new tab would mean introducing routing here and in Dispatcher's shell both.
//
// Console owns the period list (GET /api/budgeting/periods) and threads it down, because four
// screens read it; it owns openPeriodId too, so stepping over to Budget Codes and back does not
// throw you out of the period you were working in. The dashboard owns the open period's lines,
// which only it reads. The lifecycle is five states, forward only — Draft → Finalized → Open →
// In review → Closed — mapped onto the shared status kinds in lib/api/budgeting.ts (periodKind)
// and carried on each row as BudgetPeriod.pk, so nothing here picks a colour itself.
//
// Creating a period opens it directly: Console.handlePeriodCreated selects the new id *and*
// enters period mode, so the modal closes onto the plan you just made room for.

export default function BudgetPeriods({
  periods,
  error,
  onRetry,
  openPeriodId,
  onOpenPeriod,
  onClosePeriod,
  onCreated,
  onPeriodsRefreshed,
}: {
  /** null while the first load is in flight. */
  periods: BudgetPeriod[] | null;
  error: { message: string; code: string } | null;
  onRetry: () => void;
  /** The period being shown in full, or null for the list. Hoisted into Console. */
  openPeriodId: string | null;
  onOpenPeriod: (id: string) => void;
  onClosePeriod: () => void;
  onCreated: (records: BudgetPeriodRecord[], id: string) => void;
  /** After a transition or a line change: Console's applyLoaded, which preserves the selection. */
  onPeriodsRefreshed: (records: BudgetPeriodRecord[]) => void;
}) {
  const [showCreate, setShowCreate] = useState(false);

  const list = periods ?? [];
  // Console drops openPeriodId when a reload no longer contains it, so this lookup failing means
  // the list is still loading — the list mode's own loading note covers that.
  const open = openPeriodId ? (list.find((p) => p.id === openPeriodId) ?? null) : null;

  return (
    <Screen
      eyebrow="Planning"
      title="Budget Periods"
      right={
        open ? (
          <ActionButton onClick={onClosePeriod}>← ALL PERIODS</ActionButton>
        ) : (
          <ActionButton variant="primary" onClick={() => setShowCreate(true)}>
            + NEW PERIOD
          </ActionButton>
        )
      }
    >
      {error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorNotice title="Couldn't load budget periods" message={error.message} code={error.code} />
          <div style={{ marginTop: 9 }}>
            <ActionButton onClick={onRetry}>RETRY</ActionButton>
          </div>
        </div>
      )}

      {periods === null && !error && <EmptyNote>Loading budget periods…</EmptyNote>}

      {periods !== null && list.length === 0 && !error && (
        <EmptyNote>No budget periods yet — create one to start planning.</EmptyNote>
      )}

      {open ? (
        /* Keyed by id so every confirm, modal and fetch resets when a different period is opened. */
        <PeriodDashboard key={open.id} period={open} onPeriodsRefreshed={onPeriodsRefreshed} />
      ) : (
        list.length > 0 && <PeriodList periods={list} onOpen={onOpenPeriod} />
      )}

      {showCreate && (
        <BudgetPeriodFormModal onClose={() => setShowCreate(false)} onSaved={onCreated} />
      )}
    </Screen>
  );
}
