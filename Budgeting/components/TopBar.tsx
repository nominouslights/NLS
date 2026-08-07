"use client";

import { colors, fonts } from "@/lib/theme";
import { getClaims, logout } from "@/lib/auth";
import HeaderClock from "@/components/HeaderClock";

// Adapted from Dispatcher/components/TopBar.tsx. Geometry, colours and spacing are unchanged —
// the 56px bar, the wordmark, the 34px search field with its ⌘K tag, the clock, the bell, the
// blue pill and the avatar block are what make the two consoles read as one product.
//
// Three differences: the pill creates an allocation rather than a trip, the search placeholder
// names budgeting things, and the identity block shows the real signed-in user instead of
// Dispatcher's hardcoded "R. Kelsey / Owner · Dispatcher".

/**
 * Two initials from an email's local part: "l.fontaine@…" and "l_fontaine@…" both give "LF",
 * "owner@…" gives "OW". lib/format.ts's initials() expects a display name ("R. Kelsey"), and
 * the token carries no name claim — only an email.
 */
function emailInitials(email: string): string {
  const local = email.split("@")[0] ?? "";
  const parts = local.split(/[._-]+/).filter(Boolean);
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return "?";
}

export default function TopBar({
  onToggleRail,
  onNewAllocation,
}: {
  onToggleRail: () => void;
  onNewAllocation: () => void;
}) {
  const claims = getClaims();
  const email = claims?.email ?? "";
  const role = claims?.role ?? "";

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 14,
        height: 56,
        flex: "none",
        padding: "0 16px",
        background: colors.topbarBg,
        borderBottom: `1px solid ${colors.border}`,
        boxShadow: colors.shadowCard,
        zIndex: 20,
      }}
    >
      <div
        onClick={onToggleRail}
        style={{
          width: 30,
          height: 30,
          flex: "none",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          border: `1px solid ${colors.borderStrong}`,
          borderRadius: 6,
          cursor: "pointer",
          color: colors.textLabel,
        }}
        title="Collapse rail"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: 3 }}>
          <span style={{ width: 14, height: 1.5, background: "currentColor", display: "block" }} />
          <span style={{ width: 14, height: 1.5, background: "currentColor", display: "block" }} />
          <span style={{ width: 14, height: 1.5, background: "currentColor", display: "block" }} />
        </div>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: 2, flex: "none" }}>
        <span style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 19, letterSpacing: ".02em", color: colors.headingBright }}>
          NORTHERN
        </span>
        <span style={{ fontFamily: fonts.condensed, fontWeight: 700, fontSize: 19, letterSpacing: ".02em", color: colors.amberText }}>
          LINK
        </span>
      </div>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 9,
          flex: 1,
          maxWidth: 420,
          height: 34,
          padding: "0 12px",
          borderRadius: 8,
          background: colors.inputBg,
          border: `1px solid ${colors.borderStrong}`,
          color: colors.textDim,
          cursor: "text",
          minWidth: 0,
        }}
      >
        <span style={{ fontSize: 14 }}>⌕</span>
        <span
          style={{
            fontFamily: fonts.body,
            fontSize: 13.5,
            flex: 1,
            minWidth: 0,
            overflow: "hidden",
            whiteSpace: "nowrap",
            textOverflow: "ellipsis",
          }}
        >
          Search budget codes, allocations, periods…
        </span>
        <span
          style={{
            fontFamily: fonts.mono,
            fontSize: 10,
            padding: "2px 6px",
            border: `1px solid ${colors.borderStrong}`,
            borderRadius: 4,
            color: colors.textDim,
            flex: "none",
          }}
        >
          ⌘K
        </span>
      </div>
      <div style={{ flex: 1 }} />
      <HeaderClock />
      <div
        style={{
          position: "relative",
          width: 34,
          height: 34,
          flex: "none",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          border: `1px solid ${colors.borderStrong}`,
          borderRadius: 8,
          cursor: "pointer",
          color: colors.textMuted,
        }}
      >
        <span style={{ fontSize: 16 }}>◔</span>
        <span
          style={{
            position: "absolute",
            top: -5,
            right: -5,
            minWidth: 17,
            height: 17,
            padding: "0 4px",
            borderRadius: 9,
            background: "#D55E00",
            color: "#fff",
            fontFamily: fonts.mono,
            fontSize: 10,
            fontWeight: 500,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            border: `1.5px solid ${colors.topbarBg}`,
          }}
        >
          3
        </span>
      </div>
      <div
        onClick={onNewAllocation}
        style={{
          display: "flex",
          alignItems: "center",
          gap: 7,
          flex: "none",
          padding: "8px 15px",
          borderRadius: 8,
          background: colors.blue,
          color: "#FFFFFF",
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 14,
          letterSpacing: ".04em",
          cursor: "pointer",
        }}
      >
        <span style={{ fontSize: 15, lineHeight: 1 }}>+</span> NEW ALLOCATION
      </div>
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 9,
          flex: "none",
          paddingLeft: 6,
          borderLeft: `1px solid ${colors.border}`,
          cursor: "pointer",
        }}
      >
        <div
          style={{
            width: 34,
            height: 34,
            borderRadius: 8,
            background: "linear-gradient(135deg,#E3EEF9,#CFE2F3)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontFamily: fonts.condensed,
            fontWeight: 700,
            fontSize: 14,
            color: colors.blue,
            flex: "none",
          }}
        >
          {emailInitials(email)}
        </div>
        <div style={{ lineHeight: 1.15 }}>
          <div
            style={{
              fontFamily: fonts.body,
              fontWeight: 600,
              fontSize: 12.5,
              color: colors.textPrimary,
              maxWidth: 180,
              overflow: "hidden",
              whiteSpace: "nowrap",
              textOverflow: "ellipsis",
            }}
          >
            {email || "—"}
          </div>
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontSize: 10,
              letterSpacing: ".08em",
              color: colors.textDim,
              textTransform: "uppercase",
            }}
          >
            {role || "—"}
          </div>
        </div>
        <div
          onClick={() => void logout()}
          title="Sign out"
          aria-label="Sign out"
          style={{
            display: "flex",
            alignItems: "center",
            gap: 6,
            flex: "none",
            padding: "5px 11px",
            border: `1px solid ${colors.borderStrong}`,
            borderRadius: 8,
            cursor: "pointer",
            color: colors.textSecondary,
            fontFamily: fonts.semiCondensed,
            fontSize: 11,
            letterSpacing: ".08em",
          }}
        >
          SIGN OUT
        </div>
      </div>
    </div>
  );
}
