// The one place Budgeting's chrome differs from Dispatcher's. components/NavRail.tsx is a
// verbatim copy and reads everything from here, so the whole navigation change is data-only:
// two groups with an uppercase label and a collapsed short form, items carrying a two-letter
// JetBrains Mono code tile and an optional count badge (none is set: Variance's old hardcoded
// "3" was mock data dressed as a live count). Settings sits last in the second group and keeps
// Dispatcher's "ST" code — a deliberate cross-app tell; QuickBooks sits just above it.

// Planning has no Allocations item: allocations are planned on the period's own dashboard
// (screens/periods/), so a separate Allocations screen was a second place to do the same job —
// and the worse one, since it could not refresh Console's period list after a save. Removed
// rather than fixed; "one place to plan a period" is the point. Cost Centres (the tenant-wide
// register budget codes pick from) is a Planning item but plans nothing itself.

export type ScreenId =
  | "periods"
  | "codes"
  | "vendors"
  | "costCentres"
  | "actuals"
  | "variance"
  | "reports"
  | "qbo"
  | "settings";

/**
 * The screens that act on ONE budget period — they render only once a period is entered, inside
 * Console's banner, and every switch remounts them. Budget Codes joined when codes moved under
 * the period (each period has its own chart, routes periods/{id}/codes). Four screens are left
 * out: Settings (a profile belongs to a person, not to a period), the two tenant-wide
 * registers — Vendors (a vendor is the same counterparty in every period; routes
 * /api/budgeting/vendors) and Cost Centres (one list for every period; routes
 * /api/budgeting/cost-centres) — and QuickBooks (one connection per tenant, routes
 * qbo/connection — never under a period). Leaving them out is deliberate: scoping any of them
 * would hide a tenant-wide thing behind the period chooser for no reason.
 */
export const PERIOD_SCOPED: ReadonlySet<ScreenId> = new Set<ScreenId>([
  "periods",
  "codes",
  "actuals",
  "variance",
  "reports",
]);

export function isPeriodScoped(id: ScreenId): boolean {
  return PERIOD_SCOPED.has(id);
}

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
      { id: "periods", label: "Period Dashboard", code: "BP" },
      { id: "codes", label: "Budget Codes", code: "BC" },
      // In PLANNING because vendors are planning reference data, but NOT period-scoped.
      { id: "vendors", label: "Vendors", code: "VN" },
      { id: "costCentres", label: "Cost Centres", code: "CC" },
    ],
  },
  {
    label: "PERFORMANCE",
    collapsedLabel: "PERF",
    items: [
      { id: "actuals", label: "Actuals vs Budget", code: "AB" },
      { id: "variance", label: "Variance", code: "VR" },
      { id: "reports", label: "Reports", code: "RP" },
      { id: "qbo", label: "QuickBooks", code: "QB" },
      { id: "settings", label: "Settings", code: "ST" },
    ],
  },
];
