"use client";

// ---------------------------------------------------------------------------
// Survive-unmount state for the console's apps.
//
// Only one app is mounted at a time, and returning Home unmounts it. A dispatcher who stepped
// the Trips list back three months, or is mid-shipment in Cargo, should not lose that on a
// detour to another app — the same contract Console used to keep with sixteen hoisted
// useStates. Those now live here, keyed by the app that owns them, in a module-level Map that
// outlives any component (the same survive-unmount idea as lib/driverStore.ts and friends,
// minus the cross-component reactivity, which nothing needs: keys are app-owned and only one
// app is mounted).
//
// The Map is written inside the setter rather than in an effect, so a value set on the render
// before an unmount is never lost. Console clears the whole Map on its own unmount (sign-out),
// so the next account inherits nothing.
// ---------------------------------------------------------------------------

import { useCallback, useState, type SetStateAction } from "react";

const retained = new Map<string, unknown>();

/**
 * Like useState, but the value is read back from the module Map on mount and every write is
 * mirrored into it. Same setter contract as React's: a value or a functional update, with a
 * stable identity across renders.
 */
export function useRetainedState<T>(
  key: string,
  initial: T | (() => T),
): readonly [T, (next: SetStateAction<T>) => void] {
  const [value, setValue] = useState<T>(() => {
    if (retained.has(key)) return retained.get(key) as T;
    return typeof initial === "function" ? (initial as () => T)() : initial;
  });

  const set = useCallback(
    (next: SetStateAction<T>) => {
      setValue((prev) => {
        const resolved = typeof next === "function" ? (next as (p: T) => T)(prev) : next;
        // Idempotent, so StrictMode's double-invoked updater is harmless.
        retained.set(key, resolved);
        return resolved;
      });
    },
    [key],
  );

  return [value, set] as const;
}

/** Forgets every retained value. Console calls this on unmount — i.e. on sign-out. */
export function clearRetainedState(): void {
  retained.clear();
}
