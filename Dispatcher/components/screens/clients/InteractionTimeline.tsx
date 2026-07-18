"use client";

import { useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { formatUtcDate } from "@/lib/api";
import { contactsFor, timelineFor, useClientStore } from "@/lib/clientStore";
import { docStatusFor } from "@/lib/maintenanceStore";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import InteractionModal from "@/components/InteractionModal";
import { followUpLabel, InteractionTypeChip } from "./shared";

// Chronological interaction / touchpoint log for a client, newest first, with
// the ability to log a new interaction. Mock (lib/clientStore).

export default function InteractionTimeline({ clientId, clientName }: { clientId: number; clientName: string }) {
  useClientStore();
  const [open, setOpen] = useState(false);

  const rows = timelineFor(clientId);
  const nameById = new Map(contactsFor(clientId).map((c) => [c.id, c.name]));

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>
          Interaction log · {rows.length} touchpoint{rows.length === 1 ? "" : "s"}
        </SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setOpen(true)}>
          + LOG INTERACTION
        </ActionButton>
      </div>

      {rows.length === 0 ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No interactions logged for {clientName} yet.
        </div>
      ) : (
        rows.map((ix) => {
          const names = ix.participantContactIds.map((id) => nameById.get(id) ?? "Unknown").join(", ");
          return (
            <Panel key={ix.id} style={{ marginBottom: 10 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 9, flexWrap: "wrap" }}>
                <InteractionTypeChip type={ix.type} />
                <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                  {formatUtcDate(ix.date)}
                </span>
                {ix.followUpDate && (
                  <span style={{ marginLeft: "auto" }}>
                    <StatusChip kind={docStatusFor(ix.followUpDate)} label={`Follow-up ${followUpLabel(docStatusFor(ix.followUpDate))}`} />
                  </span>
                )}
              </div>

              <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textSecondary, lineHeight: 1.55 }}>
                {ix.summary}
              </div>

              {names && (
                <div style={{ display: "grid", gridTemplateColumns: "84px 1fr", gap: 10, marginTop: 10 }}>
                  <span
                    style={{
                      fontFamily: fonts.semiCondensed,
                      fontSize: 10,
                      letterSpacing: ".08em",
                      textTransform: "uppercase",
                      color: colors.textLabel,
                      paddingTop: 1,
                    }}
                  >
                    With
                  </span>
                  <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>{names}</span>
                </div>
              )}

              {ix.followUpDate && (
                <div
                  style={{
                    marginTop: 11,
                    padding: "10px 12px",
                    background: colors.inputBg,
                    border: `1px solid ${colors.borderSubtle}`,
                    borderRadius: 9,
                    display: "flex",
                    gap: 10,
                    alignItems: "flex-start",
                  }}
                >
                  <span style={{ color: colors.skyBlue, fontWeight: 800, fontSize: 12 }}>↪</span>
                  <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.5 }}>
                    <span style={{ fontWeight: 600, color: colors.textPrimary }}>
                      Follow-up {formatUtcDate(ix.followUpDate)}
                    </span>
                    {ix.followUpNote ? ` — ${ix.followUpNote}` : ""}
                  </div>
                </div>
              )}
            </Panel>
          );
        })
      )}

      {open && (
        <InteractionModal clientId={clientId} clientName={clientName} onClose={() => setOpen(false)} onSaved={() => undefined} />
      )}
    </div>
  );
}
