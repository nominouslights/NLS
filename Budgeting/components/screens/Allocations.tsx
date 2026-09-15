"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { BudgetCode, BudgetCodeCategory, BudgetPeriod } from "@/lib/types";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { MetricTile } from "@/components/ui/MetricTile";
import { ActionButton } from "@/components/ui/Button";
import { formatCad } from "@/lib/api/format";
import { formatDeltaCad } from "@/lib/money";
import { ApiError } from "@/lib/api/transport";
import {
  budgetCodeCategoryKind,
  canEditAllocations,
  listBudgetAllocations,
  listBudgetCodes,
  netKind,
  netLabel,
  toBudgetCode,
  PERIOD_STATE_LABELS,
  SERVICE_LINE_LABELS,
  type BudgetAllocationRecord,
} from "@/lib/api/budgeting";
import { ErrorNotice } from "@/components/ErrorNotice";
import BudgetAllocationFormModal from "@/components/BudgetAllocationFormModal";
import {
  EmptyNote,
  Num,
  PeriodPicker,
  Screen,
  TableHead,
  periodLabel,
} from "@/components/screens/shared";

// Every line of the selected period in one flat list, on real data
// (GET /api/budgeting/periods/{id}/allocations). The period dashboard is where planning happens
// section by section; this screen is the whole plan at a glance, and TopBar's "+ Allocation"
// still lands here. A row click jumps to the line's code on the Budget Codes screen — a real id
// now, so the pane opens on the right code.
//
// The three tiles are summed from the lines on screen rather than read from the period record:
// this screen has no way to refresh Console's period list after a save, and a tile that
// disagreed with the list beneath it would be worse than one that trails the dashboard by a
// load. The dashboard shows the server's own totals.

export default function Allocations({
  periods,
  periodId,
  onSelectPeriod,
  onOpenCode,
}: {
  periods: BudgetPeriod[];
  periodId: string;
  onSelectPeriod: (id: string) => void;
  onOpenCode: (id: string) => void;
}) {
  const period = periods.find((p) => p.id === periodId) ?? null;

  // Lines are tagged with the period they were fetched for, so switching periods shows the
  // loading state rather than the previous period's rows under the new header.
  const [loaded, setLoaded] = useState<{ periodId: string; rows: BudgetAllocationRecord[] } | null>(
    null,
  );
  const [codes, setCodes] = useState<BudgetCode[]>([]);
  const [error, setError] = useState<{ message: string; code: string } | null>(null);
  const [modal, setModal] = useState<BudgetCodeCategory | null>(null);

  const applyError = useCallback((e: unknown) => {
    setError(
      e instanceof ApiError
        ? { message: e.message, code: e.code }
        : { message: "Failed to load the period's lines.", code: "Unknown" },
    );
  }, []);

  const load = useCallback(
    (isActive: () => boolean = () => true) => {
      if (!periodId) return;
      listBudgetAllocations(periodId).then(
        (rows) => {
          if (isActive()) {
            setLoaded({ periodId, rows });
            setError(null);
          }
        },
        (e) => {
          if (isActive()) {
            setLoaded({ periodId, rows: [] });
            applyError(e);
          }
        },
      );
      // The picker's options. A failure here only empties the picker; the list still renders.
      listBudgetCodes().then(
        (records) => {
          if (isActive()) setCodes(records.map(toBudgetCode));
        },
        () => {},
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

  const lines = loaded?.periodId === periodId ? loaded.rows : null;
  const rows = lines ?? [];

  const plannedRevenue = rows
    .filter((l) => l.category === "Revenue")
    .reduce((sum, l) => sum + l.amountCad, 0);
  const plannedExpense = rows
    .filter((l) => l.category === "Expense")
    .reduce((sum, l) => sum + l.amountCad, 0);
  const net = plannedRevenue - plannedExpense;
  const netMeta = statusMeta(netKind(net));

  const editable = period !== null && canEditAllocations(period.state);

  function handleSaved(fresh: BudgetAllocationRecord[]) {
    setLoaded({ periodId, rows: fresh });
    setError(null);
  }

  return (
    <Screen
      eyebrow={`Planning · ${periodLabel(periods, periodId)}`}
      title="Allocations"
      right={<PeriodPicker periods={periods} periodId={periodId} onSelect={onSelectPeriod} />}
    >
      {period === null ? (
        <EmptyNote>No budget periods yet — create one under Budget Periods to start planning.</EmptyNote>
      ) : (
        <>
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
              value={formatCad(plannedRevenue)}
              valueColor={colors.headingBright}
            />
            <MetricTile
              icon="●"
              iconBg="rgba(31,111,178,.10)"
              iconColor={colors.blue}
              label="Planned expense"
              value={formatCad(plannedExpense)}
              valueColor={colors.headingBright}
            />
            {/* Glyph + "Surplus/Balanced/Deficit" + a signed figure; the colour never stands alone. */}
            <MetricTile
              icon={netMeta.g}
              iconBg={netMeta.bg}
              iconColor={netMeta.t}
              label={`Net · ${netLabel(net)}`}
              value={formatDeltaCad(net)}
              valueColor={netMeta.t}
            />
          </div>

          {error && (
            <div style={{ marginBottom: 12 }}>
              <ErrorNotice title="Allocations" message={error.message} code={error.code} />
              <div style={{ marginTop: 9 }}>
                <ActionButton onClick={() => load()}>RETRY</ActionButton>
              </div>
            </div>
          )}

          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 10,
              marginBottom: 10,
              flexWrap: "wrap",
            }}
          >
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontSize: 9.5,
                letterSpacing: ".14em",
                textTransform: "uppercase",
                color: colors.textLabel,
              }}
            >
              {rows.length} {rows.length === 1 ? "line" : "lines"}
            </div>
            <StatusChip kind={period.pk} label={PERIOD_STATE_LABELS[period.state]} />
            {editable && (
              <div style={{ marginLeft: "auto", display: "flex", gap: 8 }}>
                <ActionButton variant="primary" onClick={() => setModal("Revenue")} disabled={lines === null}>
                  + REVENUE
                </ActionButton>
                <ActionButton onClick={() => setModal("Expense")} disabled={lines === null}>
                  + EXPENSE
                </ActionButton>
              </div>
            )}
          </div>

          {!editable && (
            <div style={{ marginBottom: 12 }}>
              <EmptyNote>
                This period is {PERIOD_STATE_LABELS[period.state]}; its plan is read-only. Lines
                can change only while the period is Draft or Open.
              </EmptyNote>
            </div>
          )}

          {lines === null && !error && <EmptyNote>Loading lines…</EmptyNote>}

          {lines !== null && rows.length === 0 && (
            <EmptyNote>
              Nothing planned in {period.label} yet — set revenue and budget lines here or from
              the period&apos;s dashboard.
            </EmptyNote>
          )}

          {rows.length > 0 && (
            <>
              <TableHead columns={[{ label: "Code" }, { label: "Amount", align: "right" }]} />
              <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                {rows.map((l) => (
                  <div
                    key={l.id}
                    onClick={() => onOpenCode(l.budgetCodeId)}
                    style={{
                      ...rowSurface(false),
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
                        <MonoTag>{l.code}</MonoTag>
                        <span
                          style={{
                            fontFamily: fonts.body,
                            fontWeight: 600,
                            fontSize: 12.5,
                            color: colors.textPrimary,
                          }}
                        >
                          {l.name}
                        </span>
                        <StatusChip kind={budgetCodeCategoryKind(l.category)} label={l.category} />
                        {!l.isCodeActive && <StatusChip kind="off" label="Retired" />}
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
                        {l.serviceLine ? `${SERVICE_LINE_LABELS[l.serviceLine]} · ` : ""}
                        {l.justification}
                      </div>
                    </div>
                    <div style={{ width: 150, textAlign: "right", flex: "none" }}>
                      <Num size={13.5}>{formatCad(l.amountCad)}</Num>
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}

          {modal && lines !== null && (
            <BudgetAllocationFormModal
              periodId={period.id}
              periodLabel={period.label}
              category={modal}
              codes={codes}
              lines={lines}
              line={null}
              onClose={() => setModal(null)}
              onSaved={handleSaved}
            />
          )}
        </>
      )}
    </Screen>
  );
}
