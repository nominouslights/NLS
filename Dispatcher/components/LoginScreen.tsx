"use client";

import { useState, type FormEvent } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { TextField } from "@/components/ui/Field";
import { login, redeemAdminInvite } from "@/lib/auth";
import { ApiError } from "@/lib/api";

// Full-screen sign-in for the Dispatch Console. On success, lib/auth notifies
// AuthGate (onAuthChange) which swaps this screen for the Console — no
// callback prop needed.
//
// A local state toggle (no routing) swaps in the admin-invite redemption form:
// a new administrator pastes a one-time token minted in Settings → Users &
// Roles, picks their credentials, and redeemAdminInvite signs them straight in.

const MIN_PASSWORD_LENGTH = 8;

export default function LoginScreen() {
  const [mode, setMode] = useState<"login" | "invite">("login");
  const [token, setToken] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<{ code: string; message: string } | null>(null);

  const over = statusMeta("over"); // vermillion — never colour alone (glyph + text below)

  function switchMode(next: "login" | "invite") {
    if (pending) return;
    setMode(next);
    setError(null);
    setPassword("");
    setConfirm("");
  }

  async function submitLogin(e: FormEvent) {
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

  async function submitInvite(e: FormEvent) {
    e.preventDefault();
    if (pending) return;

    if (!token.trim()) {
      setError({ code: "Validation.Required", message: "Paste the invite token you were given." });
      return;
    }
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
      // Creates the account, then signs straight in — AuthGate flips to the
      // Console via onAuthChange. Backend errors (invalid/expired/used token,
      // duplicate email) surface verbatim below.
      await redeemAdminInvite(token.trim(), email.trim(), password);
    } catch (err) {
      if (err instanceof ApiError) {
        setError({ code: err.code, message: err.message });
      } else {
        setError({ code: "Unknown", message: "Something went wrong. Please try again." });
      }
      setPending(false);
    }
  }

  const errorBox = error && (
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
          {mode === "login" ? "Sign-in failed" : "Couldn't redeem the invite"}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textSecondary, marginTop: 1 }}>
          {error.message}
        </div>
        <div style={{ fontFamily: fonts.mono, fontSize: 10, color: colors.textDim, marginTop: 3 }}>
          {error.code}
        </div>
      </div>
    </div>
  );

  const submitButtonStyle = {
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
  } as const;

  const toggleLinkStyle = {
    background: "none",
    border: "none",
    padding: 0,
    fontFamily: fonts.body,
    fontSize: 11.5,
    color: colors.blue,
    cursor: pending ? "default" : "pointer",
    textDecoration: "underline",
    textUnderlineOffset: 3,
  } as const;

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

        {mode === "login" ? (
          <Panel style={{ padding: "20px 22px 22px" }}>
            <SectionLabel>Sign in</SectionLabel>
            <form onSubmit={submitLogin} style={{ display: "flex", flexDirection: "column", gap: 13 }}>
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

              {errorBox}

              <button type="submit" disabled={pending} style={submitButtonStyle}>
                {pending ? "SIGNING IN…" : "SIGN IN"}
              </button>
            </form>
            <div style={{ textAlign: "center", marginTop: 14 }}>
              <button type="button" onClick={() => switchMode("invite")} style={toggleLinkStyle}>
                Have an admin invite token?
              </button>
            </div>
          </Panel>
        ) : (
          <Panel style={{ padding: "20px 22px 22px" }}>
            <SectionLabel>Redeem admin invite</SectionLabel>
            <div
              style={{
                fontFamily: fonts.body,
                fontSize: 12,
                color: colors.textMuted,
                lineHeight: 1.6,
                marginBottom: 13,
              }}
            >
              Paste the one-time token an administrator generated for you, then choose the email
              and password you&apos;ll sign in with. Tokens are single-use and expire 15 minutes
              after they&apos;re issued.
            </div>
            <form onSubmit={submitInvite} style={{ display: "flex", flexDirection: "column", gap: 13 }}>
              <TextField
                label="Invite token"
                value={token}
                onChange={setToken}
                mono
                autoComplete="off"
                placeholder="Paste the token here"
                disabled={pending}
              />
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

              {errorBox}

              <button type="submit" disabled={pending} style={submitButtonStyle}>
                {pending ? "CREATING ACCOUNT…" : "REDEEM INVITE & SIGN IN"}
              </button>
            </form>
            <div style={{ textAlign: "center", marginTop: 14 }}>
              <button type="button" onClick={() => switchMode("login")} style={toggleLinkStyle}>
                Back to sign in
              </button>
            </div>
          </Panel>
        )}

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
