"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { listDutyDays, type DutyDaySummary, type DutyEventRecord, type HosStatus } from "@/lib/api/hos";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { DutyChip } from "./chips";
import { HosStatusGauge } from "./hos/HosStatusGauge";
import { DutyDayTimeline } from "./hos/DutyDayTimeline";
import DutyEventEntryModal from "./hos/DutyEventEntryModal";
import AmendEventModal from "./hos/AmendEventModal";

// The driver detail's Hours of Service tab, on the duty-event ledger: the status gauge
// (GET …/hos/status, loaded by the parent because the Profile tab shows it too) and the
// 14-day timeline (GET …/hos/days, loaded here). Writes go through the transcription and
// amendment modals; `onLedgerChanged` lets the parent reload the status, the roster batch
// and the calendar. Certification is never offered — it is the driver's own act.

export function HoursOfServiceTab({
  driverId,
  driverName,
  today,
  status,
  statusError,
  version,
  onLedgerChanged,
}: {
  driverId: string;
  driverName: string;
  today: string;
  status: HosStatus | null;
  statusError: string | null;
  version: number;
  onLedgerChanged: () => void;
}) {
  const [daysState, setDaysState] = useState<{ driverId: string; version: number; rows: DutyDaySummary[] } | null>(null);
  const [daysError, setDaysError] = useState<string | null>(null);
  const [modal, setModal] = useState<null | { kind: "entry" } | { kind: "amend"; day: DutyDaySummary; event: DutyEventRecord }>(null);

  useEffect(() => {
    let active = true;
    listDutyDays(driverId).then(
      (rows) => {
        if (active) {
          setDaysState({ driverId, version, rows });
          setDaysError(null);
        }
      },
      (e) => {
        if (active) setDaysError(e instanceof ApiError ? e.message : "Failed to load the duty ledger.");
      },
    );
    return () => {
      active = false;
    };
  }, [driverId, version]);

  const days = daysState?.driverId === driverId ? daysState.rows : null;
  const stale = daysState !== null && daysState.driverId === driverId && daysState.version !== version;

  return (
    <>
      <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
        <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted }}>Current duty status</span>
        {status ? <DutyChip status={status.currentDuty} /> : <StatusChip kind="off" label="Unknown" />}
      </div>

      {status ? (
        <HosStatusGauge status={status} />
      ) : statusError ? (
        <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
          <StatusChip kind="over" label={`Hours of service unavailable — ${statusError}`} />
        </Panel>
      ) : (
        <Panel style={{ marginBottom: 12 }}>
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading hours of service…</div>
        </Panel>
      )}

      <Panel>
        <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
          <SectionLabel>Duty ledger · last 14 days</SectionLabel>
          <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setModal({ kind: "entry" })}>
            + TRANSCRIBE DAY
          </ActionButton>
        </div>
        {daysError ? (
          <StatusChip kind="over" label={`Duty ledger unavailable — ${daysError}`} />
        ) : days === null ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading duty ledger…</div>
        ) : days.length === 0 ? (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No duty days on file.</div>
        ) : (
          <div style={{ opacity: stale ? 0.6 : 1 }}>
            <DutyDayTimeline days={days} today={today} onAmend={(day, event) => setModal({ kind: "amend", day, event })} />
          </div>
        )}
      </Panel>

      {modal?.kind === "entry" && (
        <DutyEventEntryModal driverId={driverId} driverName={driverName} defaultDate={today} onClose={() => setModal(null)} onSaved={onLedgerChanged} />
      )}
      {modal?.kind === "amend" && (
        <AmendEventModal
          driverId={driverId}
          driverName={driverName}
          day={modal.day}
          event={modal.event}
          onClose={() => setModal(null)}
          onSaved={onLedgerChanged}
        />
      )}
    </>
  );
}
