"use client";

import { useState } from "react";
import { colors } from "@/lib/theme";
import type { ScreenId } from "@/lib/nav";
import type { DutyState } from "@/lib/types";
import { currentDuty, incidents } from "@/lib/data";
import TopBar from "@/components/TopBar";
import DutyRail from "@/components/DutyRail";
import Today from "@/components/screens/Today";
import Trips from "@/components/screens/Trips";
import Manifest from "@/components/screens/Manifest";
import Inspection from "@/components/screens/Inspection";
import Hours from "@/components/screens/Hours";
import Incidents from "@/components/screens/Incidents";
import Vehicle from "@/components/screens/Vehicle";
import Profile from "@/components/screens/Profile";

// The app shell. Same structure as Budgeting/components/Console.tsx and Dispatcher's: a top
// bar, then a flex row of rail + screen, with plain `&&` switching on a ScreenId union. No
// Next routing anywhere in this app — the tablet has no address bar to type into, and a
// back-button history a driver can get lost in is a liability, not a feature.
//
// The root frame is height:100vh / overflow:hidden; screens scroll internally. On a mounted
// tablet a page that scrolls as a whole means the top bar — duty state and sync — can leave the
// screen, and those two must always be visible.

export default function Console() {
  const [screen, setScreen] = useState<ScreenId>("today");
  const [railCollapsed, setRailCollapsed] = useState(false);

  // Hoisted because more than one screen reads them: TopBar and Today both show duty, and
  // Manifest's first board is what flips the active trip to In Transit. Budgeting's rule —
  // hoist only what more than one screen needs — applies here too.
  const [duty, setDuty] = useState<DutyState>(currentDuty.duty);
  const [activeScreenTripId, setActiveScreenTripId] = useState<string | null>(null);

  const openIncidents = incidents.filter((i) => i.status !== "Closed").length;

  return (
    <div
      style={{
        height: "100vh",
        width: "100%",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden",
        background: colors.pageBg,
      }}
    >
      <TopBar duty={duty} />

      <div style={{ flex: 1, minHeight: 0, display: "flex" }}>
        <DutyRail
          active={screen}
          onSelect={setScreen}
          collapsed={railCollapsed}
          onToggleCollapsed={() => setRailCollapsed((c) => !c)}
          pendingIncidents={openIncidents}
        />

        <div style={{ flex: 1, minWidth: 0, background: colors.mainBg }}>
          {screen === "today" && (
            <Today
              duty={duty}
              onDutyChange={setDuty}
              onOpenTrip={(id) => {
                setActiveScreenTripId(id);
                setScreen("manifest");
              }}
            />
          )}
          {screen === "trips" && (
            <Trips
              onOpenManifest={(id) => {
                setActiveScreenTripId(id);
                setScreen("manifest");
              }}
            />
          )}
          {screen === "manifest" && <Manifest tripId={activeScreenTripId} />}
          {screen === "inspection" && <Inspection />}
          {screen === "hours" && <Hours duty={duty} onDutyChange={setDuty} />}
          {screen === "incidents" && <Incidents />}
          {screen === "vehicle" && <Vehicle />}
          {screen === "profile" && <Profile />}
        </div>
      </div>
    </div>
  );
}
