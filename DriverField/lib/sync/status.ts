// ---------------------------------------------------------------------------
// The offline seam — sync status. NO-OP IMPLEMENTATION.
//
// components/ui-tablet/SyncPill.tsx reads ONLY this module, so the pill ships in this scaffold
// and never has to change again: the offline batch replaces the counts behind getSyncState()
// and the same component starts telling the truth.
//
// `enabled` is false for the whole of this scaffold, and the pill says so out loud. A scaffold
// that renders a green "Synced" check while syncing nothing is exactly how a hard requirement
// quietly dies.
//
// The snapshot is CACHED and only replaced when something actually changes. That is not a
// micro-optimisation: SyncPill reads it through useSyncExternalStore, which compares snapshots
// by identity and re-renders forever if every call returns a fresh object.
// ---------------------------------------------------------------------------

import type { SyncState } from "./types";

type SyncListener = (state: SyncState) => void;
const listeners = new Set<SyncListener>();

let snapshot: SyncState = {
  // Optimistic until the browser tells us otherwise. This value is also the server snapshot,
  // so it must not read `navigator` — it is evaluated during SSR.
  online: true,
  pending: 0,
  failed: 0,
  lastSyncedAt: null,
  enabled: false,
};

/** The stable server-render snapshot. Never varies, so hydration cannot mismatch. */
const serverSnapshot: SyncState = snapshot;

function publish(next: SyncState): void {
  const changed =
    next.online !== snapshot.online ||
    next.pending !== snapshot.pending ||
    next.failed !== snapshot.failed ||
    next.lastSyncedAt !== snapshot.lastSyncedAt ||
    next.enabled !== snapshot.enabled;
  if (!changed) return;

  snapshot = next;
  for (const listener of [...listeners]) listener(snapshot);
}

/** Called by queue.enqueue so the pill's count moves even while sync is disabled. */
export function bumpPending(count: number): void {
  publish({ ...snapshot, pending: count });
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
