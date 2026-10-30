"use client";

import { useCallback } from "react";
import type { AppProps } from "@/components/apps/shell";
import { useRetainedState } from "@/lib/appState";
import { currentPeriod, type Period } from "@/lib/period";
import Drivers, { HOS_TAB_INDEX, type DriverFocus } from "@/components/screens/Drivers";
import Incidents from "@/components/screens/Incidents";
import Compliance, { type ComplianceTab } from "@/components/screens/Compliance";

// Drivers & Compliance — credentials, hours of service, incidents and faults, and (for the
// compliance roles) the cross-driver Compliance screen.
export default function DriversApp({ screen, shell }: AppProps) {
  const [driverSel, setDriverSel] = useRetainedState("drivers.sel", 0);
  const [incidentSel, setIncidentSel] = useRetainedState("drivers.incidentSel", 0);
  const [complianceTab, setComplianceTab] = useRetainedState<ComplianceTab>("drivers.complianceTab", "Audit");
  const [auditPeriod, setAuditPeriod] = useRetainedState<Period>("drivers.auditPeriod", () => currentPeriod("month"));
  // A hand-off from Compliance to Drivers: which driver to select and which tab to open. The
  // Drivers screen applies it once its roster lands, then clears it, so a later visit to
  // Drivers keeps whatever selection the user made there.
  const [focus, setFocus] = useRetainedState<DriverFocus | null>("drivers.focus", null);

  const { navigate } = shell;
  const openDriver = useCallback(
    (driverId: string) => {
      setFocus({ driverId, tab: HOS_TAB_INDEX });
      navigate("drivers");
    },
    [navigate, setFocus],
  );
  const clearFocus = useCallback(() => setFocus(null), [setFocus]);

  return (
    <>
      {screen === "drivers" && <Drivers driverSel={driverSel} setDriverSel={setDriverSel} focus={focus} onFocusApplied={clearFocus} />}
      {screen === "incidents" && <Incidents incidentSel={incidentSel} setIncidentSel={setIncidentSel} />}
      {screen === "compliance" && (
        <Compliance tab={complianceTab} setTab={setComplianceTab} period={auditPeriod} setPeriod={setAuditPeriod} onOpenDriver={openDriver} />
      )}
    </>
  );
}
