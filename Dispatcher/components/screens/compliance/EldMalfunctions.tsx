"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { listEldMalfunctions, type EldMalfunctionRecord } from "@/lib/api/hos";
import type { DriverRecord } from "@/lib/api/drivers";
import { daysOnPaper } from "@/lib/hos/compliance";
import { formatDateTimeUtc } from "@/lib/hos/display";
import { StatusChip } from "@/components/ui/Chip";
import { Panel } from "@/components/ui/Panel";
import { PAPER_LOG_CHIP, ResolveEldMalfunctionModal } from "@/components/screens/drivers/hos/EldMalfunctionPanel";
import { bodyRow, DriverLink, FanOutFailures, headRow, LoadingLine, mono, RowAction, useRosterFanOut } from "./shared";

// Open ELD malfunctions across drivers (GET …/hos/eld-malfunctions per driver — the fan-out).
// Each one is paper-log mode (✎ caution, the HOS tab's own chip) with the days it has been on
// paper — today minus the local date it was noticed. RESOLVE closes it with a note.

const COLS = "minmax(130px,1fr) 90px 200px 150px minmax(200px,2fr) 80px";

const fetchEld = (d: DriverRecord) => listEldMalfunctions(d.id);

export function EldMalfunctions({
  roster,
  today,
  version,
  onChanged,
  onOpenDriver,
}: {
  roster: DriverRecord[];
  today: string;
  version: number;
  onChanged: () => void;
  onOpenDriver: (driverId: string) => void;
}) {
  const [resolve, setResolve] = useState<{ driverName: string; report: EldMalfunctionRecord } | null>(null);
  const { rows, failures } = useRosterFanOut(roster, fetchEld, version);

  const open = (rows ?? [])
    .flatMap(({ driver, value }) => value.filter((m) => m.status === "Open").map((m) => ({ driver, m })))
    .sort((a, b) => a.m.noticedAtUtc.localeCompare(b.m.noticedAtUtc));

  return (
    <div>
      <FanOutFailures failures={failures} what="ELD malfunction reports" />
      {rows === null ? (
        <LoadingLine>Loading ELD malfunction reports for {roster.length} drivers (one request each)…</LoadingLine>
      ) : open.length === 0 ? (
        <Panel>
          <StatusChip kind="ontime" label="No open ELD malfunctions — every driver is on the ELD" />
        </Panel>
      ) : (
        <Panel style={{ padding: "12px 0 0" }}>
          <div style={{ ...headRow, gridTemplateColumns: COLS }}>
            <div>Driver</div>
            <div>Code</div>
            <div>On paper</div>
            <div>Noticed</div>
            <div>Description</div>
            <div />
          </div>
          {open.map(({ driver, m }) => {
            const days = daysOnPaper(m.noticedAtUtc, today);
            return (
              <div key={m.id} data-testid="eld-open-row" style={{ ...bodyRow, gridTemplateColumns: COLS }}>
                <DriverLink driver={driver} onOpen={onOpenDriver} />
                <span style={{ ...mono, fontWeight: 600, color: colors.textPrimary }}>{m.code}</span>
                <span>
                  <StatusChip kind={PAPER_LOG_CHIP.kind} glyph={PAPER_LOG_CHIP.glyph} label={`${days} day${days === 1 ? "" : "s"} on paper`} />
                </span>
                <span style={mono}>{formatDateTimeUtc(m.noticedAtUtc)}</span>
                <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.5 }}>{m.description}</span>
                <span style={{ textAlign: "right" }}>
                  <RowAction label={`Resolve ELD malfunction ${m.code} for ${driver.name}`} onClick={() => setResolve({ driverName: driver.name, report: m })}>
                    Resolve
                  </RowAction>
                </span>
              </div>
            );
          })}
        </Panel>
      )}

      {resolve && (
        <ResolveEldMalfunctionModal
          driverId={resolve.report.driverId}
          driverName={resolve.driverName}
          report={resolve.report}
          onClose={() => setResolve(null)}
          onSaved={onChanged}
        />
      )}
    </div>
  );
}
