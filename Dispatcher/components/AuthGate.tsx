"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import Console from "@/components/Console";
import LoginScreen from "@/components/LoginScreen";
import { hasStoredSession, onAuthChange, restoreSession } from "@/lib/auth";

// Root auth gate: restores a persisted session silently on mount (brief splash,
// no login-form flash), then renders the Console when authenticated or the
// LoginScreen otherwise. Reacts to lib/auth state changes — login, logout, and
// forced sign-out when a token refresh definitively fails.

type Phase = "restoring" | "signedOut" | "signedIn";

export default function AuthGate() {
  const [phase, setPhase] = useState<Phase>("restoring");

  useEffect(() => {
    const unsubscribe = onAuthChange((authenticated) =>
      setPhase(authenticated ? "signedIn" : "signedOut"),
    );
    if (hasStoredSession()) {
      restoreSession().then((ok) => setPhase(ok ? "signedIn" : "signedOut"));
    } else {
      setPhase("signedOut");
    }
    return unsubscribe;
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
          gap: 8,
        }}
      >
        <div style={{ display: "inline-flex", alignItems: "center", gap: 2 }}>
          <span
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 24,
              letterSpacing: ".02em",
              color: colors.headingBright,
            }}
          >
            NORTHERN
          </span>
          <span
            style={{
              fontFamily: fonts.condensed,
              fontWeight: 700,
              fontSize: 24,
              letterSpacing: ".02em",
              color: colors.amberText,
            }}
          >
            LINK
          </span>
        </div>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 11,
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

  if (phase === "signedOut") return <LoginScreen />;
  return <Console />;
}
