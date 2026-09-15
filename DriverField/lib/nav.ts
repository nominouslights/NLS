// Navigation data for the Driver Field App.
//
// Same shape as Budgeting/lib/nav.ts and Dispatcher's — groups with an uppercase label and a
// collapsed short form, items carrying a two-letter code tile and an optional badge — but this
// app renders it through components/DutyRail.tsx rather than the copied NavRail. NavRail's
// geometry is hardcoded in the component (72/236px widths, 26px tiles, 13px labels) with
// nothing driven from here, and enlarging it would mean editing a protected copy. See
// DriverField/CLAUDE.md.
//
// Two groups, matching how a shift actually runs: what the driver is doing right now, and what
// the regulator will ask for afterwards.

export type ScreenId =
  | "today"
  | "trips"
  | "manifest"
  | "inspection"
  | "hours"
  | "incidents"
  | "vehicle"
  | "profile";

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

export const NAV_GROUPS: NavGroup[] = [
  {
    label: "SHIFT",
    collapsedLabel: "SHF",
    items: [
      { id: "today", label: "Today", code: "TD" },
      { id: "trips", label: "Trips", code: "TR" },
      { id: "manifest", label: "Manifest", code: "MF" },
    ],
  },
  {
    label: "COMPLIANCE",
    collapsedLabel: "CMP",
    items: [
      { id: "inspection", label: "Pre-Trip", code: "PT" },
      { id: "hours", label: "Hours", code: "HS" },
      { id: "incidents", label: "Incidents", code: "IN" },
      { id: "vehicle", label: "Vehicle", code: "VH" },
      { id: "profile", label: "My Profile", code: "MP" },
    ],
  },
];
