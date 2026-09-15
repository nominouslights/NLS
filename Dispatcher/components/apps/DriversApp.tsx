"use client";

import type { AppProps } from "@/components/apps/shell";
import { useRetainedState } from "@/lib/appState";
import Drivers from "@/components/screens/Drivers";
import Incidents from "@/components/screens/Incidents";

// Drivers & Compliance — credentials, hours of service, incidents and faults.
export default function DriversApp({ screen }: AppProps) {
  const [driverSel, setDriverSel] = useRetainedState("drivers.sel", 0);
  const [incidentSel, setIncidentSel] = useRetainedState("drivers.incidentSel", 0);

  return (
    <>
      {screen === "drivers" && <Drivers driverSel={driverSel} setDriverSel={setDriverSel} />}
      {screen === "incidents" && <Incidents incidentSel={incidentSel} setIncidentSel={setIncidentSel} />}
    </>
  );
}
