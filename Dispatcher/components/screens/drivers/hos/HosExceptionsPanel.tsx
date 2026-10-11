"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  acknowledgeHosException,
  listHosExceptions,
  rejectHosException,
  withdrawHosException,
  type HosExceptionRecord,
} from "@/lib/api/hos";
import { exceptionKindLabel, exceptionStatusChip, formatDateTimeUtc } from "@/lib/hos/display";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import HosExceptionModal, { ExceptionNoteModal } from "./HosExceptionModal";

// The driver's hours-of-service exceptions (GET …/hos/exceptions — the 14-day window):
// adverse driving conditions and emergencies the driver or dispatch declared, and off-duty
// deferrals a compliance officer approved. Each row is kind + status chip (colour + glyph +
// label), the effective window, the reason, the extension or deferral, the trip. Actions:
// ACKNOWLEDGE is dispatch's act on a Declared one; REJECT and WITHDRAW, and approving a
// deferral at all, are compliance-only — `canCompliance` is the client gate (lib/roles.ts),
// the ComplianceAccess policy is the real one.

const linkStyle = (color: string): React.CSSProperties => ({
  fontFamily: "inherit",
  fontWeight: 600,
  fontSize: 10,
  letterSpacing: ".08em",
  textTransform: "uppercase",
  color,
  cursor: "pointer",
});

export function HosExceptionsPanel({
  driverId,
  driverName,
  today,
  version,
  canCompliance,
  onChanged,
}: {
  driverId: string;
  driverName: string;
  today: string;
  version: number;
  canCompliance: boolean;
  onChanged: () => void;
}) {
  const [state, setState] = useState<{ driverId: string; rows: HosExceptionRecord[] } | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [modal, setModal] = useState<null | { kind: "declare" } | { kind: "deferral" } | { kind: "reject"; row: HosExceptionRecord } | { kind: "withdraw"; row: HosExceptionRecord }>(null);

  useEffect(() => {
    let active = true;
    listHosExceptions(driverId).then(
      (rows) => {
        if (active) {
          setState({ driverId, rows });
          setError(null);
        }
      },
      (e) => {
        if (active) setError(e instanceof ApiError ? e.message : "Failed to load exceptions.");
      },
    );
    return () => {
      active = false;
    };
  }, [driverId, version]);

  const rows = state?.driverId === driverId ? state.rows : null;

  async function acknowledge(row: HosExceptionRecord) {
    if (busy) return;
    setBusy(true);
    setActionError(null);
    try {
      await acknowledgeHosException(driverId, row.id);
      onChanged();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "Failed to acknowledge the exception.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <Panel style={{ marginBottom: 12 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 12, flexWrap: "wrap" }}>
        <SectionLabel>Exceptions · last 14 days</SectionLabel>
        <span style={{ marginLeft: "auto", display: "inline-flex", gap: 8 }}>
          {canCompliance && <ActionButton onClick={() => setModal({ kind: "deferral" })}>APPROVE DEFERRAL</ActionButton>}
          <ActionButton variant="primary" onClick={() => setModal({ kind: "declare" })}>
            + RECORD EXCEPTION
          </ActionButton>
        </span>
      </div>

      {actionError && (
        <div style={{ marginBottom: 10 }}>
          <StatusChip kind="over" label={actionError} />
        </div>
      )}

      {error ? (
        <StatusChip kind="over" label={`Exceptions unavailable — ${error}`} />
      ) : rows === null ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading exceptions…</div>
      ) : rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>No exceptions declared in the last 14 days.</div>
      ) : (
        <div>
          {[...rows]
            .sort((a, b) => b.effectiveFromUtc.localeCompare(a.effectiveFromUtc))
            .map((row) => {
              const chip = exceptionStatusChip(row.status);
              const closed = row.status === "Rejected" || row.status === "Withdrawn";
              return (
                <div key={row.id} data-testid="hos-exception-row" style={{ borderTop: `1px solid ${colors.borderSubtle}`, padding: "10px 0" }}>
                  <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                    <span style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: colors.textPrimary }}>{exceptionKindLabel(row.kind)}</span>
                    <StatusChip kind={chip.kind} glyph={chip.glyph} label={chip.label} />
                    <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.textDim }}>
                      {formatDateTimeUtc(row.effectiveFromUtc)} – {row.effectiveToUtc ? formatDateTimeUtc(row.effectiveToUtc) : "open"}
                    </span>
                    <span style={{ marginLeft: "auto", display: "inline-flex", gap: 14, fontFamily: fonts.semiCondensed }}>
                      {row.status === "Declared" && (
                        <span onClick={busy ? undefined : () => acknowledge(row)} style={linkStyle(busy ? colors.textFaint : colors.blue)}>
                          Acknowledge
                        </span>
                      )}
                      {canCompliance && !closed && (
                        <>
                          <span onClick={() => setModal({ kind: "reject", row })} style={linkStyle(colors.textMuted)}>
                            Reject
                          </span>
                          <span onClick={() => setModal({ kind: "withdraw", row })} style={linkStyle(colors.textMuted)}>
                            Withdraw
                          </span>
                        </>
                      )}
                    </span>
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textMuted, marginTop: 5, lineHeight: 1.5 }}>
                    {row.reason}
                    {row.location && ` · ${row.location}`}
                  </div>
                  <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginTop: 6 }}>
                    {row.extraDrivingMinutes !== null && <StatusChip kind="info" glyph="+" label={`+${row.extraDrivingMinutes} min driving`} />}
                    {row.deferredOffDutyMinutes !== null && <StatusChip kind="info" glyph="◷" label={`${row.deferredOffDutyMinutes} min off-duty deferred`} />}
                    {row.tripId && <StatusChip kind="info" glyph="◗" label={`Trip ${row.tripId.slice(0, 8)}`} />}
                    {row.notKnownBeforeDeparture && <StatusChip kind="ontime" label="Not knowable before departure" />}
                    {row.eldRemarkRecorded ? <StatusChip kind="ontime" glyph="≡" label="ELD remark recorded" /> : <StatusChip kind="soon" glyph="≡" label="No ELD remark" />}
                    {row.closureNote && <StatusChip kind="off" label={`Note — ${row.closureNote}`} />}
                  </div>
                </div>
              );
            })}
        </div>
      )}

      {(modal?.kind === "declare" || modal?.kind === "deferral") && (
        <HosExceptionModal driverId={driverId} driverName={driverName} today={today} mode={modal.kind} onClose={() => setModal(null)} onSaved={onChanged} />
      )}
      {modal?.kind === "reject" && (
        <ExceptionNoteModal
          driverName={driverName}
          title="Reject Exception"
          action="REJECT"
          prompt="Why the exception is rejected"
          onClose={() => setModal(null)}
          onConfirm={async (note) => {
            await rejectHosException(driverId, modal.row.id, note);
            onChanged();
          }}
        />
      )}
      {modal?.kind === "withdraw" && (
        <ExceptionNoteModal
          driverName={driverName}
          title="Withdraw Exception"
          action="WITHDRAW"
          prompt="Why the exception is withdrawn"
          onClose={() => setModal(null)}
          onConfirm={async (note) => {
            await withdrawHosException(driverId, modal.row.id, note);
            onChanged();
          }}
        />
      )}
    </Panel>
  );
}
