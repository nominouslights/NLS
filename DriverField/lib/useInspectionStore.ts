"use client";

import { useSyncExternalStore } from "react";
import { getServerSnapshot, getSnapshot, subscribe } from "./inspectionStore";

// The one React binding for lib/inspectionStore.ts, which is otherwise React-free.
//
// Four surfaces need it — Console (the rail badge), Inspection (the wizard), Today and Manifest
// (the blocking banner) — so it lives here rather than being re-typed in each.
//
// WHY useSyncExternalStore AND NOT useEffect + setState:
//
//   1. LINT. `react-hooks/set-state-in-effect` is one of the three inherited errors in the
//      copied lib/useToday.ts. Those three plus two warnings are the bar for this app; a fourth
//      authored here would break it, and the fix would belong in Dispatcher.
//   2. HYDRATION. The server has no localStorage. getServerSnapshot() returns `null`, and React
//      uses that value for the server render AND the hydrating client render before re-reading
//      getSnapshot(). So a caller writes
//
//        const hydrated = useInspectionStoreHydrated();
//        const draft = hydrated ? getDraft(mode, vehicleId) : null;
//
//      and the localStorage read simply cannot happen while the markup still has to match the
//      server's. No suppressHydrationWarning, no mounted flag, no effect.
//
// Same shape as lib/sync/status.ts + components/ui-tablet/SyncPill.tsx:27, which is the
// established in-repo pattern for exactly this.

/**
 * True once the client has hydrated and the store may be read. Re-renders the caller whenever
 * the draft or the local certifications change.
 */
export function useInspectionStoreHydrated(): boolean {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot) !== null;
}
