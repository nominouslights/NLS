"use client";

// CONVERT TO DEADHEAD — confirm for POST /api/trips/{id}/convert-to-deadhead.
// The parent pre-gates the button with deadheadConversionBlockReason; the server
// is still the final authority (Bookeo bookings, booking-day trips, unlinked
// manifests), so its 409 message is shown inline here. Only the sentences that
// apply to this trip are shown.

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { corridorLabel, type TripRecord } from "@/lib/api/trips";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";

export default function ConvertToDeadheadModal({
  trip,
  partner,
  onClose,
  onConfirmed,
}: {
  trip: TripRecord;
  /** The paired leg when it is on the loaded page — null when it isn't (or unpaired). */
  partner: TripRecord | null;
  onClose: () => void;
  /** Runs the POST and the reload; a throw is shown inline. */
  onConfirmed: () => Promise<void>;
}) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function submit() {
    if (busy) return;
    setBusy(true);
    setError(null);
    try {
      await onConfirmed();
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to convert the trip — please try again.");
      setBusy(false);
    }
  }

  const paired = trip.roundTripKey !== null;
  const partnerName = partner ? partner.tripNumber : "the leg paired with it";

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · ${corridorLabel(trip)}`}
      title="Convert to Deadhead"
      onClose={onClose}
      error={error}
      maxWidth={500}
      footer={
        <>
          <ActionButton onClick={onClose}>KEEP AS PASSENGER TRIP</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy}>
            {busy ? "CONVERTING…" : "CONVERT TO DEADHEAD"}
          </ActionButton>
        </>
      }
    >
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          gap: 10,
          fontFamily: fonts.body,
          fontSize: 13,
          color: colors.textSecondary,
          lineHeight: 1.6,
        }}
      >
        <p style={{ margin: 0 }}>
          {trip.tripNumber} becomes an empty repositioning run. While it is a deadhead no passenger manifest can be
          added, and it can start and finish without a manifest or post-trip inspection. Shipments can still ride it.
        </p>
        {trip.manifestId !== null && <p style={{ margin: 0 }}>Its empty manifest record will be removed.</p>}
        {paired ? (
          <p style={{ margin: 0 }}>
            It stays paired with {partnerName}; the round trip still bills one full rate, flagged for an optional
            discount.
          </p>
        ) : (
          trip.clientName && (
            <p style={{ margin: 0 }}>
              It keeps {trip.clientName}
              {trip.poNumber ? ` / PO ${trip.poNumber}` : ""}. An unpaired deadhead is left out of draft invoices —
              pair it or close it without billing after the run.
            </p>
          )
        )}
        <p style={{ margin: 0 }}>You can convert it back while it is Scheduled.</p>
      </div>
    </ModalShell>
  );
}
