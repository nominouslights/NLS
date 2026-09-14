"use client";

import type { AppProps } from "@/components/apps/shell";
import DispatchBoard from "@/components/screens/DispatchBoard";
import LiveMap from "@/components/screens/LiveMap";

// Today — what is moving right now. Nothing to retain: both screens own their fetch and
// neither carries a selection worth surviving a detour.
export default function TodayApp({ screen, shell }: AppProps) {
  return (
    <>
      {screen === "dispatch" && <DispatchBoard onOpenTrip={shell.openTrip} />}
      {screen === "map" && <LiveMap onOpenTrip={() => shell.openTrip(null)} />}
    </>
  );
}
