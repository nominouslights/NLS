"use client";

import type { CSSProperties } from "react";
import { chipStyle, colors, fonts, statusMeta } from "@/lib/theme";
import { APPS, appBadge, canUseApp, type AppDef, type InternalApp } from "@/lib/apps";
import { getClaims, logout } from "@/lib/auth";
import { useToday } from "@/lib/useToday";
import { PageHeader, Panel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";

// Home — the launcher. One tile per app from the manifest (lib/apps.ts), gated by role as a UX
// gate only (the API policies are the boundary). A tile the role may not use is rendered
// locked, never hidden, and says so in words: colour + glyph + label, not grey alone.

export default function Home({
  role,
  onOpenApp,
}: {
  role: string | null;
  onOpenApp: (app: InternalApp) => void;
}) {
  const today = useToday();
  const email = getClaims()?.email || "—";
  const nothingUsable = APPS.every((a) => !canUseApp(a, role));

  return (
    <div style={{ flex: 1, minHeight: 0, overflowY: "auto" }} className="detailfade">
      <div style={{ maxWidth: 1180, margin: "0 auto", padding: "28px 32px 40px" }}>
        <PageHeader
          eyebrow={today.label}
          title="Choose an app"
          right={
            <span style={chipStyle(colors.inputBg, colors.border, colors.textSecondary)}>
              Signed in as {email} · {(role ?? "").toUpperCase() || "—"}
            </span>
          }
        />

        {nothingUsable && (
          // A valid session with no way out is a trap (the AccessDeniedScreen lesson), so this
          // offers one. All tiles still render below, locked, so the person can see what exists.
          <Panel style={{ marginTop: 20, display: "flex", alignItems: "center", gap: 16, borderColor: statusMeta("soon").bd }}>
            <StatusChip kind="soon" label="No apps for this role" />
            <div style={{ flex: 1, fontFamily: fonts.body, fontSize: 13, lineHeight: 1.5, color: colors.textSecondary }}>
              {role ? `Your role (${role}) has no console apps yet.` : "Your account has no role with console apps yet."} Ask the
              owner to change your role, or sign out.
            </div>
            <ActionButton onClick={() => void logout()}>SIGN OUT</ActionButton>
          </Panel>
        )}

        <div
          style={{
            marginTop: 22,
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(230px, 1fr))",
            gap: 16,
          }}
        >
          {APPS.map((app) => (
            <Tile key={app.id} app={app} role={role} onOpenApp={onOpenApp} />
          ))}
        </div>
      </div>
    </div>
  );
}

function Tile({
  app,
  role,
  onOpenApp,
}: {
  app: AppDef;
  role: string | null;
  onOpenApp: (app: InternalApp) => void;
}) {
  const allowed = canUseApp(app, role);
  const configured = app.kind === "internal" || app.href !== null;
  const enabled = allowed && configured;

  // Locked by role takes precedence over "not configured": the role is the thing this person
  // can do something about.
  const lock = !allowed
    ? { text: "Not available for your role", title: "Not available for your role" }
    : !configured
      ? { text: "Not configured", title: "Set NEXT_PUBLIC_BUDGETING_URL at build time" }
      : null;

  const meta = app.kind === "internal" ? app.screens.map((s) => s.label).join(" · ") : app.hint;
  const badge = app.kind === "internal" ? appBadge(app) : undefined;

  // Real <button>/<a> elements: keyboard access and focus for free. UA styles reset here; the
  // hover/focus/disabled rules live in app/globals.css under .nl-tile.
  const box: CSSProperties = {
    appearance: "none",
    width: "100%",
    minHeight: 156,
    padding: "18px 18px 16px",
    margin: 0,
    borderRadius: 14,
    background: colors.cardBg,
    border: `1px solid ${colors.borderSubtle}`,
    boxShadow: colors.shadowCard,
    display: "flex",
    flexDirection: "column",
    alignItems: "stretch",
    gap: 12,
    textAlign: "left",
    font: "inherit",
    color: colors.textPrimary,
    textDecoration: "none",
    cursor: enabled ? "pointer" : "not-allowed",
  };

  const body = (
    <>
      <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
        <span
          style={{
            width: 44,
            height: 44,
            flex: "none",
            borderRadius: 10,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontFamily: fonts.mono,
            fontSize: 15,
            fontWeight: 500,
            // Enabled = the rail's active pairing scaled up; locked = greyed, plus the chip below.
            background: enabled ? colors.amber : colors.cardBg,
            color: enabled ? colors.navy : colors.textDim,
            border: enabled ? undefined : `1px solid ${colors.border}`,
          }}
        >
          {app.code}
        </span>
        {badge && (
          <span
            title={`${badge} need attention`}
            aria-label={`${badge} need attention`}
            style={{
              marginLeft: "auto",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              minWidth: 20,
              height: 18,
              padding: "0 5px",
              borderRadius: 9,
              fontFamily: fonts.mono,
              fontSize: 10,
              background: "rgba(213,94,0,.12)",
              color: statusMeta("over").t,
              border: "1px solid rgba(213,94,0,.35)",
            }}
          >
            {badge}
          </span>
        )}
      </div>
      <div>
        <div style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 22, lineHeight: 1.1, color: colors.headingBright }}>
          {app.label}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, lineHeight: 1.45, color: colors.textMuted, marginTop: 4 }}>
          {app.tagline}
        </div>
      </div>
      <div
        style={{
          marginTop: "auto",
          fontFamily: fonts.semiCondensed,
          fontSize: 10.5,
          letterSpacing: ".08em",
          textTransform: "uppercase",
          lineHeight: 1.5,
          color: colors.textDim,
        }}
      >
        {meta}
      </div>
      {lock && (
        <div>
          <StatusChip kind="off" glyph="⊘" label={lock.text} />
        </div>
      )}
    </>
  );

  if (enabled && app.kind === "external") {
    return (
      <a className="nl-tile" href={app.href ?? undefined} target="_blank" rel="noopener noreferrer" title={app.hint} style={box}>
        {body}
      </a>
    );
  }

  return (
    <button
      type="button"
      className="nl-tile"
      disabled={!enabled}
      aria-disabled={!enabled || undefined}
      title={lock?.title}
      onClick={enabled && app.kind === "internal" ? () => onOpenApp(app) : undefined}
      style={box}
    >
      {body}
    </button>
  );
}
