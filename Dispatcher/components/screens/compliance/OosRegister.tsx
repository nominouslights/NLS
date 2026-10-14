"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { listOutOfService, type OutOfServiceDeclaration } from "@/lib/api/hos";
import type { DriverRecord } from "@/lib/api/drivers";
import { formatDateTimeUtc } from "@/lib/hos/display";
import { StatusChip } from "@/components/ui/Chip";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { LiftOutOfServiceModal } from "@/components/screens/drivers/hos/RecordOutOfServiceModal";
import { bodyRow, DriverLink, EmptyLine, FanOutFailures, headRow, LoadingLine, mono, RowAction, useRosterFanOut } from "./shared";

// The out-of-service register across drivers (GET …/out-of-service per driver — the fan-out):
// declarations in force first, with LIFT; then the history. In force is ✕ vermillion, the
// glyph that tells it apart from a violation's ▲; lifted/expired is the neutral — with its label.

const COLS = "minmax(130px,1fr) 150px 150px minmax(220px,2fr) 70px";

const fetchOos = (d: DriverRecord) => listOutOfService(d.id);

export function OosRegister({
  roster,
  version,
  onChanged,
  onOpenDriver,
}: {
  roster: DriverRecord[];
  version: number;
  onChanged: () => void;
  onOpenDriver: (driverId: string) => void;
}) {
  const [lift, setLift] = useState<{ driverId: string; driverName: string; recordId: string } | null>(null);
  const { rows, failures } = useRosterFanOut(roster, fetchOos, version);

  const all = (rows ?? []).flatMap(({ driver, value }) => value.map((r) => ({ driver, r })));
  const byNewest = (a: { r: OutOfServiceDeclaration }, b: { r: OutOfServiceDeclaration }) => b.r.declaredAtUtc.localeCompare(a.r.declaredAtUtc);
  const active = all.filter(({ r }) => r.active).sort(byNewest);
  const history = all.filter(({ r }) => !r.active).sort(byNewest);

  function table(list: typeof all, inForce: boolean) {
    return (
      <>
        <div style={{ ...headRow, gridTemplateColumns: COLS }}>
          <div>Driver</div>
          <div>Status</div>
          <div>Declared</div>
          <div>Reason</div>
          <div />
        </div>
        {list.map(({ driver, r }) => (
          <div key={r.id} data-testid={inForce ? "oos-active-row" : "oos-history-row"} style={{ ...bodyRow, gridTemplateColumns: COLS }}>
            <DriverLink driver={driver} onOpen={onOpenDriver} />
            <span>
              {inForce ? (
                <StatusChip kind="over" glyph="✕" label={r.effectiveUntilUtc ? `In force until ${formatDateTimeUtc(r.effectiveUntilUtc)}` : "In force until lifted"} />
              ) : (
                <StatusChip kind="off" label={r.status === "Lifted" ? `Lifted ${r.liftedAtUtc ? formatDateTimeUtc(r.liftedAtUtc) : ""}`.trim() : "Expired"} />
              )}
            </span>
            <span style={mono}>{formatDateTimeUtc(r.declaredAtUtc)}</span>
            <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.5 }}>
              {r.reason}
              {r.evidence && ` · Evidence: ${r.evidence}`}
              {r.liftReason && ` · Lift: ${r.liftReason}`}
            </span>
            <span style={{ textAlign: "right" }}>
              {inForce && (
                <RowAction label={`Lift out-of-service for ${driver.name}`} onClick={() => setLift({ driverId: driver.id, driverName: driver.name, recordId: r.id })}>
                  Lift
                </RowAction>
              )}
            </span>
          </div>
        ))}
      </>
    );
  }

  return (
    <div>
      <FanOutFailures failures={failures} what="Out-of-service records" />
      {rows === null ? (
        <LoadingLine>Loading out-of-service records for {roster.length} drivers (one request each)…</LoadingLine>
      ) : (
        <>
          <Panel style={{ marginBottom: 12, padding: "12px 0 0" }}>
            <div style={{ padding: "0 12px" }}>
              <SectionLabel>In force</SectionLabel>
            </div>
            {active.length === 0 ? (
              <div style={{ padding: "0 12px 12px" }}>
                <StatusChip kind="ontime" label="No driver is out of service" />
              </div>
            ) : (
              table(active, true)
            )}
          </Panel>
          <Panel style={{ padding: "12px 0 0" }}>
            <div style={{ padding: "0 12px" }}>
              <SectionLabel>History</SectionLabel>
            </div>
            {history.length === 0 ? (
              <div style={{ padding: "0 12px 12px" }}>
                <EmptyLine>No lifted or expired declarations on file.</EmptyLine>
              </div>
            ) : (
              table(history, false)
            )}
          </Panel>
        </>
      )}

      {lift && (
        <LiftOutOfServiceModal
          driverId={lift.driverId}
          driverName={lift.driverName}
          recordId={lift.recordId}
          onClose={() => setLift(null)}
          onSaved={onChanged}
        />
      )}
    </div>
  );
}
