"use client";

import { useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError, formatUtcDate } from "@/lib/api";
import {
  listContacts,
  listInteractions,
  type ClientContactRecord,
  type ClientInteractionRecord,
} from "@/lib/api/clients";
import { docStatusFor } from "@/lib/maintenanceStore";
import { Panel, SectionLabel } from "@/components/ui/Panel";
import { StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import InteractionModal from "@/components/InteractionModal";
import { followUpLabel, InteractionTypeChip } from "./shared";

// Chronological interaction / touchpoint log for a client, newest first, with
// the ability to log a new interaction. Real Clients API (interactions + the
// contact roster for participant names), keyed by the real client Guid.

export default function InteractionTimeline({
  clientId,
  clientName,
}: {
  clientId: string;
  clientName: string;
}) {
  const [rows, setRows] = useState<ClientInteractionRecord[] | null>(null);
  const [contacts, setContacts] = useState<ClientContactRecord[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    // Fetch on mount; setState only inside the async callbacks, with a mounted
    // guard (same convention as ContactRoster / Drivers).
    let active = true;
    Promise.all([listInteractions(clientId), listContacts(clientId)]).then(
      ([timeline, roster]) => {
        if (active) {
          setRows(timeline);
          setContacts(roster);
          setLoadError(null);
        }
      },
      (e) => {
        if (active) {
          setLoadError(e instanceof ApiError ? e.message : "Failed to load interactions.");
          setRows([]);
        }
      },
    );
    return () => {
      active = false;
    };
  }, [clientId]);

  async function onSaved() {
    // Refetch the timeline after logging an interaction.
    try {
      const [timeline, roster] = await Promise.all([
        listInteractions(clientId),
        listContacts(clientId),
      ]);
      setRows(timeline);
      setContacts(roster);
      setLoadError(null);
    } catch (e) {
      setLoadError(e instanceof ApiError ? e.message : "Failed to reload interactions.");
    }
  }

  const nameById = new Map(contacts.map((c) => [c.id, c.name]));
  const timeline = rows ?? [];

  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", marginBottom: 14 }}>
        <SectionLabel>
          Interaction log · {timeline.length} touchpoint{timeline.length === 1 ? "" : "s"}
        </SectionLabel>
        <ActionButton variant="primary" style={{ marginLeft: "auto" }} onClick={() => setOpen(true)}>
          + LOG INTERACTION
        </ActionButton>
      </div>

      {loadError && (
        <div style={{ marginBottom: 12 }}>
          <StatusChip kind="over" label={`Interactions unavailable — ${loadError}`} />
        </div>
      )}

      {rows === null && !loadError ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          Loading interactions…
        </div>
      ) : timeline.length === 0 && !loadError ? (
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim, padding: "6px 2px" }}>
          No interactions logged for {clientName} yet.
        </div>
      ) : (
        timeline.map((ix) => {
          const names = ix.participantContactIds.map((id) => nameById.get(id) ?? "Unknown").join(", ");
          return (
            <Panel key={ix.id} style={{ marginBottom: 10 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 9, flexWrap: "wrap" }}>
                <InteractionTypeChip type={ix.type} />
                <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim }}>
                  {formatUtcDate(ix.occurredOn)}
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
        <InteractionModal clientId={clientId} clientName={clientName} onClose={() => setOpen(false)} onSaved={onSaved} />
      )}
    </div>
  );
}
