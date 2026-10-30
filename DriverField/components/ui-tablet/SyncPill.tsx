"use client";

import { fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { radius, touch } from "@/lib/tablet";
import { useSyncState } from "@/lib/sync/useSyncState";
import type { SyncState } from "@/lib/sync/types";

// APP-LOCAL. The always-visible sync indicator, mandated by architecture §8: never let sync
// state be invisible — a driver must be able to tell "this is saved on the device" from "this
// has reached the server".
//
// It reads ONLY lib/sync/status.ts (through lib/sync/useSyncState.ts). The queue puts the
// counts there; this component only words them.
//
// WHAT IT SAYS, and why each line is deliberate:
//   "N failed — see Hours"        vermillion ▲   the server refused something; the Hours screen
//                                                has the message and the DISCARD. Shown first,
//                                                whatever else is true.
//   "Sync off · N held"           gray —         no kind has a sender (before sign-in).
//   "Offline · N held"            gold ◐         senders exist, no network; the queue waits.
//   "Sync · hours only · N held"  gold ◐         the two hos.* kinds reach the server, every
//                                                other kind is held. NEVER a green check while
//                                                any kind is parked — a pill that says "Synced"
//                                                over a DVIR that never left the device is how a
//                                                hard requirement quietly dies.
//   "Synced" / "Syncing N"        only once `enabled` is true, i.e. every kind has a sender.

export function SyncPill() {
  const state = useSyncState();
  const { kind, label } = describe(state);
  const m = statusMeta(kind);

  return (
    <div
      title={title(state)}
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

/** Exported for the test that pins the wording — the pill's honesty is the test's subject. */
export function describe(state: SyncState): { kind: StatusKind; label: string } {
  if (state.failed > 0) return { kind: "over", label: `${state.failed} failed — see Hours` };
  if (!state.enabled && state.enabledKinds.length === 0) {
    // "off" is the neutral gray + em-dash glyph — the palette's "unavailable" state, which is
    // exactly what this is. Not an error, not a success.
    return { kind: "off", label: `Sync off · ${state.pending} held` };
  }
  if (!state.online) return { kind: "soon", label: `Offline · ${state.pending} held` };
  if (!state.enabled) return { kind: "soon", label: `Sync · ${scope(state)} · ${state.pending} held` };
  if (state.pending > 0) return { kind: "soon", label: `Syncing ${state.pending}` };
  return { kind: "ontime", label: "Synced" };
}

/** "hours only" while every live kind is an hos.* one; anything wider says so rather than lying. */
function scope(state: SyncState): string {
  return state.enabledKinds.every((k) => k.startsWith("hos.")) ? "hours only" : "partial";
}

function title(state: SyncState): string {
  if (state.enabled) return `Last synced ${state.lastSyncedAt ?? "never"}`;
  if (state.enabledKinds.length === 0) return "Sync is not running — nothing on this screen has reached the server.";
  return `Only ${state.enabledKinds.join(", ")} reach the server; everything else is held on this device. Last sent ${state.lastSyncedAt ?? "never"}.`;
}
