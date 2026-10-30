"use client";

import { useSyncExternalStore } from "react";
import { getServerSyncState, getSyncState, subscribe } from "./status";
import type { SyncState } from "./types";

// The one React binding for lib/sync/status.ts — the shape every store in this app uses
// (lib/hos/useHosLedger.ts, lib/useInspectionStore.ts): useSyncExternalStore, never
// useEffect + setState, for the lint and hydration reasons spelled out in those files.
// Two surfaces read it: the SyncPill, and the Hours screen's parked-command list, which
// re-renders when the failed count moves.

export function useSyncState(): SyncState {
  return useSyncExternalStore<SyncState>(subscribe, getSyncState, getServerSyncState);
}
