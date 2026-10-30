"use client";

import { useCallback, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { HosWire, addDays } from "@/lib/hos/engine";
import { listHosExceptions, rejectHosException, type HosExceptionRecord } from "@/lib/api/hos";
import type { DriverRecord } from "@/lib/api/drivers";
import { exceptionKindLabel, exceptionStatusChip, formatDateTimeUtc } from "@/lib/hos/display";
import { ActionButton } from "@/components/ui/Button";
import { Panel } from "@/components/ui/Panel";
import { SelectField } from "@/components/ui/Field";
import { StatusChip } from "@/components/ui/Chip";
import HosExceptionModal, { ExceptionNoteModal } from "@/components/screens/drivers/hos/HosExceptionModal";
import { bodyRow, DriverLink, FanOutFailures, headRow, LoadingLine, mono, RowAction, SpecChip, useRosterFanOut } from "./shared";

// What compliance stands behind or must look at: off-duty deferrals in force (approved by a
// compliance officer, so revocable here) and adverse-conditions / emergency declarations
// still awaiting dispatch's acknowledgement. REJECT ends one with a note; APPROVE DEFERRAL
// opens the same deferral form the driver's HOS tab uses.

/** The exceptions window: the regulation's 14 days back, plus the 7 days ahead a deferral may be approved for. */
export function queueRange(today: string): { from: string; to: string } {
  return { from: addDays(today, -13), to: addDays(today, 7) };
}

/** In the queue: a deferral the engine still honours, or a declaration nobody has acknowledged. */
export function inQueue(e: HosExceptionRecord): boolean {
  if (e.kind === HosWire.OffDutyDeferralKind) return e.status === "Approved" || e.status === "Declared" || e.status === "Acknowledged";
  return e.status === "Declared";
}

const COLS = "minmax(130px,1fr) 170px 130px 150px minmax(200px,2fr) 70px";

export function DeferralQueue({
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
  const [reject, setReject] = useState<{ driverName: string; row: HosExceptionRecord } | null>(null);
  const [approveFor, setApproveFor] = useState(roster[0]?.id ?? "");
  const [approveOpen, setApproveOpen] = useState(false);
  const range = queueRange(today);
  const fetchOne = useCallback((d: DriverRecord) => listHosExceptions(d.id, { from: range.from, to: range.to }), [range.from, range.to]);
  const { rows, failures } = useRosterFanOut(roster, fetchOne, version);

  const queue = (rows ?? [])
    .flatMap(({ driver, value }) => value.filter(inQueue).map((e) => ({ driver, e })))
    .sort((a, b) => a.e.effectiveFromUtc.localeCompare(b.e.effectiveFromUtc));
  const approveDriver = roster.find((d) => d.id === approveFor) ?? null;

  return (
    <div>
      <div style={{ display: "flex", alignItems: "flex-end", gap: 10, marginBottom: 12, flexWrap: "wrap" }}>
        <div style={{ minWidth: 220 }}>
          <SelectField label="Approve a deferral for" value={approveFor} onChange={setApproveFor} options={roster.map((d) => ({ value: d.id, label: d.name }))} />
        </div>
        <ActionButton variant="primary" onClick={() => setApproveOpen(true)} disabled={!approveDriver}>
          + APPROVE DEFERRAL
        </ActionButton>
      </div>

      <FanOutFailures failures={failures} what="Exceptions" />

      {rows === null ? (
        <LoadingLine>Loading exceptions for {roster.length} drivers (one request each)…</LoadingLine>
      ) : queue.length === 0 ? (
        <Panel>
          <StatusChip kind="ontime" label="Nothing waiting — no deferrals in force and no unacknowledged declarations" />
        </Panel>
      ) : (
        <Panel style={{ padding: "12px 0 0" }}>
          <div style={{ ...headRow, gridTemplateColumns: COLS }}>
            <div>Driver</div>
            <div>Kind</div>
            <div>Status</div>
            <div>From</div>
            <div>Reason</div>
            <div />
          </div>
          {queue.map(({ driver, e }) => (
            <div key={e.id} data-testid="deferral-row" style={{ ...bodyRow, gridTemplateColumns: COLS }}>
              <DriverLink driver={driver} onOpen={onOpenDriver} />
              <span style={{ fontWeight: 600 }}>
                {exceptionKindLabel(e.kind)}
                {e.deferredOffDutyMinutes !== null && <span style={{ ...mono, display: "block" }}>{e.deferredOffDutyMinutes} min deferred</span>}
                {e.extraDrivingMinutes !== null && <span style={{ ...mono, display: "block" }}>+{e.extraDrivingMinutes} min driving</span>}
              </span>
              <span>
                <SpecChip spec={e.status === "Declared" ? { ...exceptionStatusChip(e.status), label: "Awaiting acknowledgement" } : exceptionStatusChip(e.status)} />
              </span>
              <span style={mono}>{formatDateTimeUtc(e.effectiveFromUtc)}</span>
              <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, lineHeight: 1.5 }}>
                {e.reason}
                {e.location && ` · ${e.location}`}
              </span>
              <span style={{ textAlign: "right" }}>
                <RowAction label={`Reject ${exceptionKindLabel(e.kind)} for ${driver.name}`} onClick={() => setReject({ driverName: driver.name, row: e })}>
                  Reject
                </RowAction>
              </span>
            </div>
          ))}
        </Panel>
      )}

      {reject && (
        <ExceptionNoteModal
          driverName={reject.driverName}
          title="Reject Exception"
          action="REJECT"
          prompt="Why the exception is rejected"
          onClose={() => setReject(null)}
          onConfirm={async (note) => {
            await rejectHosException(reject.row.driverId, reject.row.id, note);
            onChanged();
          }}
        />
      )}
      {approveOpen && approveDriver && (
        <HosExceptionModal
          driverId={approveDriver.id}
          driverName={approveDriver.name}
          today={today}
          mode="deferral"
          onClose={() => setApproveOpen(false)}
          onSaved={onChanged}
        />
      )}
    </div>
  );
}
