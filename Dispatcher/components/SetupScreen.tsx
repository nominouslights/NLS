"use client";

import { useState, type FormEvent } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { TextField } from "@/components/ui/Field";
import { createFirstAdmin } from "@/lib/auth";
import { ApiError } from "@/lib/api";

// First-run setup for the Dispatch Console: shown only while the backend has no
// users at all (AuthGate checks /auth/setup-status). Creates the initial Admin
// and signs straight in — lib/auth notifies AuthGate, which swaps in the Console.

const MIN_PASSWORD_LENGTH = 8;

export default function SetupScreen() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<{ code: string; message: string } | null>(null);

  const over = statusMeta("over"); // vermillion — never colour alone (glyph + text below)

  async function submit(e: FormEvent) {
    e.preventDefault();
    if (pending) return;

    if (!email.trim() || !password) {
      setError({ code: "Validation.Required", message: "Enter an email and password." });
      return;
    }
    if (password.length < MIN_PASSWORD_LENGTH) {
      setError({
        code: "Validation.PasswordTooShort",
        message: `Use at least ${MIN_PASSWORD_LENGTH} characters for the administrator password.`,
      });
      return;
    }
    if (password !== confirm) {
      setError({ code: "Validation.PasswordMismatch", message: "The two passwords don't match." });
      return;
    }

    setPending(true);
    setError(null);
    try {
      await createFirstAdmin(email.trim(), password);
    } catch (err) {
      if (err instanceof ApiError) {
        // 409 — someone else completed setup first; a reload drops to the login form.
        setError(
          err.status === 409
            ? { code: err.code, message: "Setup was already completed. Reload to sign in." }
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
            First-run setup
          </div>
        </div>

        <Panel style={{ padding: "20px 22px 22px" }}>
          <SectionLabel>Create administrator</SectionLabel>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 12,
              color: colors.textMuted,
              lineHeight: 1.6,
              marginBottom: 13,
            }}
          >
            No accounts exist yet. This creates the first administrator — full access to the
            Dispatch Console. You can add more administrators afterwards.
          </div>
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
              autoComplete="new-password"
              disabled={pending}
            />
            <TextField
              label="Confirm password"
              value={confirm}
              onChange={setConfirm}
              type="password"
              autoComplete="new-password"
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
                    Couldn&apos;t create the administrator
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
              {pending ? "CREATING…" : "CREATE ADMINISTRATOR"}
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
          Shown once, on a brand-new system. Store these credentials somewhere safe.
        </div>
      </div>
    </div>
  );
}
