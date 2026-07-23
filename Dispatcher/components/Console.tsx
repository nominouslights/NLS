"use client";

import { useState } from "react";
import { colors } from "@/lib/theme";
import type { ScreenId } from "@/lib/nav";
import NavRail from "@/components/NavRail";
import TopBar from "@/components/TopBar";
import CreateTripWizard from "@/components/CreateTripWizard";
import DispatchBoard from "@/components/screens/DispatchBoard";
import LiveMap from "@/components/screens/LiveMap";
import Trips from "@/components/screens/Trips";
import Drivers from "@/components/screens/Drivers";
import Fleet from "@/components/screens/Fleet";
import Clients from "@/components/screens/Clients";
import Riders from "@/components/screens/Riders";
import Billing from "@/components/screens/Billing";
import Manifests from "@/components/screens/Manifests";
import ManualTripEntry from "@/components/screens/manualtrip/ManualTripEntry";
import RoutesSchedules from "@/components/screens/RoutesSchedules";
import Grocery from "@/components/screens/Grocery";
import Incidents from "@/components/screens/Incidents";
import Communications from "@/components/screens/Communications";
import Settings from "@/components/screens/Settings";

export default function Console() {
  const [screen, setScreen] = useState<ScreenId>("dispatch");
  const [railCollapsed, setRailCollapsed] = useState(false);
  const [wizardOpen, setWizardOpen] = useState(false);

  const [tripSelId, setTripSelId] = useState<string | null>(null); // Trips API Guid
  const [driverSel, setDriverSel] = useState(0);
  const [fleetSelId, setFleetSelId] = useState<string | null>(null);
  const [clientSel, setClientSel] = useState<string | null>(null); // Clients API Guid
  const [invoiceSelId, setInvoiceSelId] = useState<string | null>(null); // Billing API Guid
  const [riderSel, setRiderSel] = useState(0);
  const [incidentSel, setIncidentSel] = useState(0);

  function openTrip(id: string | null) {
    setTripSelId(id);
    setScreen("trips");
  }

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        height: "100vh",
        width: "100%",
        background: colors.pageBg,
        overflow: "hidden",
      }}
    >
      <TopBar onToggleRail={() => setRailCollapsed((v) => !v)} onCreateTrip={() => setWizardOpen(true)} />

      <div style={{ display: "flex", flex: 1, minHeight: 0 }}>
        <NavRail screen={screen} collapsed={railCollapsed} onSelect={setScreen} />

        <div
          style={{
            flex: 1,
            minWidth: 0,
            background: colors.mainBg,
            display: "flex",
            flexDirection: "column",
            overflow: "hidden",
          }}
        >
          {screen === "dispatch" && <DispatchBoard onOpenTrip={openTrip} />}
          {screen === "map" && <LiveMap onOpenTrip={() => openTrip(null)} />}
          {screen === "trips" && (
            <Trips selectedId={tripSelId} setSelectedId={setTripSelId} onNewTrip={() => setWizardOpen(true)} />
          )}
          {screen === "drivers" && <Drivers driverSel={driverSel} setDriverSel={setDriverSel} />}
          {screen === "fleet" && <Fleet fleetSelId={fleetSelId} setFleetSelId={setFleetSelId} />}
          {screen === "routes" && <RoutesSchedules />}
          {screen === "manifests" && <Manifests />}
          {screen === "manualtrip" && <ManualTripEntry />}
          {screen === "grocery" && <Grocery />}
          {screen === "clients" && (
            <Clients clientSel={clientSel} setClientSel={setClientSel} onCreateTrip={() => setWizardOpen(true)} />
          )}
          {screen === "riders" && <Riders riderSel={riderSel} setRiderSel={setRiderSel} />}
          {screen === "billing" && <Billing invoiceSelId={invoiceSelId} setInvoiceSelId={setInvoiceSelId} />}
          {screen === "incidents" && <Incidents incidentSel={incidentSel} setIncidentSel={setIncidentSel} />}
          {screen === "comms" && <Communications />}
          {screen === "settings" && <Settings />}
        </div>
      </div>

      {wizardOpen && (
        <CreateTripWizard
          onClose={() => setWizardOpen(false)}
          onCreated={(tripId) => {
            setWizardOpen(false);
            openTrip(tripId);
          }}
        />
      )}
    </div>
  );
}
