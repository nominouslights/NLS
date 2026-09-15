"use client";

import { useState, type FormEvent } from "react";
import { colors, fonts } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { TextField } from "@/components/ui/Field";
import { BrandScreen } from "@/components/Brandmark";
import { ErrorNotice } from "@/components/ErrorNotice";
import { touch } from "@/lib/tablet";
import { login } from "@/lib/auth";
import { ApiError } from "@/lib/api/transport";

// ADAPTED FROM Budgeting/components/LoginScreen.tsx — same flow, tablet sizing.
//
// On success, lib/auth notifies AuthGate (onAuthChange) which swaps this screen for the
// Console — no callback prop needed. There is no invite redemption and no create-account path:
// accounts are created in the Dispatch Console (see lib/auth.ts). Signing in with a role that
// lacks driver access is not an error — it lands on AccessDeniedScreen, which is RoleGate's
// job, not this screen's.

export default function LoginScreen() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<{ code: string; message: string } | null>(null);

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (pending) return;
    if (!email.trim() || !password) {
      setError({ code: "Validation.Required", message: "Enter your email and password." });
      return;
    }
    setPending(true);
    setError(null);
    try {
      await login(email.trim(), password);
    } catch (err) {
      if (err instanceof ApiError) {
        // The backend's bad-credentials 401 carries no structured body — the parsed fallback
        // is "Http.401 / Unauthorized". Show something human.
        setError(
          err.status === 401 && err.code === "Http.401"
            ? { code: err.code, message: "Email or password is incorrect." }
            : { code: err.code, message: err.message },
        );
      } else {
        setError({ code: "Unknown", message: "Something went wrong. Please try again." });
      }
      setPending(false);
    }
  }

  const submitButtonStyle = {
    width: "100%",
    height: touch.primary,
    border: "1px solid transparent",
    borderRadius: 8,
    background: colors.blue,
    color: "#FFFFFF",
    fontFamily: fonts.condensed,
    fontWeight: 700,
    fontSize: 19,
    letterSpacing: ".04em",
    cursor: pending ? "default" : "pointer",
    opacity: pending ? 0.65 : 1,
    marginTop: 4,
  } as const;

  return (
    <BrandScreen>
      <Panel style={{ padding: "22px 24px 24px" }}>
        <SectionLabel>Sign in</SectionLabel>
        <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          <TextField
            label="Email"
            value={email}
            onChange={setEmail}
            type="email"
            autoComplete="username"
            placeholder="you@northernlink.local"
            disabled={pending}
          />
          <TextField
            label="Password"
            value={password}
            onChange={setPassword}
            type="password"
            autoComplete="current-password"
            disabled={pending}
          />
          {error && <ErrorNotice title="Sign-in failed" message={error.message} code={error.code} />}
          <button type="submit" disabled={pending} style={submitButtonStyle}>
            {pending ? "SIGNING IN…" : "SIGN IN"}
          </button>
        </form>
      </Panel>

      <div
        style={{
          textAlign: "center",
          marginTop: 16,
          fontFamily: fonts.body,
          fontSize: 14,
          color: colors.textDim,
          lineHeight: 1.6,
        }}
      >
        Company device — driver accounts only.
        <br />
        Contact dispatch for access.
      </div>
    </BrandScreen>
  );
}
