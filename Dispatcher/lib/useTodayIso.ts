"use client";

import { useSyncExternalStore } from "react";

// Today's date as yyyy-MM-dd in the browser's own zone — the DateOnly spelling the APIs
// use — for screens that key state by today (the Drivers calendar's default month/day and
// the HOS tab's "today"). Empty on the server render, so a client component falls back
// until hydration hands it the viewer's date.
//
// Deliberately NOT a change to lib/useToday.ts: that file is copied verbatim into
// Budgeting/ and DriverField/, so extending it is a design-system change with a two-app
// re-copy. This is app-local. The date rolls over at local midnight via a one-minute tick
// that only runs while something is subscribed.

const listeners = new Set<() => void>();
let timer: ReturnType<typeof setInterval> | null = null;

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  if (timer === null) {
    timer = setInterval(() => listeners.forEach((l) => l()), 60_000);
  }
  return () => {
    listeners.delete(listener);
    if (listeners.size === 0 && timer !== null) {
      clearInterval(timer);
      timer = null;
    }
  };
}

const isoFmt = new Intl.DateTimeFormat("en-CA", { year: "numeric", month: "2-digit", day: "2-digit" });

/** Stable within a day, so React sees a new snapshot only when the date actually changes. */
function getSnapshot(): string {
  return isoFmt.format(new Date());
}

function getServerSnapshot(): string {
  return "";
}

export function useTodayIso(): string {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
