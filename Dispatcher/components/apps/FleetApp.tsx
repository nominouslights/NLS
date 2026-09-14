"use client";

import type { AppProps } from "@/components/apps/shell";
import { useRetainedState } from "@/lib/appState";
import Fleet from "@/components/screens/Fleet";

// Fleet & Maintenance — one screen today; the selected vehicle survives a detour.
export default function FleetApp({ screen }: AppProps) {
  const [fleetSelId, setFleetSelId] = useRetainedState<string | null>("fleet.selId", null);

  return <>{screen === "fleet" && <Fleet fleetSelId={fleetSelId} setFleetSelId={setFleetSelId} />}</>;
}
