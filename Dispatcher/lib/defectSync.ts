"use client";

import { useEffect, useRef } from "react";

// One defect, many surfaces. A defect lives in exactly one place — its Fleet
// inspection record, addressed by (inspectionId, item) — but the console shows
// it on the trip detail's inspection list, the trip detail's Open Defects panel,
// the vehicle's Open Defects and Inspections tabs, and the inspection detail.
// When any of them changes a defect's state (resolve, attach to a work order,
// complete that work order, enter / amend / remove an inspection), every other
// surface mounted at the same time must hear about it, or it keeps showing the
// defect as it was. This is that seam: a bare browser event, no new dependency.
//
// WARN ONLY, like DefectsPanel. The event carries NOTHING — no vehicle, no
// count, no "this vehicle has defects" signal — it only says "defect state
// changed somewhere, re-read yours". Nothing may branch trip creation, vehicle
// assignment or any other action on it; a listener's only job is to refetch.

/** Read models are projector-maintained on a 5s poll (Backend
 *  Shared/Persistence/Projections/ProjectionOptions.cs), so a defect changed a
 *  moment ago still reads back as it was. Re-read once the projector has
 *  certainly run — do NOT "fix" this into a tight poll; it would hammer the API
 *  to win a second. */
export const PROJECTION_RECONCILE_MS = 6000;

/** Namespaced so it cannot collide with anything else on `window`. */
export const DEFECTS_CHANGED_EVENT = "nl:defects-changed";

/** Announce that a defect changed. Call once, at the single success point of
 *  the write — never on failure, never twice for one write. SSR-safe. */
export function notifyDefectsChanged(): void {
  if (typeof window === "undefined") return;
  window.dispatchEvent(new CustomEvent(DEFECTS_CHANGED_EVENT));
}

/**
 * Run `callback` once, `PROJECTION_RECONCILE_MS` after the LAST defect change
 * in a burst (trailing debounce — one timer, reset by each event).
 *
 * Deliberately never fires immediately: an immediate refetch reads the stale
 * projection and would resurrect a row a surface has just dropped
 * optimistically. The callback is read through a ref, so callers need not
 * memoize it and it always sees their latest props/state when it fires.
 */
export function useDefectsChanged(callback: () => void): void {
  const latest = useRef(callback);
  useEffect(() => {
    latest.current = callback;
  });

  useEffect(() => {
    let timer: ReturnType<typeof setTimeout> | null = null;
    const onChanged = () => {
      if (timer) clearTimeout(timer);
      timer = setTimeout(() => {
        timer = null;
        latest.current();
      }, PROJECTION_RECONCILE_MS);
    };
    window.addEventListener(DEFECTS_CHANGED_EVENT, onChanged);
    return () => {
      window.removeEventListener(DEFECTS_CHANGED_EVENT, onChanged);
      if (timer) clearTimeout(timer);
    };
  }, []);
}
