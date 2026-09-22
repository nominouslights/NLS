"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchTile } from "@/components/ui-tablet/TouchTile";
import { DutyControl } from "@/components/ui-tablet/DutyControl";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { Screen, MockTag, Heading, TabletChip, FieldLine, CardRow } from "./shared";
import { BoardingGateBanner } from "./inspection/BoardingGateBanner";
import type { DutyState, InspectionMode } from "@/lib/types";
import { boardingGate } from "@/lib/inspectionGate";
import { useInspectionStoreHydrated } from "@/lib/useInspectionStore";
import {
  assignedTrips,
  assignedVehicle,
  currentDriver,
  formatClock,
  formatDurationH,
  hosRemaining,
  openDefects,
} from "@/lib/data";

// The screen a driver opens on. Answers four questions without a tap: how many hours do I have
// left, what is next, what am I driving, and is anything wrong with it.
//
// It also carries the boarding gate's blocking banner, above the tile row — the same block
// Manifest shows, so a driver learns what is owed before walking to the vehicle rather than
// after sitting down with a manifest open.

export default function Today({
  duty,
  onDutyChange,
  onOpenTrip,
  onStartInspection,
}: {
  duty: DutyState;
  onDutyChange: (next: DutyState) => void;
  onOpenTrip: (tripId: string) => void;
  onStartInspection: (mode: InspectionMode) => void;
}) {
  const hos = hosRemaining();
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
          value={formatDurationH(hos.drivingRemainingH)}
          kind={hos.hk}
          statusLabel={hos.hk === "over" ? "At limit" : hos.hk === "soon" ? "Low" : "OK"}
          footnote={`${formatDurationH(hos.cycleRemainingH)} left in the 7-day cycle`}
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
        <TouchTile
          label="Open defects"
          value={String(openDefects.length)}
          kind={openDefects.length === 0 ? "ontime" : "soon"}
          statusLabel={openDefects.length === 0 ? "None" : "Reported"}
          footnote={openDefects[0]?.item ?? "Nothing outstanding"}
        />
      </div>

      <Heading>Duty status</Heading>
      <Panel style={{ padding: "18px 20px" }}>
        <DutyControl duty={duty} onChange={onDutyChange} />
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: 14,
            color: colors.textDim,
            marginTop: 14,
            lineHeight: 1.55,
          }}
        >
          Changing duty status writes an hours-of-service entry. Sync is not enabled in this
          build, so nothing here has reached the server.
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
