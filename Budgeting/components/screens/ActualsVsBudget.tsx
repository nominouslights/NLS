"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface } from "@/lib/theme";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { MetricTile } from "@/components/ui/MetricTile";
import type { BudgetCode, BudgetPeriod } from "@/lib/types";
import { varianceKind, varianceLabel } from "@/lib/data";
import { formatCadPrecise, formatDeltaCad } from "@/lib/money";
import { ApiError } from "@/lib/api/transport";
import {
  budgetRollup,
  listActualTransactions,
  listBudgetCodes,
  refetchUntil,
  removeActualTransaction,
  rollupDeltaPct,
  toBudgetCode,
  toBudgetRollup,
  ACTUAL_SOURCE_LABELS,
  type ActualTransactionRecord,
  type BudgetRollupGroup,
  type BudgetRollupRowRecord,
} from "@/lib/api/budgeting";
import { ErrorNotice } from "@/components/ErrorNotice";
import ActualTransactionFormModal from "@/components/ActualTransactionFormModal";
import { usePeriodHold } from "@/lib/periodHold";
import { EmptyNote, Num, Screen, TableHead } from "@/components/screens/shared";

// Plan against reality, on real data: GET periods/{id}/rollup for the per-code comparison, GET
// periods/{id}/actuals for the transactions behind it, and the three writes that maintain them.
//
// Two things the server deliberately does not send, and this screen therefore owns:
//
//   * **The variance band.** The rollup ships planned, actual and the signed delta and no
//     threshold at all, so varianceKind / varianceLabel (lib/data.ts) stay the one place the
//     bands are decided — the Variance screen and any future report read the same function.
//   * **The revenue/expense split.** One row per code comes back ordered by code; toBudgetRollup
//     groups it the way the period dashboard groups its lines, and never adds the two sides
//     together — a "total planned" spanning both would be money in and money out in one figure.
//
// Every delta is written with its sign (formatDeltaCad) and carries the band's chip — glyph and
// words — so the direction survives a grayscale print and any colour-vision deficiency.
//
// Every figure prints through formatCadPrecise and every client-side total is summed in cents
// (toBudgetRollup → sumCad): planned items and actuals both carry cents, and a $3.03 plan beside
// a $3 actual must not read "$3 · $3 · −$0.03".
//
// Reads are projections: after a write the modal refetches the affected period's transactions
// until the new VALUES are visible, and only then does this screen refresh the rollup, whose
// totals read from the same projection.
//
// Shows the entered period only (Console, lib/workingPeriod.ts) — there is no picker here; the
// banner's SWITCH PERIOD is the one way to look at another period, and Console remounts this
// screen on every switch. The chart fetched here is the ENTERED period's (codes belong to a
// period); the modal fetches another period's chart itself when a date lands outside this one.
// A remove takes the period hold, like every other write against the entered period.

export default function ActualsVsBudget({
  period,
  periods,
}: {
  /** The entered period. */
  period: BudgetPeriod;
  /** Every period — the record modal needs them to say which period a date counts in. */
  periods: BudgetPeriod[];
}) {
  const periodId = period.id;
  /**
   * The rollup and the transactions behind it, carrying the period they were fetched for, so a
   * late response for another period is recognisably stale.
   */
  const [data, setData] = useState<{
    periodId: string;
    rows: BudgetRollupRowRecord[];
    transactions: ActualTransactionRecord[];
  } | null>(null);
  const [codes, setCodes] = useState<BudgetCode[] | null>(null);
  /** The last chart fetch failed — with codes still null, recording and correcting are blocked. */
  const [codesFailed, setCodesFailed] = useState(false);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);
  const [busy, setBusy] = useState(false);
  /** Two-click remove: holds the transaction id awaiting its confirming click. */
  const [confirmRemoveId, setConfirmRemoveId] = useState<string | null>(null);
  const [modal, setModal] = useState<{ transaction: ActualTransactionRecord | null } | null>(null);
  usePeriodHold(busy);

  const applyError = useCallback((e: unknown) => {
    setError(
      e instanceof ApiError
        ? { message: e.message, code: e.code }
        : { message: "Something went wrong — please try again.", code: "Unknown" },
    );
  }, []);

  /**
   * The period's reads, together: the rollup and its transactions, and the entered period's
   * chart — codes belong to a period, and a new transaction dated inside this period must be
   * tagged with one of THIS period's codes. One load, one error state, so RETRY refetches all of
   * it and one read's success never wipes the other's failure. The two settle independently: a
   * chart that fails still lets the rollup show, and the record button says why it is disabled.
   *
   * then-callbacks per the Stops.tsx lint idiom; isActive guards a late resolve on unmount, and
   * the fetched period travels with the result so a response that lands after the picker moved
   * on is recognisably stale rather than shown under the wrong period's name.
   */
  const load = useCallback(
    (isActive: () => boolean = () => true) => {
      Promise.allSettled([
        Promise.all([budgetRollup(periodId), listActualTransactions(periodId)]),
        listBudgetCodes(periodId),
      ]).then(([reads, chart]) => {
        if (!isActive()) return;
        if (reads.status === "fulfilled") {
          const [rows, transactions] = reads.value;
          setData({ periodId, rows, transactions });
        } else {
          setData({ periodId, rows: [], transactions: [] });
        }
        if (chart.status === "fulfilled") {
          setCodes(chart.value.map(toBudgetCode));
          setCodesFailed(false);
        } else {
          // A chart already loaded stays usable; the flag only matters while codes is null.
          setCodesFailed(true);
        }
        if (reads.status === "rejected") applyError(reads.reason);
        else if (chart.status === "rejected") applyError(chart.reason);
        else setError(null);
      });
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

  // Data fetched for another period is not this period's data — it is stale, and reads as
  // loading rather than as figures under the wrong heading.
  const loaded = data !== null && data.periodId === periodId;
  const transactions = loaded ? data.transactions : [];
  const rollup = toBudgetRollup(loaded ? data.rows : []);
  const label = period.label;
  /** No chart to pick from: the load failed and nothing loaded before it. */
  const codesUnavailable = codes === null && codesFailed;

  /** Called once a write is visible in the projection: the rollup is what depends on it. */
  function refresh() {
    setError(null);
    load();
  }

  async function removeTransaction(t: ActualTransactionRecord) {
    if (busy) return;
    if (confirmRemoveId !== t.id) {
      setConfirmRemoveId(t.id);
      return;
    }
    setConfirmRemoveId(null);
    setBusy(true);
    setError(null);
    try {
      await removeActualTransaction(t.id);
      // Refetch until the row is gone from the projection, and only then refresh the rollup,
      // whose totals read from the same one.
      const rest = await refetchUntil(
        () => listActualTransactions(periodId),
        (list) => !list.some((r) => r.id === t.id),
      );
      setData({ periodId, rows: await budgetRollup(periodId), transactions: rest });
    } catch (e) {
      applyError(e);
    } finally {
      setBusy(false);
    }
  }

  function openModal(transaction: ActualTransactionRecord | null) {
    // Without a chart the form has nothing to offer; the note by the record button says so.
    if (codes === null) return;
    setConfirmRemoveId(null);
    setModal({ transaction });
  }

  return (
    <Screen
      eyebrow={`Performance · ${label}`}
      title="Actuals vs Budget"
    >
      {error && (
        <div style={{ marginBottom: 12 }}>
          <ErrorNotice title="Actuals" message={error.message} code={error.code} />
          <div style={{ marginTop: 9 }}>
            <ActionButton onClick={() => load()}>RETRY</ActionButton>
          </div>
        </div>
      )}

      <>
        {/* "…" rather than $0 until the rollup lands: a zero is a figure, and a figure that
            is about to change is worse than an obvious absence. */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(190px, 1fr))",
            gap: 12,
            marginBottom: 18,
          }}
        >
          <MetricTile
            icon="◧"
            iconBg="rgba(31,111,178,.10)"
            iconColor={colors.blue}
            label="Planned revenue"
            value={loaded ? formatCadPrecise(rollup.revenue.plannedCad) : "…"}
            valueColor={colors.headingBright}
          />
          <MetricTile
            icon="◧"
            iconBg="rgba(31,111,178,.10)"
            iconColor={colors.blue}
            label="Actual revenue"
            value={loaded ? formatCadPrecise(rollup.revenue.actualCad) : "…"}
            valueColor={colors.headingBright}
          />
          <MetricTile
            icon="●"
            iconBg="rgba(31,111,178,.10)"
            iconColor={colors.blue}
            label="Planned expense"
            value={loaded ? formatCadPrecise(rollup.expense.plannedCad) : "…"}
            valueColor={colors.headingBright}
          />
          <MetricTile
            icon="●"
            iconBg="rgba(31,111,178,.10)"
            iconColor={colors.blue}
            label="Actual expense"
            value={loaded ? formatCadPrecise(rollup.expense.actualCad) : "…"}
            valueColor={colors.headingBright}
          />
        </div>

        {!loaded && !error && <EmptyNote>Loading the rollup…</EmptyNote>}

        {loaded && rollup.empty ? (
          <EmptyNote>
            Nothing planned and nothing spent in {label} yet. Plan an item on the Period Dashboard,
            or record a transaction below.
          </EmptyNote>
        ) : (
          loaded && (
            <>
              <RollupSection group={rollup.revenue} />
              <RollupSection group={rollup.expense} />
            </>
          )
        )}

        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            margin: "18px 0 10px",
            flexWrap: "wrap",
          }}
        >
          <div style={sectionLabelStyle}>Transactions</div>
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
            {transactions.length} recorded in {label}
          </span>
          <ActionButton
            variant="primary"
            onClick={() => openModal(null)}
            disabled={busy || codes === null}
            style={{ marginLeft: "auto" }}
          >
            + RECORD TRANSACTION
          </ActionButton>
        </div>
        {codesUnavailable && (
          <EmptyNote>
            {label}&apos;s budget codes could not be loaded, so transactions cannot be recorded or
            corrected yet — RETRY above loads them again.
          </EmptyNote>
        )}

        {loaded && transactions.length === 0 ? (
          <EmptyNote>
            No transactions recorded in {label} yet — record what was actually spent or received,
            and it rolls up against the plan above.
          </EmptyNote>
        ) : (
          loaded && (
            <>
              <TableHead
                columns={[{ label: "Date · code" }, { label: "Amount", align: "right" }, { label: "", align: "right" }]}
              />
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {transactions.map((t) => (
                  <div
                    key={t.id}
                    onClick={() => openModal(t)}
                    style={{
                      ...rowSurface(false),
                      cursor: codes === null ? "default" : "pointer",
                      padding: "11px 14px",
                      display: "flex",
                      alignItems: "center",
                      gap: 12,
                    }}
                  >
                    <div style={{ flex: "1 1 auto", minWidth: 0 }}>
                      <div
                        style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}
                      >
                        {/* The ISO date, not a localized one: new Date("2026-10-05").toLocaleDateString
                            renders the day before west of UTC, and a ledger row must never
                            disagree with the date that was typed. */}
                        <Num>{t.occurredOn}</Num>
                        <MonoTag>{t.code}</MonoTag>
                        <span
                          style={{
                            fontFamily: fonts.body,
                            fontWeight: 600,
                            fontSize: 12.5,
                            color: colors.textPrimary,
                          }}
                        >
                          {t.name}
                        </span>
                        {!t.isCodeActive && <StatusChip kind="off" label="Retired code" />}
                        {t.source !== "Manual" && (
                          <StatusChip kind="info" label={ACTUAL_SOURCE_LABELS[t.source]} />
                        )}
                      </div>
                      <div
                        style={{
                          fontFamily: fonts.body,
                          fontSize: 11.5,
                          color: colors.textDim,
                          marginTop: 3,
                          lineHeight: 1.5,
                        }}
                      >
                        {t.description}
                      </div>
                    </div>
                    <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                      <Num size={13.5}>{formatCadPrecise(t.amountCad)}</Num>
                    </div>
                    <div
                      style={{ width: 150, textAlign: "right", flex: "none" }}
                      // The row itself opens the editor; a click on the remove cell must not.
                      // ActionButton's onClick carries no event, so the cell stops the bubble.
                      onClick={(e) => e.stopPropagation()}
                    >
                      <ActionButton
                        variant="destructive"
                        disabled={busy}
                        onClick={() => removeTransaction(t)}
                        style={{ padding: "5px 10px", fontSize: 12 }}
                      >
                        {confirmRemoveId === t.id ? "CONFIRM REMOVE" : "REMOVE"}
                      </ActionButton>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )
        )}

        {modal && codes !== null && (
          <ActualTransactionFormModal
            periods={periods}
            openPeriod={period}
            openPeriodCodes={codes}
            transaction={modal.transaction}
            onClose={() => setModal(null)}
            onSaved={refresh}
          />
        )}
      </>
    </Screen>
  );
}

/**
 * One category's rollup — planned, actual and the signed delta per code, with the category's own
 * totals in the header. Rendered twice, Revenue then Expense, mirroring the period dashboard's
 * two allocation sections so the plan and the spend against it read the same way.
 */
function RollupSection({ group }: { group: BudgetRollupGroup }) {
  const revenue = group.category === "Revenue";
  const pct = group.plannedCad === 0 ? null : (group.deltaCad / group.plannedCad) * 100;

  return (
    <div style={{ marginBottom: 18 }}>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 10,
          marginBottom: 10,
          flexWrap: "wrap",
        }}
      >
        <div style={sectionLabelStyle}>{revenue ? "Revenue codes" : "Expense codes"}</div>
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          {group.rows.length} · planned {formatCadPrecise(group.plannedCad)} · actual{" "}
          {formatCadPrecise(group.actualCad)}
        </span>
        <span style={{ display: "inline-flex", alignItems: "center", gap: 8, marginLeft: "auto" }}>
          <StatusChip kind={varianceKind(pct)} label={varianceLabel(pct)} />
          <Num size={13.5}>{formatDeltaCad(group.deltaCad)}</Num>
        </span>
      </div>

      {group.rows.length === 0 ? (
        <EmptyNote>
          {revenue
            ? "No revenue planned and none recorded for this period."
            : "No expense planned and none recorded for this period."}
        </EmptyNote>
      ) : (
        <>
          <TableHead
            columns={[
              { label: "Code" },
              { label: "Planned", align: "right" },
              { label: "Actual", align: "right" },
              { label: "Delta", align: "right" },
            ]}
          />
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {group.rows.map((r) => {
              const rowPct = rollupDeltaPct(r);
              return (
                <div
                  key={r.budgetCodeId}
                  style={{
                    ...rowSurface(false),
                    padding: "11px 14px",
                    display: "flex",
                    alignItems: "center",
                    gap: 12,
                    cursor: "default",
                  }}
                >
                  <div style={{ flex: "1 1 auto", minWidth: 0 }}>
                    <div
                      style={{ display: "flex", alignItems: "center", gap: 9, flexWrap: "wrap" }}
                    >
                      <MonoTag>{r.code}</MonoTag>
                      <span
                        style={{
                          fontFamily: fonts.body,
                          fontWeight: 600,
                          fontSize: 12.5,
                          color: colors.textPrimary,
                        }}
                      >
                        {r.name}
                      </span>
                      <StatusChip kind={varianceKind(rowPct)} label={varianceLabel(rowPct)} />
                    </div>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num>{formatCadPrecise(r.plannedCad)}</Num>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num>{formatCadPrecise(r.actualCad)}</Num>
                  </div>
                  <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                    <Num>{formatDeltaCad(r.deltaCad)}</Num>
                  </div>
                </div>
              );
            })}
          </div>
        </>
      )}
    </div>
  );
}

const sectionLabelStyle = {
  fontFamily: fonts.semiCondensed,
  fontSize: 9.5,
  letterSpacing: ".14em",
  textTransform: "uppercase" as const,
  color: colors.textLabel,
};
