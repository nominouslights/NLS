// ---------------------------------------------------------------------------
// The offline seam — sync status.
//
// components/ui-tablet/SyncPill.tsx reads ONLY this module. The queue (lib/sync/queue.ts)
// writes the counts after every mutation and drain, and the senders it has registered; the
// network events write `online`. Nothing else writes here.
//
// `enabled` stays false until EVERY command kind has a sender, which is not this build: only
// the two hos.* kinds are live. The pill renders that partial truth from `enabledKinds` and
// never shows a green check while anything is held — a scaffold that renders a reassuring
// "Synced" while syncing nothing is exactly how a hard requirement quietly dies.
//
// The snapshot is CACHED and only replaced when something actually changes. That is not a
// micro-optimisation: SyncPill reads it through useSyncExternalStore, which compares snapshots
// by identity and re-renders forever if every call returns a fresh object.
// ---------------------------------------------------------------------------

import type { CommandKind, SyncState } from "./types";

type SyncListener = (state: SyncState) => void;
const listeners = new Set<SyncListener>();

const INITIAL: SyncState = {
  // Optimistic until the browser tells us otherwise. This value is also the server snapshot,
  // so it must not read `navigator` — it is evaluated during SSR.
  online: true,
  pending: 0,
  failed: 0,
  lastSyncedAt: null,
  enabled: false,
  enabledKinds: [],
};

let snapshot: SyncState = INITIAL;

/** The stable server-render snapshot. Never varies, so hydration cannot mismatch. */
const serverSnapshot: SyncState = INITIAL;

function publish(next: SyncState): void {
  const changed =
    next.online !== snapshot.online ||
    next.pending !== snapshot.pending ||
    next.failed !== snapshot.failed ||
    next.lastSyncedAt !== snapshot.lastSyncedAt ||
    next.enabled !== snapshot.enabled ||
    next.enabledKinds.join("|") !== snapshot.enabledKinds.join("|");
  if (!changed) return;

  snapshot = next;
  for (const listener of [...listeners]) listener(snapshot);
}

/** Called by the queue after every mutation and every drain. */
export function setCounts(counts: { pending: number; failed: number; lastSyncedAt?: string | null }): void {
  publish({
    ...snapshot,
    pending: counts.pending,
    failed: counts.failed,
    lastSyncedAt: counts.lastSyncedAt === undefined ? snapshot.lastSyncedAt : counts.lastSyncedAt,
  });
}

/** Called by the queue when a sender is registered or removed. `enabled` is derived, never set. */
export function setEnabledKinds(kinds: CommandKind[], allKinds: readonly CommandKind[]): void {
  const sorted = [...kinds].sort();
  const enabled = sorted.length > 0 && allKinds.every((k) => sorted.includes(k));
  publish({ ...snapshot, enabledKinds: sorted, enabled });
}

/** Re-reads whatever the platform can tell us. Safe to call repeatedly. */
export function refreshSyncState(): void {
  // navigator.onLine only reports whether the device has A network — it says nothing about
  // whether the API is reachable across it, which on this corridor is a different question.
  // Good enough to drive a pill; never good enough to gate a write.
  const online = typeof navigator === "undefined" ? true : navigator.onLine;
  publish({ ...snapshot, online });
}

export function getSyncState(): SyncState {
  return snapshot;
}

export function getServerSyncState(): SyncState {
  return serverSnapshot;
}

/** Subscribe to sync-state changes. Returns an unsubscribe function. */
export function subscribe(listener: SyncListener): () => void {
  listeners.add(listener);

  const onNetworkChange = () => refreshSyncState();
  if (typeof window !== "undefined") {
    window.addEventListener("online", onNetworkChange);
    window.addEventListener("offline", onNetworkChange);
    refreshSyncState();
  }

  return () => {
    listeners.delete(listener);
    if (typeof window !== "undefined") {
      window.removeEventListener("online", onNetworkChange);
      window.removeEventListener("offline", onNetworkChange);
    }
  };
}

/** Test seam: back to the initial snapshot. */
export function resetSyncStateForTests(): void {
  publish(INITIAL);
}
