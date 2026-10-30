"use client";

import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { NAV_GROUPS, type ScreenId } from "@/lib/nav";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { formatCad, formatUtcDate } from "@/lib/api/format";
import { PERIOD_STATE_LABELS } from "@/lib/api/budgeting";
import { suggestedPeriodId, suggestionReason } from "@/lib/workingPeriod";
import { ErrorNotice } from "@/components/ErrorNotice";
import { EmptyNote, Num, Screen } from "@/components/screens/shared";

// The way into a period. Every period-scoped screen renders this in its place until a period is
// entered, so no screen ever acts on a period the planner did not choose. It replaces the old
// master column (PeriodList) and owns the load states Budget Periods used to carry: loading,
// error with RETRY, empty, and "the period you were in is gone".
//
// Nothing is entered silently. On a first visit the period containing today — else the latest —
// is highlighted, tagged with why, and focused, so entering it is one keypress or one click.
//
// Each row carries the state as a StatusChip (glyph + label), so the accent stripe on the
// suggested row is never the only thing distinguishing it: it has its own written tag.

export default function PeriodChooser({
  periods,
  error,
  lost,
  returning,
  todayIso,
  forScreen,
  onEnter,
  onCreate,
  onRetry,
}: {
  /** null while the first load is in flight. */
  periods: BudgetPeriod[] | null;
  error: { message: string; code: string } | null;
  /** The stored period matched nothing after a good load. */
  lost: boolean;
  /** A period is remembered for this tab and the list is still loading. */
  returning: boolean;
  todayIso: string;
  /** Which screen the planner was heading for, so the eyebrow can say so. */
  forScreen: ScreenId;
  onEnter: (id: string) => void;
  onCreate: () => void;
  onRetry: () => void;
}) {
  const list = periods ?? [];
  const suggested = suggestedPeriodId(list, todayIso);
  const reason = suggestionReason(list, todayIso);
  const target = NAV_GROUPS.flatMap((g) => g.items).find((i) => i.id === forScreen)?.label;

  return (
    <Screen
      eyebrow={target ? `Choose a period to open ${target}` : "Choose a period"}
      title="Choose a Period"
      right={
        <ActionButton variant="primary" onClick={onCreate}>
          + NEW PERIOD
        </ActionButton>
      }
    >
      {lost && (
        <div
          role="status"
          style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12, flexWrap: "wrap" }}
        >
          <StatusChip kind="soon" label="Period unavailable" />
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary }}>
            The period you were working in is no longer available — choose another.
          </span>
        </div>
      )}

      {error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorNotice title="Couldn't load budget periods" message={error.message} code={error.code} />
          <div style={{ marginTop: 9 }}>
            <ActionButton onClick={onRetry}>RETRY</ActionButton>
          </div>
        </div>
      )}

      {periods === null && !error && (
        <EmptyNote>{returning ? "Returning to your period…" : "Loading budget periods…"}</EmptyNote>
      )}

      {periods !== null && list.length === 0 && !error && (
        <div>
          <EmptyNote>No budget periods yet — create one to start planning.</EmptyNote>
          <div style={{ marginTop: 12 }}>
            <ActionButton variant="primary" onClick={onCreate}>
              CREATE THE FIRST PERIOD
            </ActionButton>
          </div>
        </div>
      )}

      {list.length > 0 && (
        <div style={{ display: "flex", flexDirection: "column", gap: 8, maxWidth: 720 }}>
          {list.map((p) => {
            const isSuggested = p.id === suggested;
            const m = statusMeta(p.pk);
            return (
              <button
                key={p.id}
                type="button"
                aria-label={`Enter ${p.label}`}
                onClick={() => onEnter(p.id)}
                autoFocus={isSuggested}
                style={{
                  ...rowSurface(isSuggested, m.c),
                  padding: "11px 13px",
                  textAlign: "left",
                  width: "100%",
                  font: "inherit",
                  color: "inherit",
                }}
              >
                <div
                  style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 4, flexWrap: "wrap" }}
                >
                  <span
                    style={{
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 16,
                      color: colors.headingBright,
                      lineHeight: 1.2,
                      flex: "1 1 auto",
                      minWidth: 0,
                    }}
                  >
                    {p.label}
                  </span>
                  {isSuggested && reason && <MonoTag color={colors.blue}>{reason}</MonoTag>}
                  <StatusChip kind={p.pk} label={PERIOD_STATE_LABELS[p.state]} />
                </div>
                <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                  {formatUtcDate(p.startsOn)} — {formatUtcDate(p.endsOn)}
                </div>
                <div style={{ marginTop: 5, display: "flex", alignItems: "center", gap: 6 }}>
                  <Num size={11.5}>Rev {formatCad(p.plannedRevenue)}</Num>
                  <span style={{ color: colors.textDim, fontSize: 11.5 }}>·</span>
                  <Num size={11.5}>Exp {formatCad(p.plannedExpense)}</Num>
                </div>
              </button>
            );
          })}
        </div>
      )}
    </Screen>
  );
}
