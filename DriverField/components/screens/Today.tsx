"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchTile } from "@/components/ui-tablet/TouchTile";
import { DutyControl } from "@/components/ui-tablet/DutyControl";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { Screen, MockTag, Heading, TabletChip, FieldLine, CardRow } from "./shared";
import { BoardingGateBanner } from "./inspection/BoardingGateBanner";
import type { InspectionMode } from "@/lib/types";
import { boardingGate } from "@/lib/inspectionGate";
import { useInspectionStoreHydrated } from "@/lib/useInspectionStore";
import { band, durationLabel } from "@/lib/hos/display";
import { useHosLedger } from "@/lib/hos/useHosLedger";
import {
  assignedTrips,
  assignedVehicle,
  currentDriver,
  formatClock,
  assignedVehicleOpenDefects as vehicleOpen,
  openDefectWorkOrderText,
  worstDefectKind,
} from "@/lib/data";

// The screen a driver opens on. Answers four questions without a tap: how many hours do I have
// left, what is next, what am I driving, and is anything wrong with it.
//
// The hours tile is the engine's figure (lib/hos/ledger.ts) on the same band set as the Hours
// screen — never a second arithmetic. It also carries the boarding gate's blocking banner,
// above the tile row — the same block Manifest shows, so a driver learns what is owed before
// walking to the vehicle rather than after sitting down with a manifest open.

export default function Today({
  onOpenTrip,
  onStartInspection,
}: {
  onOpenTrip: (tripId: string) => void;
  onStartInspection: (mode: InspectionMode) => void;
}) {
  const { evaluation } = useHosLedger();
  const b = band(evaluation);
  const next = assignedTrips[0];

  // The gate reads localStorage, so it waits for hydration — see lib/useInspectionStore.ts.
  const hydrated = useInspectionStoreHydrated();
  const gate = hydrated ? boardingGate(next.vehicleId) : null;

  return (
    <Screen
      eyebrow={`${currentDriver.homeBase} · ${currentDriver.employeeNumber}`}
      title={`Good morning, ${currentDriver.name}`}
      right={<MockTag />}
    >
      {gate ? <BoardingGateBanner gate={gate} onStartInspection={onStartInspection} /> : null}

      <div style={{ display: "flex", gap: gap.row, marginBottom: gap.section }}>
        <TouchTile
          label="Driving left today"
          value={durationLabel(evaluation.remainingDrivingTodaySeconds)}
          kind={b.kind}
          statusLabel={b.label}
          footnote={`${durationLabel(evaluation.cycle.remainingSeconds)} left in the 70h cycle · planning figures, not the ELD record`}
        />
        <TouchTile
          label="Next trip"
          value={formatClock(next.startsAt)}
          unit={next.tripNumber}
          kind={next.tk}
          statusLabel={next.status}
          footnote={`${next.origin} → ${next.destination}`}
        />
        <TouchTile
          label="Your vehicle"
          value={assignedVehicle.unit}
          kind={assignedVehicle.vk}
          statusLabel={assignedVehicle.status}
          footnote={assignedVehicle.description}
        />
        {/* Open = not resolved. A defect already on a work order is still open and still
            counted here — the driver must see it before the pre-trip. Scoped to the assigned
            vehicle: this tile answers "is anything wrong with what I am driving". */}
        <TouchTile
          label="Open defects"
          value={String(vehicleOpen.length)}
          kind={worstDefectKind(vehicleOpen)}
          statusLabel={vehicleOpen.length === 0 ? "None" : "Open"}
          footnote={
            vehicleOpen[0]
              ? vehicleOpen[0].workOrder
                ? `${vehicleOpen[0].item} · ${openDefectWorkOrderText(vehicleOpen[0])}`
                : vehicleOpen[0].item
              : "Nothing outstanding"
          }
        />
      </div>

      <Heading>Duty status</Heading>
      <Panel style={{ padding: "18px 20px" }}>
        <DutyControl />
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 14,
            color: colors.textDim,
            marginTop: 14,
            lineHeight: 1.55,
          }}
        >
          Changing duty status queues an hours-of-service event, moves the figures above at
          once, and reaches the server with the next sync — the pill in the top bar says when.
          Your record of duty status is on the bus&rsquo;s ELD, not here. The trips and vehicle
          on this screen are still mock.
        </div>
      </Panel>

      <Heading right={<MockTag />}>Today&rsquo;s trips</Heading>
      {assignedTrips.map((trip) => (
        <CardRow key={trip.id} onClick={() => onOpenTrip(trip.id)}>
          <div style={{ flex: "none", width: 96 }}>
            <span
              style={{
                fontFamily: fonts.mono,
                fontSize: type.value,
                fontVariantNumeric: "tabular-nums",
                color: colors.headingBright,
              }}
            >
              {formatClock(trip.startsAt)}
            </span>
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine
              label={trip.tripNumber}
              value={`${trip.origin} → ${trip.destination} · ${trip.clientName}`}
            />
          </div>
          <div style={{ flex: "none", display: "flex", alignItems: "center", gap: 12 }}>
            <TabletChip kind={trip.tk} label={trip.status} />
            <TouchButton variant="secondary" onClick={() => onOpenTrip(trip.id)}>
              Manifest
            </TouchButton>
          </div>
        </CardRow>
      ))}
    </Screen>
  );
}
