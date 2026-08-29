"use client";

import { useState } from "react";
import { colors } from "@/lib/theme";
import type { ScreenId } from "@/lib/nav";
import { currentPeriod, type Period } from "@/lib/period";
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
import Reports from "@/components/screens/Reports";
import Manifests from "@/components/screens/Manifests";
import RoutesSchedules from "@/components/screens/RoutesSchedules";
import Stops from "@/components/screens/Stops";
import Grocery from "@/components/screens/Grocery";
import Incidents from "@/components/screens/Incidents";
import Communications from "@/components/screens/Communications";
import Settings from "@/components/screens/Settings";

export default function Console() {
  const [screen, setScreen] = useState<ScreenId>("dispatch");
  const [railCollapsed, setRailCollapsed] = useState(false);
  const [wizardOpen, setWizardOpen] = useState(false);

  const [tripSelId, setTripSelId] = useState<string | null>(null); // Trips API Guid
  // The Trips list's period and page live here, not in the screen: switching
  // screens unmounts it, and a dispatcher who steps back three months should not
  // lose that on a detour to the Fleet screen.
  const [tripPeriod, setTripPeriod] = useState<Period>(() => currentPeriod("month"));
  const [tripPage, setTripPage] = useState(1);
  const [driverSel, setDriverSel] = useState(0);
  const [fleetSelId, setFleetSelId] = useState<string | null>(null);
  const [clientSel, setClientSel] = useState<string | null>(null); // Clients API Guid
  const [invoiceSelId, setInvoiceSelId] = useState<string | null>(null); // Billing API Guid
  // The accruals report's client + month live here for the same reason as
  // tripPeriod: a dispatcher who built March's report for a client should not
  // lose that selection on a detour to another screen.
  const [reportClientId, setReportClientId] = useState<string | null>(null); // Clients API Guid
  const [reportPeriod, setReportPeriod] = useState<Period>(() => currentPeriod("month"));
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
            <Trips
              selectedId={tripSelId}
              setSelectedId={setTripSelId}
              onNewTrip={() => setWizardOpen(true)}
              period={tripPeriod}
              setPeriod={setTripPeriod}
              page={tripPage}
              setPage={setTripPage}
            />
          )}
          {screen === "drivers" && <Drivers driverSel={driverSel} setDriverSel={setDriverSel} />}
          {screen === "fleet" && <Fleet fleetSelId={fleetSelId} setFleetSelId={setFleetSelId} />}
          {screen === "routes" && <RoutesSchedules />}
          {screen === "stops" && <Stops />}
          {screen === "manifests" && <Manifests />}
          {screen === "grocery" && <Grocery />}
          {screen === "clients" && (
            <Clients clientSel={clientSel} setClientSel={setClientSel} onCreateTrip={() => setWizardOpen(true)} />
          )}
          {screen === "riders" && <Riders riderSel={riderSel} setRiderSel={setRiderSel} />}
          {screen === "billing" && <Billing invoiceSelId={invoiceSelId} setInvoiceSelId={setInvoiceSelId} />}
          {screen === "reports" && (
            <Reports
              clientId={reportClientId}
              setClientId={setReportClientId}
              period={reportPeriod}
              setPeriod={setReportPeriod}
            />
          )}
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
            // Selecting the new trip is all the Trips screen needs: it polls for the
            // trip (reads trail writes), brings its period into view, and refreshes.
            openTrip(tripId);
          }}
        />
      )}
    </div>
  );
}
