"use client";

import { useEffect, useRef, useState } from "react";
import { colors } from "@/lib/theme";
import type { ScreenId } from "@/lib/nav";
import { appForScreen, canUseApp, railGroupsFor, type AppId, type InternalApp } from "@/lib/apps";
import { clearRetainedState } from "@/lib/appState";
import { getRole } from "@/lib/claims";
import type { Shell } from "@/components/apps/shell";
import NavRail from "@/components/NavRail";
import TopBar from "@/components/TopBar";
import Home from "@/components/Home";
import CreateTripWizard from "@/components/CreateTripWizard";
import TodayApp from "@/components/apps/TodayApp";
import TripOpsApp from "@/components/apps/TripOpsApp";
import FleetApp from "@/components/apps/FleetApp";
import DriversApp from "@/components/apps/DriversApp";
import CommunityBookingApp from "@/components/apps/CommunityBookingApp";
import CommercialApp from "@/components/apps/CommercialApp";
import AdminApp from "@/components/apps/AdminApp";

// The console is a two-level shell: the Home launcher, then one app at a time, each rendering
// its own NavRail group and screens (components/apps/*App.tsx; manifest in lib/apps.ts). The
// apps' selection/period/tab state survives a trip Home in lib/appState.ts; the one selection
// that crosses apps — the trip openTrip() lands on — stays here.

type Location = ScreenId | "home";

export default function Console() {
  const [location, setLocation] = useState<Location>("home");
  const [railCollapsed, setRailCollapsed] = useState(false);
  const [wizardOpen, setWizardOpen] = useState(false);
  const [tripSelId, setTripSelId] = useState<string | null>(null); // Trips API Guid
  // Reopening an app returns to the screen it was left on.
  const lastScreen = useRef<Partial<Record<AppId, ScreenId>>>({});

  // Non-reactive on purpose (Budgeting's RoleGate precedent): the role changes only with the
  // token, and every path that replaces the token re-renders through AuthGate. A UX gate only.
  const role = getRole();

  const screen = location === "home" ? null : location;
  const app = screen === null ? null : appForScreen(screen);
  // CREATE TRIP lands on Trip Operations → Trips, so a role that cannot open that app (an
  // Accountant, say) would create a trip and be bounced Home without seeing it. Hide the button.
  const canCreateTrip = canUseApp(appForScreen("trips"), role);

  // Console unmounts only on sign-out: forget every app's retained state so the next account
  // inherits nothing.
  useEffect(() => () => clearRetainedState(), []);

  function navigate(target: ScreenId) {
    const targetApp = appForScreen(target);
    if (!canUseApp(targetApp, role)) {
      setLocation("home");
      return;
    }
    lastScreen.current[targetApp.id] = target;
    setLocation(target);
  }

  function openApp(a: InternalApp) {
    navigate(lastScreen.current[a.id] ?? a.screens[0].id);
  }

  function goHome() {
    setLocation("home");
  }

  function openTrip(id: string | null) {
    setTripSelId(id);
    navigate("trips");
  }

  const shell: Shell = {
    openTrip,
    createTrip: () => setWizardOpen(true),
    navigate,
    goHome,
  };

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
      <TopBar
        onToggleRail={() => setRailCollapsed((v) => !v)}
        onCreateTrip={canCreateTrip ? () => setWizardOpen(true) : null}
        onHome={goHome}
        app={app && { label: app.label, code: app.code }}
      />

      <div style={{ display: "flex", flex: 1, minHeight: 0 }}>
        {app && screen && (
          <NavRail
            screen={screen}
            collapsed={railCollapsed}
            onSelect={navigate}
            groups={railGroupsFor(app)}
            onHome={goHome}
          />
        )}

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
          {app === null || screen === null ? (
            <Home role={role} onOpenApp={openApp} />
          ) : (
            <>
              {app.id === "today" && <TodayApp screen={screen} shell={shell} />}
              {app.id === "tripOps" && (
                <TripOpsApp screen={screen} shell={shell} tripSelId={tripSelId} setTripSelId={setTripSelId} />
              )}
              {app.id === "fleet" && <FleetApp screen={screen} shell={shell} />}
              {app.id === "drivers" && <DriversApp screen={screen} shell={shell} />}
              {app.id === "communityBooking" && <CommunityBookingApp screen={screen} shell={shell} />}
              {app.id === "commercial" && <CommercialApp screen={screen} shell={shell} />}
              {app.id === "admin" && <AdminApp screen={screen} shell={shell} />}
            </>
          )}
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
