"use client";

import { useSyncExternalStore } from "react";
import { getServerSnapshot, getSnapshot, subscribe, type LedgerSnapshot } from "./ledger";

// The one React binding for lib/hos/ledger.ts, which is otherwise React-free — the same shape
// as lib/useInspectionStore.ts, for the same two reasons (no set-state-in-effect lint error, no
// hydration mismatch: the seed is pure, so the server snapshot and the first client render
// agree). Five surfaces read it: TopBar (the duty chip), Today (the driving tile), Hours (every
// figure), DutyControl (the current state and the disabled reasons) and Trips (through
// lib/eligibility.ts).

/** The ledger snapshot; re-renders the caller whenever an event is appended or the clock ticks. */
export function useHosLedger(): LedgerSnapshot {
  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}
