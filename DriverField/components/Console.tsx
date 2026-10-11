"use client";

import { useEffect, useState } from "react";
import { colors } from "@/lib/theme";
import type { ScreenId } from "@/lib/nav";
import type { InspectionMode } from "@/lib/types";
import { incidents } from "@/lib/data";
import { getClaims } from "@/lib/auth";
import { inspectionDue } from "@/lib/inspectionGate";
import { startLedger } from "@/lib/hos/ledger";
import { startSync } from "@/lib/sync/queue";
import { useInspectionStoreHydrated } from "@/lib/useInspectionStore";
import TopBar from "@/components/TopBar";
import DutyRail from "@/components/DutyRail";
import HosAlerts from "@/components/HosAlerts";
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

  // Duty status is NOT hoisted here any more: it is the ledger store's current status
  // (lib/hos/ledger.ts), which TopBar, Today, Hours and the DutyControl all read through
  // useHosLedger() — one source, so the bar can never disagree with the figures. Budgeting's
  // rule — hoist only what more than one screen needs — still governs what IS here: Manifest's
  // first board is what flips the active trip to In Transit.
  const [activeScreenTripId, setActiveScreenTripId] = useState<string | null>(null);

  // The two live things come up with the shell and go down with it: the ledger (senders for
  // the hos.* commands, the cached server copy, the first fetch) and the queue's drain
  // triggers. Here and not at module level because both need a signed-in session — the
  // Console only mounts behind AuthGate and RoleGate. No state is set in this effect.
  useEffect(() => {
    const stopLedger = startLedger(getClaims()?.sub ?? null);
    const stopSync = startSync();
    return () => {
      stopSync();
      stopLedger();
    };
  }, []);

  // Hoisted on exactly the same grounds as activeScreenTripId: more than one screen needs it.
  // Manifest and Today WRITE it (their "Start pre-trip inspection" action), Inspection READS
  // it. `null` means "whatever inspectionDue() says", so the rail entry and a cold open agree.
  //
  // Nothing else about the inspection is hoisted — the draft lives at module level in
  // lib/inspectionStore.ts precisely so it need not be.
  const [inspectionMode, setInspectionMode] = useState<InspectionMode | null>(null);

  const startInspection = (mode: InspectionMode) => {
    setInspectionMode(mode);
    setScreen("inspection");
  };

  // One subscription so the rail badge tracks the draft store. See lib/useInspectionStore.ts
  // for why this is useSyncExternalStore and not an effect.
  const inspectionHydrated = useInspectionStoreHydrated();
  const due = inspectionHydrated ? inspectionDue() : null;

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
      <TopBar />

      <div style={{ flex: 1, minHeight: 0, display: "flex" }}>
        <DutyRail
          active={screen}
          onSelect={setScreen}
          collapsed={railCollapsed}
          onToggleCollapsed={() => setRailCollapsed((c) => !c)}
          pendingIncidents={openIncidents}
          inspectionDue={due}
        />

        <div style={{ flex: 1, minWidth: 0, background: colors.mainBg, display: "flex", flexDirection: "column" }}>
          {/* The hours alerts: a modal at 60/30/0 minutes and the out-of-hours banner, on
              every screen — a driver who is out of hours must not be able to navigate away
              from being told so. */}
          <HosAlerts />
          <div style={{ flex: 1, minHeight: 0 }}>
          {screen === "today" && (
            <Today
              onOpenTrip={(id) => {
                setActiveScreenTripId(id);
                setScreen("manifest");
              }}
              onStartInspection={startInspection}
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
          {screen === "manifest" && (
            <Manifest tripId={activeScreenTripId} onStartInspection={startInspection} />
          )}
          {screen === "inspection" && <Inspection mode={inspectionMode} />}
          {screen === "hours" && <Hours />}
          {screen === "incidents" && <Incidents />}
          {screen === "vehicle" && <Vehicle />}
          {screen === "profile" && <Profile />}
          </div>
        </div>
      </div>
    </div>
  );
}
