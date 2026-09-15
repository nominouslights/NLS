"use client";

import { useState } from "react";
import { colors } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { ActionButton } from "@/components/ui/Button";
import type { BudgetPeriodRecord } from "@/lib/api/budgeting";
import { ErrorNotice } from "@/components/ErrorNotice";
import BudgetPeriodFormModal from "@/components/BudgetPeriodFormModal";
import { EmptyNote, Screen } from "@/components/screens/shared";
import PeriodList from "@/components/screens/periods/PeriodList";
import PeriodDashboard from "@/components/screens/periods/PeriodDashboard";

// Budget periods and where each one stands — master/detail on real data, following the Budget
// Codes screen (and Dispatcher's Clients and Trips): a left column of period rows and a right
// dashboard pane on the tinted detailBg, split by a CSS grid with a top border.
//
// Console owns the period list (GET /api/budgeting/periods) and threads it down, because five
// screens read it; the dashboard owns the selected period's lines, which only it reads. The
// lifecycle is five states, forward only — Draft → Finalized → Open → In review → Closed — mapped
// onto the shared status kinds in lib/api/budgeting.ts (periodKind) and carried on each row as
// BudgetPeriod.pk, so nothing here picks a colour itself.
//
// Creating a period lands on its dashboard with no extra navigation: Console.handlePeriodCreated
// already selects the new id, and the selected row is the one the dashboard shows.

export default function BudgetPeriods({
  periods,
  error,
  onRetry,
  periodId,
  onSelectPeriod,
  onCreated,
  onPeriodsRefreshed,
}: {
  /** null while the first load is in flight. */
  periods: BudgetPeriod[] | null;
  error: { message: string; code: string } | null;
  onRetry: () => void;
  periodId: string;
  onSelectPeriod: (id: string) => void;
  onCreated: (records: BudgetPeriodRecord[], id: string) => void;
  /** After a transition or a line change: Console's applyLoaded, which preserves the selection. */
  onPeriodsRefreshed: (records: BudgetPeriodRecord[]) => void;
}) {
  const [showCreate, setShowCreate] = useState(false);

  const list = periods ?? [];
  // periodId is "" until Console's first load picks one; falling back to the first row keeps the
  // pane populated in that gap.
  const selected = list.find((p) => p.id === periodId) ?? list[0] ?? null;

  return (
    <Screen
      eyebrow="Planning"
      title="Budget Periods"
      right={
        <ActionButton variant="primary" onClick={() => setShowCreate(true)}>
          + NEW PERIOD
        </ActionButton>
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

      {selected && (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "38% 1fr",
            borderTop: `1px solid ${colors.border}`,
            height: "100%",
            minHeight: 0,
          }}
        >
          <PeriodList periods={list} selectedId={selected.id} onSelect={onSelectPeriod} />
          {/* Keyed by id so every confirm, modal and fetch resets when the selection changes. */}
          <PeriodDashboard
            key={selected.id}
            period={selected}
            onPeriodsRefreshed={onPeriodsRefreshed}
          />
        </div>
      )}

      {showCreate && (
        <BudgetPeriodFormModal onClose={() => setShowCreate(false)} onSaved={onCreated} />
      )}
    </Screen>
  );
}
