import { INTERNAL_APPS, railGroupsFor } from "./apps";

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
  | "comms"
  | "settings";

export interface NavItem {
  id: ScreenId;
  label: string;
  code: string;
  badge?: string;
}

export interface NavGroup {
  label: string;
  collapsedLabel: string;
  items: NavItem[];
}

// Every app's rail group, in launcher order. The screens themselves are declared once, in
// lib/apps.ts; this export survives as NavRail's default so the component (copied verbatim
// into Budgeting/, whose own lib/nav.ts exports the same name) keeps working with no props
// beyond the original three.
export const NAV_GROUPS: NavGroup[] = INTERNAL_APPS.flatMap(railGroupsFor);
