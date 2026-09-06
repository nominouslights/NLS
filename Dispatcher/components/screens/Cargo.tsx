"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { PageHeader } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import ShipmentsPane from "@/components/screens/cargo/ShipmentsPane";
import CargoSchedulesPane from "@/components/screens/cargo/CargoSchedulesPane";

// Cargo & Grocery — the operational home for the diversified-revenue cargo
// business, replacing the old hardcoded "Grocery & Parcel" prototype. Two tabs:
//   Shipments       — the real Shipment aggregate (/api/trips/shipments):
//                     register, route onto Cargo/Grocery trips, track legs,
//                     deliver, cancel.
//   Cargo schedules — the cargo/grocery schedule templates, read-only, with
//                     per-template SPECIAL DATES (Skip / ExtraRun /
//                     TimeOverride). Template CRUD stays single-sourced in
//                     Routes & Schedules.

export type CargoTab = "shipments" | "schedules";

const TABS: { id: CargoTab; label: string }[] = [
  { id: "shipments", label: "Shipments" },
  { id: "schedules", label: "Cargo schedules" },
];

export default function Cargo({
  tab,
  setTab,
  selectedId,
  setSelectedId,
  onOpenTrip,
}: {
  /** Tab + selection live in Console so they survive navigating away and back. */
  tab: CargoTab;
  setTab: (t: CargoTab) => void;
  selectedId: string | null;
  setSelectedId: (id: string | null) => void;
  /** Jump to a trip on the Trips screen (assigned legs, generated-trip warnings). */
  onOpenTrip: (tripId: string) => void;
}) {
  const [registerOpen, setRegisterOpen] = useState(false);

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade">
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader
          eyebrow="Operations · Diversified-revenue logistics on spare capacity"
          title="Cargo & Grocery"
          right={
            tab === "shipments" ? (
              <ActionButton variant="primary" onClick={() => setRegisterOpen(true)}>
                + REGISTER SHIPMENT
              </ActionButton>
            ) : undefined
          }
        />
        <div style={{ display: "flex", gap: 8, marginTop: 14 }}>
          {TABS.map(({ id, label }) => (
            <span
              key={id}
              onClick={() => setTab(id)}
              style={{
                fontFamily: fonts.body,
                fontWeight: tab === id ? 600 : 500,
                fontSize: 12,
                padding: "5px 12px",
                borderRadius: 7,
                background: tab === id ? colors.cardBgActive : colors.cardBg,
                border: `1px solid ${tab === id ? colors.borderActive : colors.border}`,
                color: tab === id ? colors.headingBright : colors.textMuted,
                cursor: "pointer",
                userSelect: "none",
              }}
            >
              {label}
            </span>
          ))}
        </div>
      </div>

      {tab === "shipments" ? (
        <ShipmentsPane
          selectedId={selectedId}
          setSelectedId={setSelectedId}
          onOpenTrip={onOpenTrip}
          registerOpen={registerOpen}
          setRegisterOpen={setRegisterOpen}
        />
      ) : (
        <CargoSchedulesPane onOpenTrip={onOpenTrip} />
      )}
    </div>
  );
}
