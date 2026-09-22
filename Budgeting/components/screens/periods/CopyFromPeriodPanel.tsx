"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { SelectField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import {
  copySourceCandidates,
  copyOutcomeSummary,
  defaultCopySource,
  PERIOD_STATE_LABELS,
  type BudgetAllocationCopyResult,
} from "@/lib/api/budgeting";
import { EmptyNote } from "@/components/screens/shared";

// "Start from last period" — zero-based budgeting's fifth step (a fresh plan before the period
// begins) without rebuilding eleven lines by hand, which is the reason people abandon ZBB.
//
// It lives beside the checklist rather than inside AllocationSection because a copy spans both
// categories at once, and only while the period accepts plan changes (canEditAllocations) —
// the caller decides that, since the dashboard already explains a read-only period in one note.
//
// The source picker offers periods in ANY state, the target excepted. A Closed period is a
// perfectly legal source: the server checks AllowsPlanChanges on the TARGET only, because
// copying a closed period's plan into a fresh Draft is the entire point. See
// copySourceCandidates — do not filter this list by canEditAllocations.
//
// Two-click confirm, matching the dashboard's transition and remove idiom.

export default function CopyFromPeriodPanel({
  period,
  periods,
  busy,
  confirming,
  onRequestConfirm,
  onCancelConfirm,
  onCopy,
}: {
  /** The target — the period whose plan the copy lands in. */
  period: BudgetPeriod;
  /** The whole list, from Console via BudgetPeriods. Filtered here by copySourceCandidates. */
  periods: BudgetPeriod[];
  busy: boolean;
  /** True once the first click has been made; the dashboard owns the one-confirm-at-a-time rule. */
  confirming: boolean;
  onRequestConfirm: () => void;
  onCancelConfirm: () => void;
  /**
   * Runs the copy. The dashboard owns the request, the refetch and the error banner, and hands
   * back the server's counts — or null when it refused, in which case its banner carries the
   * server's message verbatim and there is no outcome to report here.
   */
  onCopy: (sourcePeriodId: string) => Promise<BudgetAllocationCopyResult | null>;
}) {
  const candidates = copySourceCandidates(periods, period.id);
  const [sourceId, setSourceId] = useState(() => defaultCopySource(periods, period.id)?.id ?? "");
  /** The last completed copy's counts, so the outcome survives until the planner moves on. */
  const [outcome, setOutcome] = useState<BudgetAllocationCopyResult | null>(null);

  const source = candidates.find((p) => p.id === sourceId) ?? null;

  async function run() {
    if (!source || busy) return;
    if (!confirming) {
      onRequestConfirm();
      return;
    }
    setOutcome(null);
    setOutcome(await onCopy(source.id));
  }

  return (
    <Panel style={{ marginBottom: 14 }}>
      <SectionLabel>Start from an earlier period</SectionLabel>

      {candidates.length === 0 ? (
        <EmptyNote>
          There is no other budget period to copy from yet — this is the only one.
        </EmptyNote>
      ) : (
        <>
          <SelectField
            label="Copy from"
            value={sourceId}
            onChange={(v) => {
              setSourceId(v);
              setOutcome(null);
              onCancelConfirm();
            }}
            options={candidates.map((p) => ({
              value: p.id,
              label: `${p.label} · ${PERIOD_STATE_LABELS[p.state]}`,
            }))}
            hint="Any period, in any state — a closed period's plan is a perfectly good starting point"
            disabled={busy}
          />

          <div style={{ display: "flex", alignItems: "center", gap: 10, marginTop: 12, flexWrap: "wrap" }}>
            <ActionButton variant="primary" onClick={() => void run()} disabled={busy || !source}>
              {busy ? "COPYING…" : confirming ? "CONFIRM COPY" : "COPY THE AMOUNTS"}
            </ActionButton>
            {confirming && !busy && (
              <ActionButton onClick={onCancelConfirm}>CANCEL</ActionButton>
            )}
          </div>

          {confirming && source && (
            <Note>
              This brings {source.label}&apos;s amounts into {period.label} —{" "}
              <strong>the amounts only. Every justification is cleared</strong>, so each line must
              be argued again before it can be saved. Lines you already have here are left alone,
              and retired codes are skipped. Click CONFIRM COPY to proceed.
            </Note>
          )}

          {outcome && (
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 10,
                marginTop: 12,
                flexWrap: "wrap",
              }}
            >
              <StatusChip
                kind={outcome.copied > 0 ? "ontime" : "info"}
                label={outcome.copied > 0 ? "Copied" : "Nothing copied"}
              />
              <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textSecondary }}>
                {copyOutcomeSummary(outcome)}
              </span>
            </div>
          )}
        </>
      )}
    </Panel>
  );
}

/** A quiet explanatory line, matching the allocation modal's trailing note. */
function Note({ children }: { children: React.ReactNode }) {
  return (
    <div
      style={{
        marginTop: 10,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textDim,
        lineHeight: 1.6,
      }}
    >
      {children}
    </div>
  );
}
