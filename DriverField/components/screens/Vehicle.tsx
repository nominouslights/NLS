"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { gap, radius, type } from "@/lib/tablet";
import { Panel } from "@/components/ui/Panel";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { Screen, MockTag, Heading, CardRow, FieldLine, TabletChip, EmptyNote } from "./shared";
import { enqueue } from "@/lib/sync/queue";
import { assignedVehicle, fuelEntries, vehicleDefects } from "@/lib/data";

// The narrow Fleet slice architecture §6 allows the Driver Field App: vehicle status and fault
// reporting, nothing more. No work-order management, no PM scheduling, no parts — those belong
// to the Dispatch Console and are now behind DispatchAccess on the API.
//
// Fuel has no backend resource at all: fuel is captured today only as three fields inside a
// DVIR submission (FuelAdded / FuelLitres / FuelCostCad). There is no fuel log to read from and
// nowhere to post one, which is why that section is read-only mock with its reason on screen.

export default function Vehicle() {
  const [faultNote, setFaultNote] = useState("");
  const [reported, setReported] = useState(false);

  const defects = vehicleDefects.filter((d) => d.vehicleId === assignedVehicle.id);

  async function reportFault() {
    await enqueue("vehicle.fault", { vehicleId: assignedVehicle.id, note: faultNote });
    setReported(true);
    setFaultNote("");
  }

  return (
    <Screen
      eyebrow={assignedVehicle.description}
      title={assignedVehicle.unit}
      right={
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <TabletChip kind={assignedVehicle.vk} label={assignedVehicle.status} />
          <MockTag />
        </div>
      }
    >
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <div style={{ display: "flex", gap: gap.section, flexWrap: "wrap" }}>
          <FieldLine label="Odometer" value={`${assignedVehicle.odometerKm.toLocaleString("en-CA")} km`} />
          <FieldLine label="Seats" value={String(assignedVehicle.seats)} />
          <FieldLine label="Licence class required" value={`Class ${assignedVehicle.licenceClassRequired}`} />
          <FieldLine
            label="Outstanding failed inspection"
            value={assignedVehicle.hasFailedDvir ? "Yes" : "No"}
          />
        </div>
      </Panel>

      {reported && (
        <StatusBanner kind="soon" title="Held on this device.">
          Your fault report was recorded locally. Sync is not enabled, so nothing has reached
          maintenance — call dispatch if the vehicle is unsafe to operate.
        </StatusBanner>
      )}

      <Heading>Report a fault</Heading>
      <Panel style={{ padding: "18px 20px", marginBottom: gap.section }}>
        <textarea
          value={faultNote}
          onChange={(e) => setFaultNote(e.target.value)}
          placeholder="What is wrong with the vehicle?"
          rows={3}
          style={{
            width: "100%",
            minHeight: 96,
            padding: "14px 16px",
            borderRadius: radius.control,
            border: `1px solid ${colors.borderStrong}`,
            background: colors.inputBg,
            color: colors.textPrimary,
            fontFamily: fonts.body,
            fontSize: type.label,
            resize: "vertical",
          }}
        />
        <div style={{ display: "flex", gap: 12, marginTop: 12, flexWrap: "wrap" }}>
          <TouchButton
            onClick={() => void reportFault()}
            disabled={!faultNote.trim()}
            disabledReason="Describe the fault first."
          >
            Report fault
          </TouchButton>
          <TouchButton
            variant="danger"
            disabled
            disabledReason="Taking a vehicle out of service is a dispatch action — POST /api/fleet/vehicles/{id}/status is behind DispatchAccess."
          >
            Take out of service
          </TouchButton>
        </div>
      </Panel>

      <Heading right={<MockTag />}>Open defects</Heading>
      {defects.length === 0 ? (
        <EmptyNote>Nothing outstanding on {assignedVehicle.unit}.</EmptyNote>
      ) : (
        defects.map((d) => (
          <CardRow key={d.id}>
            <div style={{ flex: "none", width: 130 }}>
              <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
                {d.reportedOn}
              </span>
            </div>
            <div style={{ flex: 1, minWidth: 0 }}>
              <FieldLine label={d.item} value={d.note} />
            </div>
            <div style={{ flex: "none", display: "flex", alignItems: "center", gap: 10 }}>
              <TabletChip kind={d.dk} label={d.severity} />
              <span style={{ fontFamily: fonts.mono, fontSize: 14, color: colors.textDim, width: 90 }}>
                {d.workOrder ?? "No WO"}
              </span>
            </div>
          </CardRow>
        ))
      )}

      <Heading right={<MockTag />}>Fuel</Heading>
      <StatusBanner kind="off" title="There is no fuel log on the backend.">
        Fuel is captured today only as three fields inside a DVIR submission. Nothing here can be
        read from or written to the API.
      </StatusBanner>
      {fuelEntries.map((f) => (
        <CardRow key={f.id}>
          <div style={{ flex: "none", width: 170 }}>
            <span style={{ fontFamily: fonts.mono, fontSize: 15, color: colors.textDim }}>
              {f.filledAt.slice(0, 16).replace("T", " ")}
            </span>
          </div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <FieldLine label={f.location} value={`${f.litres} L · ${f.odometerKm.toLocaleString("en-CA")} km`} />
          </div>
          <div style={{ flex: "none" }}>
            <span style={{ fontFamily: fonts.mono, fontSize: type.value, color: colors.textSecondary }}>
              ${f.costCad}
            </span>
          </div>
        </CardRow>
      ))}
    </Screen>
  );
}
