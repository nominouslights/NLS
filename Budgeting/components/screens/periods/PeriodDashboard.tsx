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
  assignmentState,
  canEditAllocations,
  copyBudgetAllocations,
  coverage,
  getBudgetPeriod,
  leftToAssignCad,
  listBudgetAllocations,
  listBudgetCodes,
  listBudgetPeriods,
  nextTransition,
  planBalanced,
  planningProgress,
  refetchUntil,
  removeBudgetAllocation,
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
import BudgetAllocationFormModal from "@/components/BudgetAllocationFormModal";
import { EmptyNote } from "@/components/screens/shared";
import LifecycleStepper from "@/components/screens/periods/LifecycleStepper";
import PlanningChecklist from "@/components/screens/periods/PlanningChecklist";
import CopyFromPeriodPanel from "@/components/screens/periods/CopyFromPeriodPanel";
import AllocationSection from "@/components/screens/periods/AllocationSection";

// The detail pane of the Budget Periods screen: one period, where it stands in its lifecycle,
// what is planned against it, and the two things a planner does here — set lines, and move the
// period forward. Owns its own fetch of the period's lines and the chart (both needed for
// coverage and the picker), mounted per period by key from BudgetPeriods so every confirm and
// modal resets when the selection changes.
//
// Two eventual-consistency rules, both inherited from the codes screen: after a transition,
// refetch the period until it reports the expected state; after a line changes, refetch the lines
// until the new VALUES are visible (not merely the row — on edit the row was always there). Only
// then is the period list refreshed, because its totals read from the same projection.
//
// Lines are editable in Draft and Open only (BudgetPeriod.AllowsPlanChanges). Everywhere else the
// controls are absent and one note names the state, so "I can't add a line" reads as the rule it
// is rather than as a bug.

/** What the confirming click will do — shown under the button while it awaits confirmation. */
const TRANSITION_NOTES: Record<PeriodTransitionAction, string> = {
  finalize:
    "Finalizing signs the plan off. Its lines become read-only until the period is opened.",
  open: "Opening starts the live period. Lines can be adjusted again while it is open.",
  "begin-review": "Beginning review freezes the plan. Lines become read-only for the review.",
  close: "Closing is final. The period and its plan stay read-only, and there is no step after it.",
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
  | { kind: "remove"; codeId: string }
  | { kind: "copy" }
  | null;

export default function PeriodDashboard({
  period,
  periods,
  onPeriodsRefreshed,
}: {
  period: BudgetPeriod;
  /** The whole list, for the copy panel's source picker. Threaded from Console via BudgetPeriods. */
  periods: BudgetPeriod[];
  /** Console's applyLoaded: replaces the list while preserving the selection. */
  onPeriodsRefreshed: (records: BudgetPeriodRecord[]) => void;
}) {
  const periodId = period.id;

  // null = still loading.
  const [lines, setLines] = useState<BudgetAllocationRecord[] | null>(null);
  const [codes, setCodes] = useState<BudgetCode[] | null>(null);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);
  const [busy, setBusy] = useState(false);
  /** Two-click confirms — transition, remove and copy — exactly one of which can be pending. */
  const [confirm, setConfirm] = useState<PendingConfirm>(null);
  const [modal, setModal] = useState<{
    category: BudgetCodeCategory;
    line: BudgetAllocationRecord | null;
  } | null>(null);

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
      listBudgetCodes().then(
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
      `${unargued} ${unargued === 1 ? "line" : "lines"} still ${unargued === 1 ? "carries" : "carry"} no justification — copied from an earlier period and not yet argued.`,
    );
  }
  const finalizeWarning = transition?.action === "finalize" && finalizeConcerns.length > 0;
  const warningKind = planBalanced(period) ? "soon" : ASSIGNMENT_KINDS[assignment];
  const warningLabel = planBalanced(period) ? "Unargued lines" : ASSIGNMENT_LABELS[assignment];

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

  async function removeLine(line: BudgetAllocationRecord) {
    if (busy) return;
    if (confirm?.kind !== "remove" || confirm.codeId !== line.budgetCodeId) {
      setConfirm({ kind: "remove", codeId: line.budgetCodeId });
      return;
    }
    setConfirm(null);
    setBusy(true);
    setError(null);
    try {
      await removeBudgetAllocation(periodId, line.budgetCodeId);
      const rows = await refetchUntil(
        () => listBudgetAllocations(periodId),
        (rows) => !rows.some((r) => r.budgetCodeId === line.budgetCodeId),
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

  function openModal(category: BudgetCodeCategory, line: BudgetAllocationRecord | null) {
    setConfirm(null);
    setModal({ category, line });
  }

  function handleSaved(rows: BudgetAllocationRecord[]) {
    setLines(rows);
    setError(null);
    void refreshPeriods();
  }

  return (
    <div style={{ overflowY: "auto", padding: "22px 0 22px 26px", background: colors.detailBg }}>
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
              {TRANSITION_NOTES[transition.action]} Forward only — there is no step back. Click{" "}
              {transition.confirmLabel} to proceed.
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
            This period is {stateLabel}; its plan is read-only. Lines can change only while the
            period is Draft or Open.
          </EmptyNote>
        </div>
      )}

      {!loaded && !error && <EmptyNote>Loading the plan…</EmptyNote>}

      {loaded && (
        <>
          <AllocationSection
            category="Revenue"
            lines={lines.filter((l) => l.category === "Revenue")}
            editable={editable}
            busy={busy}
            confirmRemoveCodeId={confirm?.kind === "remove" ? confirm.codeId : null}
            onAdd={() => openModal("Revenue", null)}
            onEdit={(line) => openModal("Revenue", line)}
            onRemove={removeLine}
          />
          <AllocationSection
            category="Expense"
            lines={lines.filter((l) => l.category === "Expense")}
            editable={editable}
            busy={busy}
            confirmRemoveCodeId={confirm?.kind === "remove" ? confirm.codeId : null}
            onAdd={() => openModal("Expense", null)}
            onEdit={(line) => openModal("Expense", line)}
            onRemove={removeLine}
          />
        </>
      )}

      {modal && loaded && (
        <BudgetAllocationFormModal
          periodId={periodId}
          periodLabel={period.label}
          category={modal.category}
          codes={codes}
          lines={lines}
          line={modal.line}
          onClose={() => setModal(null)}
          onSaved={handleSaved}
        />
      )}
    </div>
  );
}
