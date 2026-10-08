"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import type { BudgetCode, BudgetCodeCategory, BudgetPeriod } from "@/lib/types";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { MetricTile } from "@/components/ui/MetricTile";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { formatCad, formatUtcDate } from "@/lib/api/format";
import { formatDeltaCad } from "@/lib/money";
import { ApiError } from "@/lib/api/transport";
import {
  allocationCandidates,
  assignmentState,
  canEditAllocations,
  copyBudgetAllocations,
  coverage,
  getBudgetPeriod,
  leftToAssignCad,
  listBudgetAllocations,
  listBudgetCodes,
  getCostCentreRollup,
  sumCad,
  type CostCentreRollup,
  listBudgetPeriods,
  nextTransition,
  planBalanced,
  planningProgress,
  refetchUntil,
  priorityBreakdown,
  removeBudgetItem,
  stateAfter,
  toBudgetCode,
  transitionBudgetPeriod,
  unjustifiedLines,
  ASSIGNMENT_KINDS,
  ASSIGNMENT_LABELS,
  PERIOD_STATE_LABELS,
  type BudgetAllocationCopyResult,
  type BudgetAllocationRecord,
  type BudgetPeriodRecord,
  type ChecklistStep,
  type LifecycleStep,
  type PeriodTransitionAction,
} from "@/lib/api/budgeting";
import { ErrorNotice } from "@/components/ErrorNotice";
import BudgetItemFormModal from "@/components/BudgetItemFormModal";
import PriorityBreakdown from "@/components/screens/periods/PriorityBreakdown";
import CostCentreBreakdown from "@/components/screens/periods/CostCentreBreakdown";
import { EmptyNote } from "@/components/screens/shared";
import { usePeriodHold } from "@/lib/periodHold";
import LifecycleStepper from "@/components/screens/periods/LifecycleStepper";
import PlanningChecklist from "@/components/screens/periods/PlanningChecklist";
import CopyFromPeriodPanel from "@/components/screens/periods/CopyFromPeriodPanel";
import AllocationSection from "@/components/screens/periods/AllocationSection";

// The body of the Period Dashboard: the entered period, where it stands in its lifecycle, what
// is planned against it, and the two things a planner does here — add budget items, and move the
// period forward. Owns its own fetch of the period's budget items and of the period's OWN chart of
// codes (periods/{id}/codes — codes belong to a period; both are needed for coverage and the
// picker). When the period has no active code of a category, that section points to Budget Codes
// (copy from another period, the starter set, or a new code) instead of opening an empty picker. Console remounts it (a Fragment keyed by the entered period's id)
// on every switch, so no confirm, modal or fetch outlives the period it was for.
//
// While a transition, a removal or a copy is in flight (`busy`), the dashboard holds the period
// (lib/periodHold.ts): SWITCH PERIOD and + NEW PERIOD refuse until the request settles, so its
// result — or its error — lands on the period the planner is still looking at. Every confirm
// names the period it acts on.
//
// Two eventual-consistency rules, both inherited from the codes screen: after a transition,
// refetch the period until it reports the expected state; after an item changes, refetch the items
// until the new VALUES are visible (not merely the row — on edit the row was always there). Only
// then is the period list refreshed, because its totals read from the same projection.
//
// Items are editable in Draft and Open only (BudgetPeriod.AllowsPlanChanges). Everywhere else the
// controls are absent and one note names the state, so "I can't add an item" reads as the rule it
// is rather than as a bug.

/**
 * What the confirming click will do — shown under the button while it awaits confirmation, and
 * naming the period, so a confirm can never be mistaken for one about another period. The button
 * labels themselves come from nextTransition and are pinned by budgeting.test.ts.
 */
const TRANSITION_NOTES: Record<PeriodTransitionAction, (label: string) => string> = {
  finalize: (label) =>
    `Finalizing signs ${label}'s plan off. Its items become read-only until the period is opened.`,
  open: (label) =>
    `Opening starts ${label} as the live period. Items can be adjusted again while it is open.`,
  "begin-review": (label) =>
    `Beginning review freezes ${label}'s plan. Items become read-only for the review.`,
  close: (label) =>
    `Closing ${label} is final. The period and its plan stay read-only, and there is no step after it.`,
};

const TRANSITION_VARIANTS: Record<PeriodTransitionAction, "primary" | "success" | "amber"> = {
  finalize: "primary",
  open: "success",
  "begin-review": "amber",
  close: "primary",
};

/**
 * The one confirm in flight, as a discriminated union rather than three independent booleans
 * that each had to remember to reset the other two. Copy made a fourth mutually-exclusive
 * confirm, at which point "one at a time" had to become structural instead of a convention
 * maintained by hand at every call site.
 */
type PendingConfirm =
  | { kind: "transition" }
  | { kind: "remove"; itemId: string }
  | { kind: "copy" }
  | null;

export default function PeriodDashboard({
  period,
  periods,
  onPeriodsRefreshed,
  onOpenCodes,
}: {
  period: BudgetPeriod;
  /** The whole list, for the copy panel's source picker. Threaded from Console via BudgetPeriods. */
  periods: BudgetPeriod[];
  /** Console's applyLoaded: replaces the list (the entered period is derived from it). */
  onPeriodsRefreshed: (records: BudgetPeriodRecord[]) => void;
  /** Go to Budget Codes for this same period — where an empty chart is filled. */
  onOpenCodes: () => void;
}) {
  const periodId = period.id;

  // null = still loading.
  const [lines, setLines] = useState<BudgetAllocationRecord[] | null>(null);
  const [codes, setCodes] = useState<BudgetCode[] | null>(null);
  /** The server's planned-expense rollup by cost centre; null while loading. */
  const [rollup, setRollup] = useState<CostCentreRollup | null>(null);
  const [rollupError, setRollupError] = useState<string | null>(null);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);
  const [busy, setBusy] = useState(false);
  /** Two-click confirms — transition, remove and copy — exactly one of which can be pending. */
  const [confirm, setConfirm] = useState<PendingConfirm>(null);
  const [modal, setModal] = useState<{
    category: BudgetCodeCategory;
    item: BudgetAllocationRecord | null;
    /** "+ ITEM" on a code header preselects that code. */
    presetCodeId: string | null;
  } | null>(null);

  // busy covers transition, remove and copy — every request this component makes against the
  // period. The item modal takes its own hold for its save.
  usePeriodHold(busy);

  const applyError = useCallback((e: unknown) => {
    setError(
      e instanceof ApiError
        ? { message: e.message, code: e.code }
        : { message: "Something went wrong — please try again.", code: "Unknown" },
    );
  }, []);

  /** Loads lines and the chart. then-callbacks per the Stops.tsx lint idiom; isActive guards unmount. */
  const load = useCallback(
    (isActive: () => boolean = () => true) => {
      listBudgetAllocations(periodId).then(
        (rows) => {
          if (isActive()) {
            setLines(rows);
            setError(null);
          }
        },
        (e) => {
          if (isActive()) {
            setLines((prev) => prev ?? []);
            applyError(e);
          }
        },
      );
      listBudgetCodes(periodId).then(
        (records) => {
          if (isActive()) setCodes(records.map(toBudgetCode));
        },
        (e) => {
          if (isActive()) {
            setCodes((prev) => prev ?? []);
            applyError(e);
          }
        },
      );
    },
    [periodId, applyError],
  );

  useEffect(() => {
    let active = true;
    load(() => active);
    return () => {
      active = false;
    };
  }, [load]);

  // The by-cost-centre rollup follows the items: it is refetched whenever a fresh items list lands
  // (initial load, and after every save, remove and copy — each of which already waited for its
  // own change to be visible). The rollup is a separate read, so it is retried until its total
  // agrees with the expense items just loaded. A failure stays inside the panel; it never blocks
  // the plan.
  useEffect(() => {
    if (lines === null) return;
    let active = true;
    const expected = sumCad(lines.filter((l) => l.category === "Expense").map((l) => l.amountCad));
    refetchUntil(
      () => getCostCentreRollup(periodId),
      (r) => r.totalPlannedExpenseCad === expected,
    ).then(
      (r) => {
        if (active) {
          setRollup(r);
          setRollupError(null);
        }
      },
      (e) => {
        if (active) {
          setRollupError(
            e instanceof ApiError ? e.message : "The cost-centre rollup could not be loaded.",
          );
        }
      },
    );
    return () => {
      active = false;
    };
  }, [periodId, lines]);

  const editable = canEditAllocations(period.state);
  const stateLabel = PERIOD_STATE_LABELS[period.state];
  const transition = nextTransition(period.state);
  const loaded = lines !== null && codes !== null;

  const steps = planningProgress(period, lines ?? [], codes ?? []);
  const lifecycleSteps = steps.filter((s): s is LifecycleStep => s.group === "lifecycle");
  const checklistSteps = steps.filter((s): s is ChecklistStep => s.group === "checklist");

  // Left to assign, not "net": under zero-based budgeting $0 is the target, not the neutral
  // case. Classified off the period's own server totals (assignmentState), so the tile is right
  // before the lines fetch lands — and so an empty period reads "Nothing planned yet" rather
  // than claiming everything is assigned.
  const assignment = assignmentState(period);
  const assignmentMeta = statusMeta(ASSIGNMENT_KINDS[assignment]);
  const left = leftToAssignCad(period);
  const unargued = unjustifiedLines(lines ?? []).length;
  const revenueCoverage = coverage(lines ?? [], codes ?? [], "Revenue");
  const expenseCoverage = coverage(lines ?? [], codes ?? [], "Expense");

  // Finalize warns but never blocks — the rule that transitions are not gated on plan
  // completeness is unchanged, and the button below is never disabled because of this.
  const finalizeConcerns: string[] = [];
  if (!planBalanced(period)) {
    finalizeConcerns.push(
      assignment === "empty"
        ? "Nothing is planned in this period yet."
        : assignment === "over"
          ? `${formatDeltaCad(left)} more is assigned than this period plans to earn.`
          : `${formatDeltaCad(left)} is still unassigned. Zero-based budgeting balances to exactly $0 — every dollar of planned revenue needs a job.`,
    );
  }
  if (unargued > 0) {
    finalizeConcerns.push(
      `${unargued} ${unargued === 1 ? "item" : "items"} still ${unargued === 1 ? "carries" : "carry"} no justification — copied from an earlier period and not yet argued.`,
    );
  }
  const finalizeWarning = transition?.action === "finalize" && finalizeConcerns.length > 0;
  const warningKind = planBalanced(period) ? "soon" : ASSIGNMENT_KINDS[assignment];
  const warningLabel = planBalanced(period) ? "Unargued items" : ASSIGNMENT_LABELS[assignment];

  // The by-priority breakdown is summed CLIENT-SIDE from the loaded items — it answers "what would
  // I cut first?", which the server does not total. The headline tiles above it stay the server's
  // own totals (period.plannedRevenue / plannedExpense) and are never re-derived from these items.
  const expenseByPriority = priorityBreakdown((lines ?? []).filter((l) => l.category === "Expense"));

  const refreshPeriods = () => listBudgetPeriods().then(onPeriodsRefreshed, applyError);

  async function runTransition() {
    if (!transition || busy) return;
    if (confirm?.kind !== "transition") {
      setConfirm({ kind: "transition" });
      return;
    }
    setConfirm(null);
    setBusy(true);
    setError(null);
    try {
      await transitionBudgetPeriod(periodId, transition.action);
      await refetchUntil(
        () => getBudgetPeriod(periodId),
        (r) => r.state === stateAfter(transition.action),
      );
      onPeriodsRefreshed(await listBudgetPeriods());
    } catch (e) {
      // A 409 (wrong source state — someone else moved it first) surfaces with the server's own
      // message, which names the rule.
      applyError(e);
    } finally {
      setBusy(false);
    }
  }

  async function removeItem(item: BudgetAllocationRecord) {
    if (busy) return;
    if (confirm?.kind !== "remove" || confirm.itemId !== item.id) {
      setConfirm({ kind: "remove", itemId: item.id });
      return;
    }
    setConfirm(null);
    setBusy(true);
    setError(null);
    try {
      await removeBudgetItem(periodId, item.id);
      const rows = await refetchUntil(
        () => listBudgetAllocations(periodId),
        (rows) => !rows.some((r) => r.id === item.id),
      );
      setLines(rows);
      onPeriodsRefreshed(await listBudgetPeriods());
    } catch (e) {
      applyError(e);
    } finally {
      setBusy(false);
    }
  }

  /**
   * Seed this period's plan from an earlier one. Returns the server's counts for the panel to
   * report, or null when the request was refused — in which case the banner above already
   * carries the server's own message (CopySourceRequired / CopySourceIsTarget /
   * CopySourceNotFound / Period.NotFound / PeriodNotEditable), which names the rule.
   */
  async function runCopy(sourcePeriodId: string): Promise<BudgetAllocationCopyResult | null> {
    if (busy) return null;
    const before = lines?.length ?? 0;
    setConfirm(null);
    setBusy(true);
    setError(null);
    try {
      const result = await copyBudgetAllocations(periodId, { sourcePeriodId });
      // Skip the refetch entirely when nothing was copied. A bulk write needs a COUNT predicate,
      // and "at least `copied` more rows" can never be satisfied by a successful no-op — the
      // button would hang until the retry loop gave up on a request that worked perfectly.
      if (result.copied > 0) {
        const rows = await refetchUntil(
          () => listBudgetAllocations(periodId),
          (rows) => rows.length >= before + result.copied,
        );
        setLines(rows);
        // The tiles read the server's own totals, so the period list has to follow the lines.
        onPeriodsRefreshed(await listBudgetPeriods());
      }
      return result;
    } catch (e) {
      applyError(e);
      return null;
    } finally {
      setBusy(false);
    }
  }

  function openModal(
    category: BudgetCodeCategory,
    item: BudgetAllocationRecord | null,
    presetCodeId: string | null = null,
  ) {
    setConfirm(null);
    setModal({ category, item, presetCodeId });
  }

  function handleSaved(rows: BudgetAllocationRecord[]) {
    setLines(rows);
    setError(null);
    void refreshPeriods();
  }

  return (
    <div>
      {/* Header: label, dates, state chip, the one forward action. */}
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 10,
          marginBottom: 6,
          flexWrap: "wrap",
        }}
      >
        <div style={{ flex: "1 1 auto", minWidth: 0 }}>
          <div
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 22,
              color: colors.headingBright,
              lineHeight: 1.1,
            }}
          >
            {period.label}
          </div>
          <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 3 }}>
            {formatUtcDate(period.startsOn)} — {formatUtcDate(period.endsOn)}
          </div>
        </div>
        <StatusChip kind={period.pk} label={stateLabel} />
        {transition && (
          <ActionButton
            variant={TRANSITION_VARIANTS[transition.action]}
            onClick={runTransition}
            disabled={busy}
          >
            {busy
              ? "WORKING…"
              : confirm?.kind === "transition"
                ? transition.confirmLabel
                : transition.label}
          </ActionButton>
        )}
      </div>

      {confirm?.kind === "transition" && transition && (
        <div style={{ marginBottom: 12 }}>
          {/* Finalizing an incomplete plan is allowed — the server does not gate transitions on
              plan completeness and neither does this screen. The warning is led by a StatusChip
              so it is never colour alone, and the button above stays enabled throughout. */}
          {finalizeWarning && (
            <div
              style={{
                display: "flex",
                alignItems: "flex-start",
                gap: 10,
                marginBottom: 8,
                fontFamily: fonts.body,
                fontSize: 11.5,
                color: colors.textSecondary,
                lineHeight: 1.6,
              }}
            >
              <StatusChip kind={warningKind} label={warningLabel} />
              <span style={{ flex: "1 1 auto" }}>
                {finalizeConcerns.join(" ")} You can finalize anyway — nothing here blocks it.
              </span>
            </div>
          )}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 10,
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: colors.textSecondary,
              lineHeight: 1.6,
            }}
          >
            <span style={{ flex: "1 1 auto" }}>
              {TRANSITION_NOTES[transition.action](period.label)} Click {transition.confirmLabel}{" "}
              to move {period.label} from {stateLabel} to{" "}
              {PERIOD_STATE_LABELS[stateAfter(transition.action)]} — forward only, there is no step
              back.
            </span>
            <ActionButton onClick={() => setConfirm(null)}>CANCEL</ActionButton>
          </div>
        </div>
      )}

      {error && (
        <div style={{ margin: "8px 0 12px" }}>
          <ErrorNotice title="Period plan" message={error.message} code={error.code} />
          <div style={{ marginTop: 9 }}>
            <ActionButton onClick={() => load()}>RETRY</ActionButton>
          </div>
        </div>
      )}

      <Panel style={{ margin: "10px 0 12px" }}>
        <SectionLabel>Lifecycle</SectionLabel>
        <LifecycleStepper steps={lifecycleSteps} />
      </Panel>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(170px, 1fr))",
          gap: 12,
          marginBottom: 12,
        }}
      >
        <MetricTile
          icon="◧"
          iconBg="rgba(31,111,178,.10)"
          iconColor={colors.blue}
          label="Planned revenue"
          value={formatCad(period.plannedRevenue)}
          valueColor={colors.headingBright}
        />
        <MetricTile
          icon="●"
          iconBg="rgba(31,111,178,.10)"
          iconColor={colors.blue}
          label="Planned expense"
          value={formatCad(period.plannedExpense)}
          valueColor={colors.headingBright}
        />
        {/* Left to assign — the headline of a zero-based plan, third so the row reads as the
            arithmetic: revenue → expense → what is left. Glyph + written state + a signed
            figure + the tinted border: four channels, and the colour is never the only one. */}
        <MetricTile
          icon={assignmentMeta.g}
          iconBg={assignmentMeta.bg}
          iconColor={assignmentMeta.t}
          label={`Left to assign · ${ASSIGNMENT_LABELS[assignment]}`}
          value={formatDeltaCad(left)}
          valueColor={assignmentMeta.t}
          borderColor={assignmentMeta.bd}
        />
        <MetricTile
          icon="◧"
          iconBg="rgba(31,111,178,.10)"
          iconColor={colors.blue}
          label="Revenue coverage"
          value={loaded ? `${revenueCoverage.planned}/${revenueCoverage.active}` : "…"}
          valueColor={colors.headingBright}
        />
        <MetricTile
          icon="●"
          iconBg="rgba(31,111,178,.10)"
          iconColor={colors.blue}
          label="Expense coverage"
          value={loaded ? `${expenseCoverage.planned}/${expenseCoverage.active}` : "…"}
          valueColor={colors.headingBright}
        />
      </div>

      {loaded && (
        <div style={{ marginBottom: 14 }}>
          <PlanningChecklist steps={checklistSteps} />
        </div>
      )}

      {loaded && (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))",
            gap: 12,
            alignItems: "start",
            marginBottom: 14,
          }}
        >
          <PriorityBreakdown title="Expense by priority" buckets={expenseByPriority} />
          <CostCentreBreakdown rollup={rollup} error={rollupError} />
        </div>
      )}

      {/* Zero-based step 5: a fresh plan each period, seeded from an earlier one rather than
          rebuilt line by line. Only while the period accepts plan changes — the same rule the
          server enforces on the copy's TARGET. */}
      {loaded && editable && (
        <CopyFromPeriodPanel
          period={period}
          periods={periods}
          busy={busy}
          confirming={confirm?.kind === "copy"}
          onRequestConfirm={() => setConfirm({ kind: "copy" })}
          onCancelConfirm={() => setConfirm(null)}
          onCopy={runCopy}
        />
      )}

      {!editable && (
        <div style={{ marginBottom: 14 }}>
          <EmptyNote>
            This period is {stateLabel}; its plan is read-only. Budget items can be added, changed
            or removed only while the period is Draft or Open.
          </EmptyNote>
        </div>
      )}

      {!loaded && !error && <EmptyNote>Loading the plan…</EmptyNote>}

      {loaded && (
        <>
          <AllocationSection
            category="Revenue"
            periodLabel={period.label}
            items={lines.filter((l) => l.category === "Revenue")}
            activeCodeCount={allocationCandidates(codes, "Revenue").length}
            onOpenCodes={onOpenCodes}
            editable={editable}
            busy={busy}
            confirmRemoveItemId={confirm?.kind === "remove" ? confirm.itemId : null}
            onAdd={() => openModal("Revenue", null)}
            onAddToCode={(codeId) => openModal("Revenue", null, codeId)}
            onEdit={(item) => openModal("Revenue", item)}
            onRemove={removeItem}
          />
          <AllocationSection
            category="Expense"
            periodLabel={period.label}
            items={lines.filter((l) => l.category === "Expense")}
            activeCodeCount={allocationCandidates(codes, "Expense").length}
            onOpenCodes={onOpenCodes}
            editable={editable}
            busy={busy}
            confirmRemoveItemId={confirm?.kind === "remove" ? confirm.itemId : null}
            onAdd={() => openModal("Expense", null)}
            onAddToCode={(codeId) => openModal("Expense", null, codeId)}
            onEdit={(item) => openModal("Expense", item)}
            onRemove={removeItem}
          />
        </>
      )}

      {modal && loaded && (
        <BudgetItemFormModal
          periodId={periodId}
          periodLabel={period.label}
          category={modal.category}
          codes={codes}
          item={modal.item}
          presetCodeId={modal.presetCodeId}
          onOpenCodes={onOpenCodes}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
    </div>
  );
}
