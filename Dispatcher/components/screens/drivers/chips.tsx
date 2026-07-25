"use client";

import { chipStyle, colors, dutyMeta, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { credentialKindFor } from "@/lib/api/drivers";
import type { DutyStatus } from "@/lib/types";

// Small chips/labels shared by the Drivers & Compliance screen and its row
// components (screens/drivers/*) — extracted verbatim from Drivers.tsx.

// Duty status as color + icon + label (never colour alone).
export function DutyChip({ status }: { status: DutyStatus }) {
  const m = dutyMeta(status);
  return (
    <span style={chipStyle(m.bg, m.bd, m.text)}>
      <span style={{ fontSize: 10, lineHeight: 1, color: m.color }}>{m.glyph}</span>
      {status}
    </span>
  );
}

// Small "credential expiring / expired" flag for a roster row, derived from
// the API's soonestCredentialExpiry rollup.
export function ExpiryFlag({ soonestExpiry }: { soonestExpiry: string | null }) {
  const kind = credentialKindFor(soonestExpiry);
  if (kind !== "soon" && kind !== "over") return null;
  const m = statusMeta(kind);
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        fontFamily: fonts.body,
        fontWeight: 600,
        fontSize: 10,
        padding: "1px 6px",
        borderRadius: 6,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        color: m.t,
      }}
    >
      <span style={{ fontSize: 9, lineHeight: 1 }}>{m.g}</span>
      {kind === "over" ? "Credential expired" : "Credential expiring"}
    </span>
  );
}

export function PermitTag() {
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        fontFamily: fonts.semiCondensed,
        fontWeight: 600,
        fontSize: 9.5,
        letterSpacing: ".06em",
        textTransform: "uppercase",
        padding: "1px 6px",
        borderRadius: 6,
        background: "rgba(232,160,32,.13)",
        border: "1px solid rgba(232,160,32,.5)",
        color: colors.amberText,
      }}
    >
      Work permit
    </span>
  );
}

export function credExpiryLabel(kind: StatusKind, expiry: string | null): string {
  if (!expiry) return "No expiry";
  if (kind === "over") return `Expired ${expiry}`;
  if (kind === "soon") return `Expires ${expiry}`;
  return `Valid to ${expiry}`;
}

export function RemoveButton({ onClick, disabled }: { onClick: () => void; disabled: boolean }) {
  return (
    <span
      onClick={disabled ? undefined : onClick}
      style={{
        fontFamily: fonts.semiCondensed,
        fontWeight: 600,
        fontSize: 10,
        letterSpacing: ".08em",
        textTransform: "uppercase",
        color: disabled ? colors.textFaint : statusMeta("over").t,
        cursor: disabled ? "wait" : "pointer",
        marginTop: 5,
        display: "inline-block",
      }}
    >
      Remove
    </span>
  );
}
