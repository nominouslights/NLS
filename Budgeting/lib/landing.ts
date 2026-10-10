import { NAV_GROUPS, type ScreenId } from "@/lib/nav";

// A one-shot "open this screen next" hint, for the one page that lives outside the console: the
// QuickBooks OAuth callback (app/qbo/callback). The console has no URL routing — every screen is
// a ScreenId in Console's state — so after connecting, the callback writes the hint and sends
// the browser to the app root, and Console opens on the screen named here instead of the
// Period Dashboard.
//
// sessionStorage, never localStorage: the hint belongs to this tab's next page load and nothing
// else. Console reads it in its initial state and clears it in an effect (reading is pure, so
// React's double-invoked initialiser in dev cannot lose it). Every access is try/catch'd — a
// blocked store just means landing on the default screen.

export const LANDING_STORAGE_KEY = "nl.budgeting.landingScreen";

const KNOWN: ReadonlySet<string> = new Set(NAV_GROUPS.flatMap((g) => g.items.map((i) => i.id)));

export function writeLandingScreen(id: ScreenId): void {
  try {
    sessionStorage.setItem(LANDING_STORAGE_KEY, id);
  } catch {
    // storage blocked — the console opens on its default screen
  }
}

/** The pending hint, if it names a real screen. Does not clear it — see clearLandingScreen. */
export function readLandingScreen(): ScreenId | null {
  try {
    const v = sessionStorage.getItem(LANDING_STORAGE_KEY);
    return v !== null && KNOWN.has(v) ? (v as ScreenId) : null;
  } catch {
    return null;
  }
}

export function clearLandingScreen(): void {
  try {
    sessionStorage.removeItem(LANDING_STORAGE_KEY);
  } catch {
    // nothing to clear
  }
}
