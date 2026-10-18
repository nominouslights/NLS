"use client";

import type { AppProps } from "@/components/apps/shell";
import { canUseScreen } from "@/lib/apps";
import { getRole } from "@/lib/claims";
import DispatchBoard from "@/components/screens/DispatchBoard";
import LiveMap from "@/components/screens/LiveMap";

// Today — what is moving right now. Nothing to retain: both screens own their fetch and
// neither carries a selection worth surviving a detour.
export default function TodayApp({ screen, shell }: AppProps) {
  // The board's Compliance alerts tile lands a compliance role on the Compliance screen and
  // everyone else (dispatch) on Drivers. getRole() is read per render like Console's (UX only).
  const alertsTarget = canUseScreen("compliance", getRole()) ? "compliance" : "drivers";
  return (
    <>
      {screen === "dispatch" && <DispatchBoard onOpenTrip={shell.openTrip} onOpenCompliance={() => shell.navigate(alertsTarget)} />}
      {screen === "map" && <LiveMap onOpenTrip={() => shell.openTrip(null)} />}
    </>
  );
}
