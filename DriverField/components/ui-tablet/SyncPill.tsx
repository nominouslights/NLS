"use client";

import { useSyncExternalStore } from "react";
import { fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { radius, touch } from "@/lib/tablet";
import { getServerSyncState, getSyncState, subscribe } from "@/lib/sync/status";
import type { SyncState } from "@/lib/sync/types";

// APP-LOCAL. The always-visible sync indicator, mandated by architecture §8: never let sync
// state be invisible — a driver must be able to tell "this is saved on the device" from "this
// has reached the server".
//
// It reads ONLY lib/sync/status.ts, which is what lets it ship in this scaffold and never
// change again: the offline batch puts real counts behind getSyncState() and this component
// starts telling the truth without being touched.
//
// WHAT IT SAYS TODAY, and why that wording is deliberate: sync is not implemented, so the pill
// says "SYNC OFF" in neutral gray, never a green check. A scaffold that renders a reassuring
// "Synced" while syncing nothing is precisely how a hard requirement quietly dies between a
// demo and a deployment.

export function SyncPill() {
  // useSyncExternalStore rather than useState + useEffect: the sync state lives outside React
  // (lib/sync/status.ts, driven by the queue and by network events), and this is the hook built
  // for exactly that. It also keeps the server render and the first client render agreeing,
  // which matters because `navigator.onLine` does not exist during SSR.
  const state = useSyncExternalStore<SyncState>(subscribe, getSyncState, getServerSyncState);

  const { kind, label } = describe(state);
  const m = statusMeta(kind);

  return (
    <div
      title={
        state.enabled
          ? `Last synced ${state.lastSyncedAt ?? "never"}`
          : "Offline sync is not implemented yet — nothing on this screen has reached the server."
      }
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 8,
        minHeight: touch.min,
        padding: "0 14px",
        borderRadius: radius.control,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        color: m.t,
        fontFamily: fonts.semiCondensed,
        fontSize: 14,
        fontWeight: 600,
        letterSpacing: ".08em",
        textTransform: "uppercase",
        whiteSpace: "nowrap",
      }}
    >
      <span aria-hidden>{m.g}</span>
      {label}
    </div>
  );
}

function describe(state: SyncState): { kind: StatusKind; label: string } {
  if (!state.enabled) {
    // "off" is the neutral gray + em-dash glyph — the palette's "unavailable" state, which is
    // exactly what this is. Not an error, not a success.
    return { kind: "off", label: `Sync off · ${state.pending} held` };
  }
  if (!state.online) return { kind: "soon", label: `Offline · ${state.pending} queued` };
  if (state.failed > 0) return { kind: "over", label: `${state.failed} failed` };
  if (state.pending > 0) return { kind: "soon", label: `Syncing ${state.pending}` };
  return { kind: "ontime", label: "Synced" };
}
