"use client";

import { useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { formatCad } from "@/lib/api/format";
import { formatUtcDate } from "@/lib/api/format";
import type { BudgetPeriodRecord } from "@/lib/api/budgeting";
import { ErrorNotice } from "@/components/ErrorNotice";
import BudgetPeriodFormModal from "@/components/BudgetPeriodFormModal";
import { EmptyNote, Num, Screen } from "@/components/screens/shared";

// Budget periods and where each one stands — the first screen on real data
// (GET /api/budgeting/periods; Console owns the fetch). Draft → Open → Locked, mapped onto the
// shared status kinds: a locked period is finished ("off"), an open one is the live plan
// ("ontime"), a draft is informational until someone opens it ("info"). Each row's kind is
// carried on the row (BudgetPeriod.pk, derived in lib/api/budgeting.ts), so this screen never
// picks a colour itself.
//
// Planned/Allocated figures are honest zeros until the allocations slice of Stage 6.1 lands —
// the wire has no such fields yet.

export default function BudgetPeriods({
  periods,
  error,
  onRetry,
  periodId,
  onSelectPeriod,
  onCreated,
}: {
  /** null while the first load is in flight. */
  periods: BudgetPeriod[] | null;
  error: { message: string; code: string } | null;
  onRetry: () => void;
  periodId: string;
  onSelectPeriod: (id: string) => void;
  onCreated: (records: BudgetPeriodRecord[], id: string) => void;
}) {
  const [showCreate, setShowCreate] = useState(false);

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

      {periods === null && !error && (
        <EmptyNote>Loading budget periods…</EmptyNote>
      )}

      {periods !== null && periods.length === 0 && !error && (
        <EmptyNote>No budget periods yet — create one to start planning.</EmptyNote>
      )}

      <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
        {(periods ?? []).map((p) => {
          const active = p.id === periodId;
          const m = statusMeta(p.pk);
          const unallocated = p.planned - p.allocated;

          return (
            <div
              key={p.id}
              onClick={() => onSelectPeriod(p.id)}
              style={{ ...rowSurface(active, m.c), padding: "13px 15px" }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
                <div style={{ flex: "1 1 auto", minWidth: 0 }}>
                  <div
                    style={{
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 17,
                      color: colors.headingBright,
                      lineHeight: 1.2,
                    }}
                  >
                    {p.label}
                  </div>
                  <div
                    style={{
                      fontFamily: fonts.body,
                      fontSize: 11.5,
                      color: colors.textDim,
                      marginTop: 2,
                    }}
                  >
                    {formatUtcDate(p.startsOn)} — {formatUtcDate(p.endsOn)}
                  </div>
                </div>

                <StatusChip kind={p.pk} label={p.state} />

                <div style={{ display: "flex", gap: 22, flex: "none" }}>
                  <Figure label="Planned" value={formatCad(p.planned)} />
                  <Figure label="Allocated" value={formatCad(p.allocated)} />
                  <Figure
                    label="Unallocated"
                    value={formatCad(unallocated)}
                    // Amber is a fill/border colour, never text — theme.ts documents this, so
                    // an unallocated balance uses amberText to stay AA on white.
                    color={unallocated > 0 ? colors.amberText : colors.textSecondary}
                  />
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {showCreate && (
        <BudgetPeriodFormModal onClose={() => setShowCreate(false)} onSaved={onCreated} />
      )}
    </Screen>
  );
}

function Figure({ label, value, color }: { label: string; value: string; color?: string }) {
  return (
    <div style={{ textAlign: "right" }}>
      <div
        style={{
          fontFamily: fonts.semiCondensed,
          fontSize: 9.5,
          letterSpacing: ".14em",
          textTransform: "uppercase",
          color: colors.textLabel,
          marginBottom: 2,
        }}
      >
        {label}
      </div>
      <Num color={color}>{value}</Num>
    </div>
  );
}
