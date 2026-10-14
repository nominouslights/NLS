"use client";

// CORRECT DRIVER — fixes who actually drove a FINISHED trip (ReadyForBilling,
// Invoiced, Completed). Live trips use REASSIGN; this is the after-the-fact
// record fix, so it lists EVERY driver on record (Inactive / Deactivated
// included, labelled) and requires a reason that lands in the activity log.
// POST /api/trips/{id}/correct-driver — the modal owns the call; server errors
// render verbatim. Client clearance is checked against the trip's serviceDate
// (who was cleared THEN), and only warns — it never blocks.

import { useState } from "react";
import { colors, fonts, rowSurface, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  correctTripDriver,
  DRIVER_CORRECTION_REASON_MAX,
  shortDateLabel,
  type TripRecord,
} from "@/lib/api/trips";
import { driverStatusKindFor } from "@/lib/api/drivers";
import { useDriverRoster } from "@/lib/useDriverRoster";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { TextAreaField } from "@/components/ui/Field";
import { SectionLabel } from "@/components/ui/Panel";
import { StatusBadge, StatusChip } from "@/components/ui/Chip";

export default function CorrectDriverModal({
  trip,
  onClose,
  onCorrected,
}: {
  trip: TripRecord;
  onClose: () => void;
  /** Runs after the POST succeeds (refresh); the modal closes after it. */
  onCorrected: (driverId: string) => Promise<void>;
}) {
  // Every driver on record — a correction may name someone since made Inactive
  // or Deactivated — minus the current driver (that is not a correction). The
  // correction is historical, so clearance is judged on the trip's own date,
  // not today.
  const { roster, error: rosterError } = useDriverRoster({
    clientName: trip.clientName,
    asOf: trip.serviceDate,
    include: "all",
    excludeDriverId: trip.driverId,
  });
  const [driverId, setDriverId] = useState<string | null>(null);
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const tripDay = shortDateLabel(trip.serviceDate);

  const trimmed = reason.trim();
  const tooLong = trimmed.length > DRIVER_CORRECTION_REASON_MAX;
  const canSubmit = driverId !== null && trimmed.length > 0 && !tooLong && !busy;
  const selected = roster?.find((r) => r.driver.id === driverId) ?? null;

  async function submit() {
    if (!canSubmit || driverId === null) return;
    setBusy(true);
    setError(null);
    try {
      await correctTripDriver(trip.id, driverId, trimmed);
      await onCorrected(driverId);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "The driver could not be corrected — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · Correction`}
      title="Correct Driver"
      onClose={onClose}
      error={error ?? rosterError}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={!canSubmit}>
            {busy ? "SAVING…" : "CORRECT DRIVER"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.55 }}>
          Recorded driver:{" "}
          <strong data-testid="current-driver" style={{ color: colors.textPrimary }}>
            {trip.driverName ?? "No driver recorded"}
          </strong>
          . Use this when the record is wrong about who drove a finished trip — the change and your reason are kept in
          the activity log.
        </div>

        <div
          data-testid="hos-note"
          style={{
            display: "flex",
            alignItems: "flex-start",
            gap: 9,
            fontFamily: fonts.body,
            fontSize: 12.5,
            color: colors.textSecondary,
            lineHeight: 1.5,
          }}
        >
          <StatusBadge kind="info" size={15} />
          <span>
            Hours-of-service logs and inspection records are not changed — amend hours on the driver&apos;s Hours tab.
          </span>
        </div>

        <div>
          <SectionLabel>Driver · everyone on record</SectionLabel>
          {roster === null && !rosterError && (
            <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading drivers…</div>
          )}
          {roster !== null && (
            <div role="radiogroup" aria-label="Correct driver" style={{ display: "flex", flexDirection: "column", gap: 6 }}>
              {roster.map(({ driver, cleared }) => (
                <button
                  key={driver.id}
                  type="button"
                  role="radio"
                  aria-checked={driverId === driver.id}
                  data-testid="driver-option"
                  onClick={() => setDriverId(driver.id)}
                  disabled={busy}
                  style={{
                    display: "flex",
                    alignItems: "center",
                    flexWrap: "wrap",
                    gap: 10,
                    padding: "10px 13px",
                    textAlign: "left",
                    font: "inherit",
                    color: "inherit",
                    width: "100%",
                    ...rowSurface(driverId === driver.id, colors.blue),
                  }}
                >
                  <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 500, color: colors.textPrimary }}>
                    {driver.name}
                  </span>
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{driver.licenceClass}</span>
                  <span style={{ marginLeft: "auto", display: "flex", gap: 6, flexWrap: "wrap" }}>
                    {driver.status !== "Active" && (
                      // colour + glyph + text — never colour alone
                      <StatusChip kind={driverStatusKindFor(driver.status)} label={driver.status} />
                    )}
                    {trip.clientName &&
                      (cleared ? (
                        <StatusChip kind="ontime" label={`${trip.clientName} clearance on ${tripDay}`} />
                      ) : (
                        <StatusChip kind="soon" label={`No ${trip.clientName} clearance on ${tripDay}`} />
                      ))}
                  </span>
                </button>
              ))}
              {roster.length === 0 && (
                <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>
                  No other drivers on record.
                </div>
              )}
            </div>
          )}
          {selected && trip.clientName && !selected.cleared && (
            <div
              data-testid="clearance-warning"
              style={{
                marginTop: 8,
                padding: "11px 14px",
                background: statusMeta("soon").bg,
                border: `1px solid ${statusMeta("soon").bd}`,
                borderRadius: 10,
                display: "flex",
                alignItems: "flex-start",
                gap: 9,
                fontFamily: fonts.body,
                fontSize: 12.5,
                fontWeight: 600,
                color: statusMeta("soon").t,
                lineHeight: 1.5,
              }}
            >
              <StatusBadge kind="soon" />
              <span>
                {selected.driver.name} had no {trip.clientName} clearance on {tripDay}. You can still record them —
                this is a warning, not a block.
              </span>
            </div>
          )}
        </div>

        <div>
          <TextAreaField
            label="Reason (required — recorded in the activity log)"
            value={reason}
            onChange={setReason}
            rows={3}
            disabled={busy}
            placeholder="Driver swap at Thompson was never entered"
            hint={
              <span data-testid="reason-counter" style={{ color: tooLong ? statusMeta("over").t : colors.textFaint }}>
                · {trimmed.length} / {DRIVER_CORRECTION_REASON_MAX}
              </span>
            }
          />
          {tooLong && (
            <div style={{ marginTop: 6 }}>
              <StatusChip
                kind="over"
                label={`Reason is ${trimmed.length - DRIVER_CORRECTION_REASON_MAX} characters too long`}
              />
            </div>
          )}
        </div>
      </div>
    </ModalShell>
  );
}
