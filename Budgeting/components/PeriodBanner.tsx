"use client";

import { colors, fonts } from "@/lib/theme";
import type { BudgetPeriod } from "@/lib/types";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { formatUtcDate } from "@/lib/api/format";
import { canEditAllocations, PERIOD_STATE_LABELS } from "@/lib/api/budgeting";

// The strip at the top of the main column that always says which period the planner is in.
// It lives here rather than in the TopBar because the TopBar's geometry matches Dispatcher's
// and is part of what makes the two consoles read as one product.
//
// Three shapes:
//   - a period-scoped screen, period entered: WORKING IN · label · dates · state · editability,
//     and SWITCH PERIOD — the only way out of a period;
//   - Settings (the one screen not tied to a period — budget codes belong to a period now),
//     period entered: the same identity plus a sentence saying this screen is not tied to it;
//   - Settings, nothing entered: "No period entered" and CHOOSE A PERIOD.
// A period-scoped screen with nothing entered renders the chooser in place of the screen, so
// the banner has nothing to add there and renders nothing.
//
// While a hold is taken (lib/periodHold.ts) SWITCH PERIOD is disabled, and the reason is
// written beside it — a disabled control with no explanation reads as a bug.

export default function PeriodBanner({
  period,
  scoped,
  held,
  onSwitch,
  onChoose,
}: {
  /** The entered period, or null when none is. */
  period: BudgetPeriod | null;
  /** Whether the current screen acts on one period (lib/nav.ts isPeriodScoped). */
  scoped: boolean;
  /** True while a request against the entered period is in flight. */
  held: boolean;
  onSwitch: () => void;
  /** Settings with nothing entered: go to the chooser. */
  onChoose: () => void;
}) {
  if (!period && scoped) return null;

  return (
    <div
      role="region"
      aria-label="Working period"
      style={{
        flex: "none",
        minHeight: 44,
        display: "flex",
        alignItems: "center",
        gap: 12,
        flexWrap: "wrap",
        padding: "6px 26px",
        background: colors.cardBg,
        borderBottom: `1px solid ${colors.border}`,
      }}
    >
      {period ? (
        <>
          <Eyebrow>Working in</Eyebrow>
          <span
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 16,
              color: colors.headingBright,
            }}
          >
            {period.label}
          </span>
          <Dim>
            {formatUtcDate(period.startsOn)} — {formatUtcDate(period.endsOn)}
          </Dim>
          <StatusChip kind={period.pk} label={PERIOD_STATE_LABELS[period.state]} />
          {scoped ? (
            <Dim>{canEditAllocations(period.state) ? "Plan editable" : "Plan read-only"}</Dim>
          ) : (
            <Dim>This screen isn&apos;t tied to a period.</Dim>
          )}
          <span style={{ flex: "1 1 auto" }} />
          {held && <Dim>Finishing a change to {period.label}…</Dim>}
          <ActionButton onClick={onSwitch} disabled={held} style={{ padding: "5px 11px", fontSize: 12 }}>
            SWITCH PERIOD
          </ActionButton>
        </>
      ) : (
        <>
          <Eyebrow>No period entered</Eyebrow>
          <Dim>This screen isn&apos;t tied to a period.</Dim>
          <span style={{ flex: "1 1 auto" }} />
          <ActionButton onClick={onChoose} style={{ padding: "5px 11px", fontSize: 12 }}>
            CHOOSE A PERIOD
          </ActionButton>
        </>
      )}
    </div>
  );
}

function Eyebrow({ children }: { children: React.ReactNode }) {
  return (
    <span
      style={{
        fontFamily: fonts.semiCondensed,
        fontSize: 10,
        letterSpacing: ".16em",
        textTransform: "uppercase",
        color: colors.textLabel,
      }}
    >
      {children}
    </span>
  );
}

function Dim({ children }: { children: React.ReactNode }) {
  return (
    <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>{children}</span>
  );
}
