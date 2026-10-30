"use client";

import { colors, fonts } from "@/lib/theme";
import type { OutOfServiceRecord } from "@/lib/api/hos";
import { formatDateTimeUtc } from "@/lib/hos/display";
import { Panel } from "@/components/ui/Panel";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";

// The declaration in force (HosStatusResponse.outOfService), shown above every tab of the
// driver detail: vermillion + ✕ + the sentence, so it is never missed and never colour
// alone. LIFT is compliance-only (client gate; the server's ComplianceAccess is the real one).

/** "Out of service until Oct 12, 14:00 — reason" / "… until lifted — reason". */
export function outOfServiceBannerText(oos: OutOfServiceRecord): string {
  const until = oos.untilUtc ? formatDateTimeUtc(oos.untilUtc) : "lifted";
  return `Out of service until ${until} — ${oos.reason}`;
}

export function OutOfServiceBanner({ outOfService, canCompliance, onLift }: { outOfService: OutOfServiceRecord; canCompliance: boolean; onLift: () => void }) {
  return (
    <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
        <StatusChip kind="over" glyph="✕" label={outOfServiceBannerText(outOfService)} />
        <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
          Declared {formatDateTimeUtc(outOfService.fromUtc)} · the driver may not drive or use personal conveyance; off and on duty are allowed.
        </span>
        {canCompliance && (
          <ActionButton style={{ marginLeft: "auto" }} onClick={onLift}>
            LIFT
          </ActionButton>
        )}
      </div>
    </Panel>
  );
}
