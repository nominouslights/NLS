"use client";

import type { AppProps } from "@/components/apps/shell";
import { useRetainedState } from "@/lib/appState";
import { currentPeriod, type Period } from "@/lib/period";
import Trips from "@/components/screens/Trips";
import RoutesSchedules from "@/components/screens/RoutesSchedules";
import Stops from "@/components/screens/Stops";
import Manifests from "@/components/screens/Manifests";
import Cargo, { type CargoTab } from "@/components/screens/Cargo";

// Trip Operations — plan and run trips.
//
// The trip selection is the one piece of state Console still owns (`tripSelId`): openTrip()
// from any app sets it before landing here, so it cannot live inside this app.
export default function TripOpsApp({
  screen,
  shell,
  tripSelId,
  setTripSelId,
}: AppProps & {
  tripSelId: string | null; // Trips API Guid
  setTripSelId: (id: string | null) => void;
}) {
  // The Trips list's period and page live here, not in the screen: switching
  // screens unmounts it, and a dispatcher who steps back three months should not
  // lose that on a detour to the Fleet screen.
  const [tripPeriod, setTripPeriod] = useRetainedState<Period>("tripOps.period", () => currentPeriod("month"));
  const [tripPage, setTripPage] = useRetainedState("tripOps.page", 1);
  // Cargo & Grocery tab + shipment selection live here (tripPeriod precedent):
  // switching screens unmounts Cargo, and a dispatcher mid-shipment should not
  // lose their place on a detour to Trips.
  const [cargoTab, setCargoTab] = useRetainedState<CargoTab>("tripOps.cargoTab", "shipments");
  const [cargoSelId, setCargoSelId] = useRetainedState<string | null>("tripOps.cargoSelId", null); // Shipments API Guid

  return (
    <>
      {screen === "trips" && (
        <Trips
          selectedId={tripSelId}
          setSelectedId={setTripSelId}
          onNewTrip={shell.createTrip}
          period={tripPeriod}
          setPeriod={setTripPeriod}
          page={tripPage}
          setPage={setTripPage}
        />
      )}
      {screen === "routes" && <RoutesSchedules onOpenTrip={shell.openTrip} />}
      {screen === "stops" && <Stops />}
      {screen === "manifests" && <Manifests />}
      {screen === "cargo" && (
        <Cargo
          tab={cargoTab}
          setTab={setCargoTab}
          selectedId={cargoSelId}
          setSelectedId={setCargoSelId}
          onOpenTrip={shell.openTrip}
        />
      )}
    </>
  );
}
