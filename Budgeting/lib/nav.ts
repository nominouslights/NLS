// The one place Budgeting's chrome differs from Dispatcher's. components/NavRail.tsx is a
// verbatim copy and reads everything from here, so the whole navigation change is data-only:
// two groups with an uppercase label and a collapsed short form, items carrying a two-letter
// JetBrains Mono code tile and an optional count badge. Settings sits last in the second
// group and keeps Dispatcher's "ST" code — a deliberate cross-app tell.

export type ScreenId =
  | "periods"
  | "codes"
  | "allocations"
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
      { id: "allocations", label: "Allocations", code: "AL" },
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
