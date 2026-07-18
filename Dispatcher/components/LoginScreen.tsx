"use client";

import { useState, type FormEvent } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { TextField } from "@/components/ui/Field";
import { login } from "@/lib/auth";
import { ApiError } from "@/lib/api";

// Full-screen sign-in for the Dispatch Console. On success, lib/auth notifies
// AuthGate (onAuthChange) which swaps this screen for the Console — no
// callback prop needed.

export default function LoginScreen() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<{ code: string; message: string } | null>(null);

  const over = statusMeta("over"); // vermillion — never colour alone (glyph + text below)

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
        // The backend's bad-credentials 401 carries no structured body — the
        // parsed fallback is "Http.401 / Unauthorized". Show something human.
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

  return (
    <div
      style={{
        height: "100vh",
        width: "100%",
        background: colors.pageBg,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 20,
      }}
    >
      <div style={{ width: 380, maxWidth: "100%" }}>
        <div style={{ textAlign: "center", marginBottom: 18 }}>
          <div style={{ display: "inline-flex", alignItems: "center", gap: 2 }}>
            <span
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 26,
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
                fontSize: 26,
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
              fontSize: 10.5,
              letterSpacing: ".16em",
              textTransform: "uppercase",
              color: colors.textDim,
              marginTop: 2,
            }}
          >
            Admin · Dispatch Console
          </div>
        </div>

        <Panel style={{ padding: "20px 22px 22px" }}>
          <SectionLabel>Sign in</SectionLabel>
          <form onSubmit={submit} style={{ display: "flex", flexDirection: "column", gap: 13 }}>
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

            {error && (
              <div
                role="alert"
                style={{
                  display: "flex",
                  gap: 9,
                  padding: "9px 12px",
                  borderRadius: 8,
                  background: over.bg,
                  border: `1px solid ${over.bd}`,
                }}
              >
                <span style={{ color: over.t, fontSize: 12, fontWeight: 800, lineHeight: "17px" }} aria-hidden>
                  {over.g}
                </span>
                <div style={{ minWidth: 0 }}>
                  <div style={{ fontFamily: fonts.body, fontWeight: 600, fontSize: 12.5, color: over.t }}>
                    Sign-in failed
                  </div>
                  <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary, marginTop: 1 }}>
                    {error.message}
                  </div>
                  <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 3 }}>
                    {error.code}
                  </div>
                </div>
              </div>
            )}

            <button
              type="submit"
              disabled={pending}
              style={{
                width: "100%",
                height: 40,
                border: "1px solid transparent",
                borderRadius: 8,
                background: colors.blue,
                color: "#FFFFFF",
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 14,
                letterSpacing: ".04em",
                cursor: pending ? "default" : "pointer",
                opacity: pending ? 0.65 : 1,
                marginTop: 2,
              }}
            >
              {pending ? "SIGNING IN…" : "SIGN IN"}
            </button>
          </form>
        </Panel>

        <div
          style={{
            textAlign: "center",
            marginTop: 14,
            fontFamily: fonts.body,
            fontSize: 11.5,
            color: colors.textDim,
          }}
        >
          Internal system — dispatchers &amp; supervisors. Contact your administrator for access.
        </div>
      </div>
    </div>
  );
}
