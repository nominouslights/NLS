"use client";

import { useCallback } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { getHosAudit, type HosAuditDay } from "@/lib/api/hos";
import type { DriverRecord } from "@/lib/api/drivers";
import type { Period } from "@/lib/period";
import { datesBetween } from "@/lib/hos/compliance";
import { formatDateOnly } from "@/lib/hos/display";
import { PeriodNav } from "@/components/ui/PeriodNav";
import { Panel } from "@/components/ui/Panel";
import { DriverLink, EmptyLine, FanOutFailures, LoadingLine, useRosterFanOut } from "./shared";

// The compliance audit as a grid: one row per driver, one column per day of the month, from
// GET …/hos/audit per driver (the fan-out — see lib/hos/compliance.ts). Each cell spells out
// what the day holds — events · ✓ certified / ◷ not · ▲n violations · ◐n exceptions · PC km —
// so the tint is never the only signal. A day with no records is "—": unknown, not clean.

export interface AuditCellSummary {
  kind: StatusKind;
  lines: string[];
  description: string;
}

/** What one audit day shows, worst first: violations ▲, then uncertified ◷ / exceptions ◐, else ✓. */
export function auditCell(day: HosAuditDay | undefined): AuditCellSummary | null {
  if (!day || !day.hasRecords) return null;
  const violations = day.violations.length;
  const exceptions = day.exceptions.length;
  const pc = day.personalConveyanceKm;
  const lines = [`${day.eventCount} ev`, day.certified ? "✓ cert" : "◷ uncert"];
  if (violations > 0) lines.push(`▲${violations}`);
  if (exceptions > 0) lines.push(`◐${exceptions}`);
  if (pc > 0) lines.push(`PC ${pc.toFixed(1)} km`);
  const kind: StatusKind = violations > 0 || day.outOfService ? "over" : !day.certified || exceptions > 0 || day.recertificationRequired ? "soon" : "ontime";
  const description = [
    `${day.eventCount} event${day.eventCount === 1 ? "" : "s"}`,
    day.certified ? (day.recertificationRequired ? "certified, recertification required" : "certified") : "not certified",
    `${violations} violation${violations === 1 ? "" : "s"}`,
    `${exceptions} exception${exceptions === 1 ? "" : "s"}`,
    pc > 0 ? `personal conveyance ${pc.toFixed(1)} km` : null,
    day.outOfService ? "out of service" : null,
    day.eldMalfunctionOpen ? "ELD malfunction open" : null,
  ]
    .filter(Boolean)
    .join(", ");
  return { kind, lines, description };
}

const DRIVER_COL = 170;
const DAY_COL = 74;

export function AuditTable({
  roster,
  period,
  onPeriodChange,
  today,
  version,
  onOpenDriver,
}: {
  roster: DriverRecord[];
  period: Period;
  onPeriodChange: (p: Period) => void;
  today: string;
  version: number;
  onOpenDriver: (driverId: string) => void;
}) {
  // The audit is capped at today server-side; a future month asks for nothing.
  const to = period.end < today ? period.end : today;
  const future = period.start > today;
  const fetchOne = useCallback(
    (d: DriverRecord) => (future ? Promise.resolve([] as HosAuditDay[]) : getHosAudit(d.id, { from: period.start, to })),
    [future, period.start, to],
  );
  const { rows, failures } = useRosterFanOut(roster, fetchOne, version);
  const dates = datesBetween(period.start, period.end);

  const byDriver = new Map<string, Map<string, HosAuditDay>>();
  for (const r of rows ?? []) byDriver.set(r.driver.id, new Map(r.value.map((d) => [d.date, d])));

  // Totals row: per day, across drivers.
  const totals = dates.map((date) => {
    let events = 0;
    let violations = 0;
    let uncertified = 0;
    for (const days of byDriver.values()) {
      const d = days.get(date);
      if (!d?.hasRecords) continue;
      events += d.eventCount;
      violations += d.violations.length;
      if (!d.certified) uncertified += 1;
    }
    return { date, events, violations, uncertified };
  });

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 12, flexWrap: "wrap" }}>
        <PeriodNav period={period} onChange={onPeriodChange} granularities={["month"]} />
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          Cell: events · ✓ certified / ◷ not · ▲ violations · ◐ exceptions · PC km. Click a cell to open the driver&rsquo;s Hours of Service.
        </span>
      </div>

      <FanOutFailures failures={failures} what="Audit" />

      {future ? (
        <Panel>
          <EmptyLine>This month has not started — there is nothing to audit yet.</EmptyLine>
        </Panel>
      ) : rows === null ? (
        <LoadingLine>Loading the audit for {roster.length} drivers (one request each)…</LoadingLine>
      ) : rows.length === 0 ? (
        <Panel>
          <EmptyLine>No drivers to audit.</EmptyLine>
        </Panel>
      ) : (
        <div style={{ overflowX: "auto", border: `1px solid ${colors.borderSubtle}`, borderRadius: 10, background: colors.cardBg }}>
          <table style={{ borderCollapse: "collapse", minWidth: DRIVER_COL + dates.length * DAY_COL }} aria-label="Hours-of-service audit, driver by day">
            <thead>
              <tr>
                <th scope="col" style={{ ...headCell, width: DRIVER_COL, position: "sticky", left: 0, background: colors.cardBg, zIndex: 1 }}>
                  Driver
                </th>
                {dates.map((date) => (
                  <th key={date} scope="col" style={{ ...headCell, width: DAY_COL }}>
                    {formatDateOnly(date)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map(({ driver }) => {
                const days = byDriver.get(driver.id);
                return (
                  <tr key={driver.id} data-testid="audit-row">
                    <th scope="row" style={{ ...rowHead, position: "sticky", left: 0, background: colors.cardBg, zIndex: 1 }}>
                      <DriverLink driver={driver} onOpen={onOpenDriver} />
                    </th>
                    {dates.map((date) => {
                      const cell = date > today ? null : auditCell(days?.get(date));
                      if (!cell) {
                        return (
                          <td key={date} style={{ ...dayCell, color: colors.textFaint }} aria-label={`${driver.name}, ${formatDateOnly(date)}: no records`}>
                            —
                          </td>
                        );
                      }
                      const m = statusMeta(cell.kind);
                      return (
                        <td key={date} style={{ ...dayCell, padding: 0 }}>
                          <button
                            type="button"
                            data-testid="audit-cell"
                            onClick={() => onOpenDriver(driver.id)}
                            title={cell.description}
                            aria-label={`${driver.name}, ${formatDateOnly(date)}: ${cell.description}. Open Hours of Service.`}
                            style={{
                              appearance: "none",
                              width: "100%",
                              minHeight: 54,
                              padding: "5px 6px",
                              border: "none",
                              borderLeft: `3px solid ${m.c}`,
                              background: m.bg,
                              color: m.t,
                              cursor: "pointer",
                              textAlign: "left",
                              fontFamily: fonts.mono,
                              fontSize: 10,
                              lineHeight: 1.35,
                            }}
                          >
                            {cell.lines.map((line) => (
                              <div key={line}>{line}</div>
                            ))}
                          </button>
                        </td>
                      );
                    })}
                  </tr>
                );
              })}
              <tr data-testid="audit-totals">
                <th scope="row" style={{ ...rowHead, position: "sticky", left: 0, background: colors.cardBg, zIndex: 1, fontFamily: fonts.semiCondensed, fontSize: 10, letterSpacing: ".1em", textTransform: "uppercase", color: colors.textLabel }}>
                  Totals
                </th>
                {totals.map((t) => (
                  <td
                    key={t.date}
                    style={{ ...dayCell, fontFamily: fonts.mono, fontSize: 10, lineHeight: 1.35, color: colors.textMuted }}
                    aria-label={`${formatDateOnly(t.date)} totals: ${t.events} events, ${t.violations} violations, ${t.uncertified} uncertified`}
                  >
                    <div>{t.events} ev</div>
                    {t.violations > 0 && <div style={{ color: statusMeta("over").t }}>▲{t.violations}</div>}
                    {t.uncertified > 0 && <div style={{ color: statusMeta("soon").t }}>◷{t.uncertified}</div>}
                  </td>
                ))}
              </tr>
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

const headCell = {
  padding: "8px 6px",
  textAlign: "left" as const,
  fontFamily: fonts.semiCondensed,
  fontWeight: 600,
  fontSize: 9.5,
  letterSpacing: ".06em",
  textTransform: "uppercase" as const,
  color: colors.textFaint,
  borderBottom: `1px solid ${colors.border}`,
  whiteSpace: "nowrap" as const,
};

const rowHead = {
  padding: "6px 10px",
  textAlign: "left" as const,
  borderTop: `1px solid ${colors.borderSubtle}`,
  whiteSpace: "nowrap" as const,
};

const dayCell = {
  padding: "5px 6px",
  verticalAlign: "top" as const,
  borderTop: `1px solid ${colors.borderSubtle}`,
  borderLeft: `1px solid ${colors.borderSubtle}`,
  fontSize: 11,
};
