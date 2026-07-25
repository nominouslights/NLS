"use client";

import { colors, fonts, statusMeta } from "@/lib/theme";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { errBadge } from "./vehicle-detail/shared";

// The pre-data states for the Fleet & Maintenance screen — rendered inside the
// screen frame by Fleet.tsx (which owns the header and any modals).

export function FleetLoadError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div style={{ padding: "26px", maxWidth: 560 }}>
      <Panel borderColor="rgba(213,94,0,.4)">
        <div style={{ display: "flex", gap: 11, alignItems: "center", marginBottom: 12 }}>
          <span style={errBadge}>▲</span>
          <span style={{ fontFamily: fonts.body, fontSize: 13.5, fontWeight: 600, color: statusMeta("over").t }}>
            Fleet registry unavailable
          </span>
        </div>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
          {message}
        </div>
        <ActionButton variant="primary" onClick={onRetry}>
          RETRY
        </ActionButton>
      </Panel>
    </div>
  );
}

export function FleetLoadingSkeleton() {
  return (
    <div style={{ padding: "16px 26px" }}>
      {[0, 1, 2, 3, 4].map((i) => (
        <div
          key={i}
          style={{
            height: 62,
            borderRadius: 9,
            border: `1px solid ${colors.borderSubtle}`,
            background: colors.cardBg,
            marginBottom: 6,
            opacity: 0.55 - i * 0.08,
          }}
        />
      ))}
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, marginTop: 10 }}>
        Loading fleet from API…
      </div>
    </div>
  );
}

export function FleetEmpty({ onRegister }: { onRegister: () => void }) {
  return (
    <div style={{ padding: "26px", maxWidth: 560 }}>
      <Panel>
        <SectionLabel>No vehicles registered</SectionLabel>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.6, marginBottom: 14 }}>
          The vehicle registry is empty for this tenant. Register the first vehicle to make it
          available for trips, documents, service history, work orders, and DVIRs.
        </div>
        <ActionButton variant="primary" onClick={onRegister}>
          + REGISTER VEHICLE
        </ActionButton>
      </Panel>
    </div>
  );
}
