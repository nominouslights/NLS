"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import type { HosStatus } from "@/lib/api/hos";
import { HOS_LIMITS } from "@/lib/hos/engine";
import { barPercent, blockingText, formatClockOn, formatRemaining, gaugeBars } from "@/lib/hos/display";
import { Panel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";

// The driver's standing right now, from GET /api/drivers/{id}/hos/status. Every figure is
// the server's evaluation (the engine mirror's HosEvaluation shape, verbatim); nothing here
// re-derives a limit. Each bar is colour + glyph + label through StatusChip, banded the way
// the engine bands (Over at 0, Soon inside the 2h window). `compact` is the Profile tab's
// summary: the four chips and the footer, no bars or detail lines.

export function HosStatusGauge({ status, compact = false }: { status: HosStatus; compact?: boolean }) {
  const ev = status.evaluation;
  const today = ev.today.date;
  const bars = gaugeBars(ev);
  const todayViolations = ev.violations.filter((v) => v.date === today && !v.suppressed);
  const next = ev.nextRequiredOffDuty;
  const mustStop = ev.shift.mustStopDrivingByUnix;
  const rest = ev.rest24;

  const heading = (
    <div
      style={{
        fontFamily: fonts.semiCondensed,
        fontSize: 9.5,
        letterSpacing: ".14em",
        textTransform: "uppercase",
        color: colors.textLabel,
      }}
    >
      Hours of Service · CVDHS Cycle 1 · as of {formatClockOn(ev.asOfUnix, today)}
    </div>
  );

  const footer = (
    <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 10, lineHeight: 1.5 }}>{status.label}</div>
  );

  if (compact) {
    return (
      <Panel style={{ marginBottom: 12, padding: "16px 18px", borderRadius: 11 }}>
        <div style={{ marginBottom: 10 }}>{heading}</div>
        <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
          {bars.map((b) => (
            <StatusChip key={b.key} kind={b.kind} label={`${b.title}: ${b.label}`} />
          ))}
        </div>
        {!status.recordsOnFile && (
          <div style={{ marginTop: 8 }}>
            <StatusChip kind="soon" label="Fewer than 14 days of records — not dispatchable" />
          </div>
        )}
        {!ev.canDriveNow && status.recordsOnFile && ev.blockingReasons.length > 0 && (
          <div style={{ marginTop: 8 }}>
            <StatusChip kind="over" label={`Cannot drive now — ${blockingText(ev.blockingReasons[0])}`} />
          </div>
        )}
        {footer}
      </Panel>
    );
  }

  return (
    <Panel style={{ marginBottom: 12, padding: "16px 18px", borderRadius: 11 }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 10, marginBottom: 12 }}>
        {heading}
        <StatusChip
          kind={ev.canDriveNow ? "ontime" : "over"}
          label={ev.canDriveNow ? `Can drive · ${formatRemaining(ev.tightestRemainingSeconds)} to the tightest limit` : "Cannot drive now"}
        />
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px 18px" }}>
        {bars.map((b) => {
          const m = statusMeta(b.kind);
          return (
            <div key={b.key}>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 5 }}>
                <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted }}>{b.title}</span>
                <StatusChip kind={b.kind} label={b.label} />
              </div>
              <div
                style={{
                  height: 8,
                  borderRadius: 5,
                  background: colors.inputBg,
                  overflow: "hidden",
                  border: `1px solid ${colors.borderSubtle}`,
                }}
              >
                <div style={{ height: "100%", width: `${barPercent(b)}%`, background: m.c, borderRadius: 5 }} />
              </div>
            </div>
          );
        })}
      </div>

      <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginTop: 14 }}>
        {mustStop !== null ? (
          <StatusChip kind={ev.canDriveNow ? "info" : "over"} glyph="◷" label={`Must stop driving by ${formatClockOn(mustStop, today)}`} />
        ) : (
          <StatusChip kind="off" label="Between shifts — no driving window open" />
        )}
        {next !== null && (
          <StatusChip
            kind="info"
            glyph="◷"
            label={`Next required off: ${formatRemaining(next.minimumSeconds)} by ${formatClockOn(next.mustBeOffByUnix, today)}`}
          />
        )}
        {rest.satisfied ? (
          <StatusChip
            kind="ontime"
            label={`24h off in last 14 days${rest.expiresAtUnix !== null ? ` · valid to ${formatClockOn(rest.expiresAtUnix, today)}` : ""}`}
          />
        ) : rest.recordsIncomplete ? (
          <StatusChip kind="soon" label="24h off in last 14 days — records incomplete" />
        ) : (
          <StatusChip kind="over" label={`No 24h off in last 14 days (longest ${formatRemaining(rest.bestOffSeconds)})`} />
        )}
        {ev.pcTodayDecikm > 0 && (
          <StatusChip
            kind={ev.pcRemainingDecikm === 0 ? "over" : "info"}
            glyph="◇"
            label={`Personal conveyance ${(ev.pcTodayDecikm / 10).toFixed(1)} km of ${HOS_LIMITS.pcDailyLimitDecikm / 10} km today`}
          />
        )}
      </div>

      {!status.recordsOnFile && (
        <div style={{ marginTop: 12 }}>
          <StatusChip kind="soon" label="Fewer than 14 days of records — not dispatchable" />
          {status.knownFromUtc && (
            <div style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, marginTop: 5 }}>
              Records on file from {status.knownFromUtc.slice(0, 10)}.
            </div>
          )}
        </div>
      )}

      {(ev.blockingReasons.length > 0 || todayViolations.length > 0) && (
        <div style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 6 }}>
          {ev.blockingReasons.map((r) => (
            <StatusChip key={r} kind="over" label={blockingText(r)} />
          ))}
          {todayViolations.map((v, i) => (
            <StatusChip key={`${v.rule}-${i}`} kind="over" label={v.message} />
          ))}
        </div>
      )}

      {status.outOfService !== null && (
        <div style={{ marginTop: 12 }}>
          <StatusChip kind="over" glyph="✕" label={`Out of service — ${status.outOfService.reason}`} />
        </div>
      )}
      {status.eldMalfunctionOpen && (
        <div style={{ marginTop: 12 }}>
          <StatusChip kind="soon" glyph="✎" label="ELD malfunction open — paper log in effect" />
        </div>
      )}

      {footer}
    </Panel>
  );
}
