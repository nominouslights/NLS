"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import type { EmailRecipientResult } from "@/lib/api/notifications";
import { StatusChip } from "@/components/ui/Chip";

// Shared by every send-email modal (pickup, accruals, booking passes): the
// per-recipient outcome line and the sent-at timestamp format. Lifted here
// rather than copied a third time — behaviour identical to the originals.

/** "Sep 15, 09:42 a.m." — local rendering of a UTC sent-at timestamp. */
export function fmtUtcDateTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("en-CA", {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  });
}

/** Per-recipient outcome line — colour + glyph + label, never colour alone.
 *  passengerName carries the passenger's name on pickup dispatches and the
 *  CONTACT's display name on accruals / booking-pass dispatches. */
export function RecipientOutcomeRow({ r }: { r: EmailRecipientResult }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
      <StatusChip kind={r.status === "Sent" ? "ontime" : "over"} label={r.status === "Sent" ? "Sent" : "Failed"} />
      <span style={{ fontFamily: fonts.body, fontSize: 12, fontWeight: 500, color: colors.textSecondary }}>
        {r.passengerName}
      </span>
      <span style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>{r.email}</span>
      {r.errorMessage && (
        <span style={{ fontFamily: fonts.body, fontSize: 11, color: statusMeta("over").t, flexBasis: "100%" }}>
          {r.errorCode ? `${r.errorCode} · ` : ""}
          {r.errorMessage}
        </span>
      )}
    </div>
  );
}
