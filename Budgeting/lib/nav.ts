// The one place Budgeting's chrome differs from Dispatcher's. components/NavRail.tsx is a
// verbatim copy and reads everything from here, so the whole navigation change is data-only:
// two groups with an uppercase label and a collapsed short form, items carrying a two-letter
// JetBrains Mono code tile and an optional count badge. Settings sits last in the second
// group and keeps Dispatcher's "ST" code — a deliberate cross-app tell.

// Planning is two items, not three: allocations are planned on the period's own dashboard
// (screens/periods/), so a separate Allocations screen was a second place to do the same job —
// and the worse one, since it could not refresh Console's period list after a save. Removed
// rather than fixed; "one place to plan a period" is the point.

export type ScreenId =
  | "periods"
  | "codes"
  | "actuals"
  | "variance"
  | "reports"
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

export const NAV_GROUPS: NavGroup[] = [
  {
    label: "PLANNING",
    collapsedLabel: "PLN",
    items: [
      { id: "periods", label: "Budget Periods", code: "BP" },
      { id: "codes", label: "Budget Codes", code: "BC" },
    ],
  },
  {
    label: "PERFORMANCE",
    collapsedLabel: "PERF",
    items: [
      { id: "actuals", label: "Actuals vs Budget", code: "AB" },
      { id: "variance", label: "Variance", code: "VR", badge: "3" },
      { id: "reports", label: "Reports", code: "RP" },
      { id: "settings", label: "Settings", code: "ST" },
    ],
  },
];
