import { INTERNAL_APPS, railGroup } from "./apps";

export type ScreenId =
  | "dispatch"
  | "map"
  | "trips"
  | "drivers"
  | "fleet"
  | "routes"
  | "stops"
  | "manifests"
  | "cargo"
  | "bookings"
  | "bookingPolicy"
  | "clients"
  | "riders"
  | "billing"
  | "reports"
  | "incidents"
  | "compliance"
  | "comms"
  | "settings";

export interface NavItem {
  id: ScreenId;
  label: string;
  code: string;
  badge?: string;
  /**
   * Roles that may open this screen, on top of the app's own gate. Omitted = everyone who can
   * open the app. A UX gate only — the screen's endpoints carry the real policy.
   */
  roles?: readonly string[];
}

export interface NavGroup {
  label: string;
  collapsedLabel: string;
  items: NavItem[];
}

// Every app's rail group, in launcher order. The screens themselves are declared once, in
// lib/apps.ts; this export survives as NavRail's default so the component (copied verbatim
// into Budgeting/, whose own lib/nav.ts exports the same name) keeps working with no props
// beyond the original three. Unfiltered by role: Console always passes its own role-filtered
// groups (railGroupsFor), so this default is the manifest, never what a user sees.
export const NAV_GROUPS: NavGroup[] = INTERNAL_APPS.map((app) => railGroup(app, app.screens));
