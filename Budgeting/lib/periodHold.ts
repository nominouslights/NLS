"use client";

import { createContext, useContext, useLayoutEffect } from "react";

// The in-flight guard. A transition, an item removal, a copy or an item save names the
// entered period in its request; if the planner could switch period while one is running, the
// request would finish against the period they had left and its result (or its error) would
// land nowhere they can see. So anything mid-request takes a hold, and while any hold is taken
// Console refuses to switch period or to start a new one.
//
// Rail navigation is deliberately NOT blocked: moving between screens never changes the period,
// so it cannot misdirect a request — and blocking it would mean editing the copied NavRail.
//
// The default context is a no-op, so a component rendered outside Console (a test, say) can
// call usePeriodHold without a provider.

export interface PeriodHold {
  /** Takes one hold; returns its release. Releasing twice is harmless. */
  acquire: () => () => void;
}

export const PeriodHoldContext = createContext<PeriodHold>({ acquire: () => () => {} });

/**
 * Holds the entered period while `active` is true. useLayoutEffect rather than useEffect so the
 * hold is in place before the browser paints the busy state — there is no frame in which the
 * banner shows SWITCH PERIOD enabled beside a request already in flight.
 */
export function usePeriodHold(active: boolean): void {
  const { acquire } = useContext(PeriodHoldContext);
  useLayoutEffect(() => {
    if (!active) return;
    return acquire();
  }, [active, acquire]);
}
