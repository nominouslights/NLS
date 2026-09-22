"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import Console from "@/components/Console";
import LoginScreen from "@/components/LoginScreen";
import SetupPendingScreen from "@/components/SetupPendingScreen";
import RoleGate from "@/components/RoleGate";
import { Wordmark } from "@/components/Brandmark";
import { checkSetupRequired, hasStoredSession, onAuthChange, restoreSession } from "@/lib/auth";

// ADAPTED FROM Budgeting/components/AuthGate.tsx — identical phases and logic, tablet sizing.
//
// Root auth gate: on mount it first asks the backend whether first-run setup is still open (no
// users exist at all) and, if so, points at the Dispatch Console. Otherwise it restores a
// persisted session silently (brief splash, no login-form flash), then renders the Console when
// authenticated or the LoginScreen otherwise.
//
// The restore path matters more here than in the consoles. A driver opens this app at 05:30 in
// a yard with one bar of signal; lib/auth deliberately keeps the session alive when a refresh
// fails with a NETWORK error (status 0) and only signs out on a definitive 4xx. Losing a
// session to a dead zone would mean a driver who cannot log their pre-trip.
//
// Signed in is not the same as allowed in: the Console is wrapped in RoleGate.

type Phase = "restoring" | "setupRequired" | "signedOut" | "signedIn";

export default function AuthGate() {
  const [phase, setPhase] = useState<Phase>("restoring");

  useEffect(() => {
    let cancelled = false;
    const unsubscribe = onAuthChange((authenticated) =>
      setPhase(authenticated ? "signedIn" : "signedOut"),
    );

    async function resolvePhase(): Promise<Phase> {
      // A stored session means setup is long done — skip the probe entirely.
      if (hasStoredSession()) {
        return (await restoreSession()) ? "signedIn" : "signedOut";
      }
      try {
        if (await checkSetupRequired()) return "setupRequired";
      } catch {
        // Fail safe: never surface the setup screen on an errored/unreachable check — fall
        // through to the normal login form.
      }
      return "signedOut";
    }

    resolvePhase().then((next) => {
      if (!cancelled) setPhase(next);
    });

    return () => {
      cancelled = true;
      unsubscribe();
    };
  }, []);

  if (phase === "restoring") {
    return (
      <div
        style={{
          height: "100vh",
          width: "100%",
          background: colors.pageBg,
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: 10,
        }}
      >
        <Wordmark size={34} />
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 14,
            letterSpacing: ".14em",
            textTransform: "uppercase",
            color: colors.textDim,
          }}
        >
          Restoring session…
        </div>
      </div>
    );
  }

  if (phase === "setupRequired") return <SetupPendingScreen />;
  if (phase === "signedOut") return <LoginScreen />;

  return (
    <RoleGate>
      <Console />
    </RoleGate>
  );
}
