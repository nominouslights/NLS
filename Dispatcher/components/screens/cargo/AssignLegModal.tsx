"use client";

import { useEffect, useState } from "react";
import { colors, fonts, svcMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api";
import type { ShipmentLegInput, ShipmentRecord } from "@/lib/api/shipments";
import {
  corridorLabel,
  listTrips,
  shortDateLabel,
  sortTrips,
  svcForTrip,
  todayIso,
  tripChip,
  tripWindowLabel,
  type TripRecord,
} from "@/lib/api/trips";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { ServiceChip, StatusChip } from "@/components/ui/Chip";
import { SectionLabel } from "@/components/ui/Panel";
import { TextField } from "@/components/ui/Field";

// Assign a shipment onto a Cargo/Grocery trip — POST /{id}/legs. The trip
// picker merges two server-side filtered lists (serviceType has no "in"
// filter), today onward, cancelled excluded. The leg's from/to default to the
// shipment's own origin/destination; the catalog stop ids ride along only
// while the names are untouched (an edited name is a free-text place).

export default function AssignLegModal({
  shipment,
  onClose,
  onAssigned,
}: {
  shipment: ShipmentRecord;
  onClose: () => void;
  onAssigned: (input: ShipmentLegInput) => Promise<void>;
}) {
  const [trips, setTrips] = useState<TripRecord[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [tripId, setTripId] = useState<string | null>(null);
  const [fromName, setFromName] = useState(shipment.originName);
  const [toName, setToName] = useState(shipment.destinationName);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    const from = todayIso();
    Promise.all([
      listTrips({ serviceType: "Cargo", from, excludeCancelled: true }),
      listTrips({ serviceType: "Grocery", from, excludeCancelled: true }),
    ]).then(
      ([cargo, grocery]) => {
        if (active) {
          // Only runs that haven't happened yet can carry new freight.
          const open = [...cargo.items, ...grocery.items].filter(
            (t) => t.status === "Scheduled" || t.status === "InProgress",
          );
          setTrips(sortTrips(open));
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setTrips(null);
          setLoadError(e instanceof ApiError ? e.message : "Failed to load cargo trips.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, []);

  const alreadyOn = new Set(shipment.legs.map((l) => l.tripId));

  async function submit() {
    if (busy) return;
    if (!tripId) return setError("Pick the trip that will carry this shipment.");
    if (!fromName.trim() || !toName.trim()) return setError("Enter the leg's pickup and drop-off places.");
    setBusy(true);
    setError(null);
    try {
      await onAssigned({
        tripId,
        // Stop ids only survive while the names are the shipment's own — an
        // edited name is free text with no catalog identity.
        fromStopId: fromName.trim() === shipment.originName ? shipment.originStopId : null,
        fromName: fromName.trim(),
        toStopId: toName.trim() === shipment.destinationName ? shipment.destinationStopId : null,
        toName: toName.trim(),
      });
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "Failed to assign the shipment — please try again.");
      setBusy(false);
    }
  }

  return (
    <ModalShell
      eyebrow={`Shipment · ${shipment.shipmentNumber}`}
      title="Assign to Trip"
      onClose={onClose}
      error={error ?? loadError}
      maxWidth={680}
      footer={
        <>
          <ActionButton onClick={onClose}>CANCEL</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={busy || !tripId}>
            {busy ? "ASSIGNING…" : "ASSIGN LEG"}
          </ActionButton>
        </>
      }
    >
      <SectionLabel>Upcoming Cargo &amp; Grocery trips</SectionLabel>
      {trips === null && !loadError && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading cargo trips…</div>
      )}
      {trips !== null && trips.length === 0 && (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, lineHeight: 1.6 }}>
          No upcoming Cargo or Grocery trips. Create one from the Create Trip wizard, or let a cargo schedule template
          generate them (Routes &amp; Schedules).
        </div>
      )}
      <div style={{ display: "flex", flexDirection: "column", gap: 6, maxHeight: 260, overflowY: "auto" }}>
        {(trips ?? []).map((t) => {
          const active = tripId === t.id;
          const onAlready = alreadyOn.has(t.id);
          const chip = tripChip(t);
          return (
            <div
              key={t.id}
              onClick={onAlready ? undefined : () => setTripId(t.id)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 9,
                flexWrap: "wrap",
                padding: "9px 12px",
                borderRadius: 9,
                border: `1px solid ${active ? colors.borderActive : colors.borderSubtle}`,
                background: active ? colors.cardBgActive : colors.cardBg,
                boxShadow: active ? `inset 3px 0 0 ${svcMeta(svcForTrip(t.serviceType)).accent}, ${colors.shadowCard}` : colors.shadowCard,
                cursor: onAlready ? "not-allowed" : "pointer",
                opacity: onAlready ? 0.55 : 1,
              }}
            >
              <span style={{ fontFamily: fonts.mono, fontSize: 11.5, color: colors.skyBlue }}>{t.tripNumber}</span>
              <ServiceChip svc={svcForTrip(t.serviceType)} />
              <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary }}>
                {shortDateLabel(t.serviceDate)} · {tripWindowLabel(t)} · {corridorLabel(t)}
              </span>
              {onAlready ? (
                <StatusChip kind="off" label="Already carries this shipment" />
              ) : (
                <StatusChip kind={chip.kind} label={chip.label} />
              )}
            </div>
          );
        })}
      </div>

      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 14, marginTop: 16 }}>
        <TextField
          label="Leg pickup"
          value={fromName}
          onChange={setFromName}
          hint={<span style={{ color: colors.textFaint }}>· defaults to the shipment origin</span>}
        />
        <TextField
          label="Leg drop-off"
          value={toName}
          onChange={setToName}
          hint={<span style={{ color: colors.textFaint }}>· a mid-corridor hub makes this a transfer leg</span>}
        />
      </div>
    </ModalShell>
  );
}
