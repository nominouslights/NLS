"use client";

import { colors, fonts } from "@/lib/theme";
import { formatUtcDate } from "@/lib/api";
import { credentialKindFor, type DriverClearanceRecord } from "@/lib/api/drivers";
import { StatusChip } from "@/components/ui/Chip";
import { credExpiryLabel, RemoveButton } from "./chips";

// One client-clearance row on the Clearances tab — extracted verbatim from
// Drivers.tsx.

export function ClearanceRow({
  c,
  busy,
  onRemove,
}: {
  c: DriverClearanceRecord;
  busy: boolean;
  onRemove: () => void;
}) {
  const kind = credentialKindFor(c.expiry);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 12,
        padding: "11px 0",
        borderTop: `1px solid ${colors.borderSubtle}`,
      }}
    >
      <div style={{ minWidth: 0 }}>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: 9.5,
            letterSpacing: ".1em",
            textTransform: "uppercase",
            color: colors.textLabel,
            marginBottom: 3,
          }}
        >
          {c.clientName}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
          {c.title}
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, marginTop: 2 }}>
          Granted {formatUtcDate(c.grantedAtUtc)}
        </div>
      </div>
      <div style={{ flex: "none", textAlign: "right" }}>
        <StatusChip kind={kind} label={credExpiryLabel(kind, c.expiry)} />
        <div>
          <RemoveButton onClick={onRemove} disabled={busy} />
        </div>
      </div>
    </div>
  );
}
