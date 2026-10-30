"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { SelectField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import {
  codeCopyOutcomeSummary,
  copySourceCandidates,
  defaultCopySource,
  PERIOD_STATE_LABELS,
  type BudgetCodeCopyResult,
} from "@/lib/api/budgeting";
import { EmptyNote } from "@/components/screens/shared";

// "Copy codes" — seed this period's chart of budget codes from another period's, in one step.
// Codes belong to a period (re-justified from zero each period, architecture §5.3), so every new
// period starts with an empty chart; this is how last period's chart comes across without
// retyping it. The look and the behaviour mirror CopyFromPeriodPanel on the dashboard.
//
// What the server does (CopyBudgetCodesCommandHandler): every ACTIVE source code whose string
// this period does not already have is copied as an active code with a new id and every
// descriptive field; the hierarchy comes too (a copied child rolls up into this period's code
// with its parent's string, or sits top-level when there is none). Codes this period already has
// are skipped, so a second copy adds nothing; retired codes are not copied.
//
// The source picker offers every other period, in ANY state — a Closed period's chart is a
// perfectly good starting point. The server checks editability on the TARGET only, so the
// caller renders this panel only while the entered period accepts plan changes (canEditPlan).
// Source list and default come from copySourceCandidates / defaultCopySource, the items copy's
// own tested helpers: the guards are identical.
//
// Two-click confirm naming both periods; the screen owns the one-confirm-at-a-time rule, the
// request, the refetch and the error banner. The outcome is kept here so it survives until the
// planner changes the source.

export default function CopyCodesPanel({
  period,
  periods,
  busy,
  confirming,
  onRequestConfirm,
  onCancelConfirm,
  onCopy,
}: {
  /** The target — the entered period, whose chart the copy lands in. */
  period: BudgetPeriod;
  /** Every period, from Console. Filtered here by copySourceCandidates. */
  periods: BudgetPeriod[];
  busy: boolean;
  /** True once the first click has been made. */
  confirming: boolean;
  onRequestConfirm: () => void;
  onCancelConfirm: () => void;
  /**
   * Runs the copy and hands back the server's counts — or null when it was refused, in which
   * case the screen's banner carries the server's message verbatim.
   */
  onCopy: (sourcePeriodId: string) => Promise<BudgetCodeCopyResult | null>;
}) {
  const candidates = copySourceCandidates(periods, period.id);
  const [sourceId, setSourceId] = useState(() => defaultCopySource(periods, period.id)?.id ?? "");
  const [outcome, setOutcome] = useState<BudgetCodeCopyResult | null>(null);

  const source = candidates.find((p) => p.id === sourceId) ?? null;
  const intoLabel = period.label.toUpperCase();

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
      <SectionLabel>Copy codes from another period</SectionLabel>

      {candidates.length === 0 ? (
        <EmptyNote>
          There is no other budget period to copy codes from yet — this is the only one.
        </EmptyNote>
      ) : (
        <>
          <div
            style={{
              marginBottom: 10,
              fontFamily: fonts.body,
              fontSize: 12,
              color: colors.textSecondary,
            }}
          >
            Into: <strong>{period.label}</strong> (the period you&apos;re working in)
          </div>
          <SelectField
            label={`Copy codes into ${period.label} from`}
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
            hint="Any period, in any state — a closed period's chart is a perfectly good starting point"
            disabled={busy}
          />

          <div
            style={{ display: "flex", alignItems: "center", gap: 10, marginTop: 12, flexWrap: "wrap" }}
          >
            <ActionButton variant="primary" onClick={() => void run()} disabled={busy || !source}>
              {busy
                ? "COPYING…"
                : confirming
                  ? `CONFIRM COPY CODES INTO ${intoLabel}`
                  : `COPY CODES INTO ${intoLabel}`}
            </ActionButton>
            {confirming && !busy && <ActionButton onClick={onCancelConfirm}>CANCEL</ActionButton>}
          </div>

          {confirming && source && (
            <Note>
              This brings {source.label}&apos;s active budget codes into {period.label} — every
              detail, including which code rolls up into which. A code {period.label} already has
              (the same code string) is skipped, so copying twice adds nothing, and retired codes
              are not copied. Nothing changes in {source.label}. Click CONFIRM COPY CODES INTO{" "}
              {intoLabel} to proceed.
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
                {codeCopyOutcomeSummary(outcome)}
              </span>
            </div>
          )}
        </>
      )}
    </Panel>
  );
}

/** A quiet explanatory line, matching CopyFromPeriodPanel's. */
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
