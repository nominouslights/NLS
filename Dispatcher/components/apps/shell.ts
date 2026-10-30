import type { ScreenId } from "@/lib/nav";

// What Console hands every app. The apps are the seam between the launcher and the screens:
// each renders its own screens (still under components/screens/) and reaches the rest of the
// console only through this interface.
export interface Shell {
  /** The one cross-app link: select a trip (or none) and land on Trip Operations → Trips. */
  openTrip(id: string | null): void;
  /** Opens the global CreateTripWizard; its onCreated routes through openTrip. */
  createTrip(): void;
  /** Opens the global BookeoImportModal (mounted beside CreateTripWizard), or null when the
   *  role cannot reach Trips. Closing it after a commit bumps `tripsRefreshKey`. */
  importBookeo: (() => void) | null;
  /** Bumped when something outside the Trips screen wrote trips (a Bookeo import); the
   *  Trips list refetches on change. */
  tripsRefreshKey: number;
  /** Console derives the app from the screen and refuses screens the role may not use. */
  navigate(screen: ScreenId): void;
  goHome(): void;
}

export interface AppProps {
  screen: ScreenId;
  shell: Shell;
}
