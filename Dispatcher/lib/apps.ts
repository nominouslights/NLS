// ---------------------------------------------------------------------------
// The app manifest — the one place to edit when an app gains or loses a screen, or a role
// gains or loses an app.
//
// The Dispatch Console is a launcher (components/Home.tsx) over seven focused apps, each a
// short NavRail group of screens. Apps are logical sections of this one codebase, not separate
// deployments; the Budgeting console is the exception and is linked out to as an external tile.
//
// Only `import type` from lib/nav.ts here: nav.ts derives NAV_GROUPS from INTERNAL_APPS at
// runtime, so a value import in this direction would be a cycle.
// ---------------------------------------------------------------------------

import type { NavGroup, NavItem, ScreenId } from "./nav";
import { BUDGET_ROLES, DISPATCH_ROLES, hasRole, normalizeRole } from "./roles";

export type AppId =
  | "today"
  | "tripOps"
  | "fleet"
  | "drivers"
  | "communityBooking"
  | "commercial"
  | "admin"
  | "budgeting";

interface AppBase {
  id: AppId;
  label: string;
  /** Two-letter mono code — the rail's collapsed label, the breadcrumb square, the tile icon. */
  code: string;
  tagline: string;
  /** Roles that may open the app. A UX gate only — see lib/roles.ts. */
  roles: readonly string[];
}

export interface InternalApp extends AppBase {
  kind: "internal";
  /** Rail order; screens[0] is the landing screen when the app is opened fresh. */
  screens: [NavItem, ...NavItem[]];
}

export interface ExternalApp extends AppBase {
  kind: "external";
  /** Where the tile links; null = not configured at build time (the tile says so). */
  href: string | null;
  hint: string;
}

export type AppDef = InternalApp | ExternalApp;

// Read as the literal `process.env.NEXT_PUBLIC_BUDGETING_URL` so `next build` inlines it into
// the client bundle — a dynamic lookup would always be undefined in the browser. Unset (or
// empty) renders the Budgeting tile as "Not configured" rather than hiding it, so a mis-built
// image is obvious.
const BUDGETING_URL = process.env.NEXT_PUBLIC_BUDGETING_URL || null;

// Role visibility (adjustable — edit the `roles` field):
//   Today, Trip Operations, Fleet, Drivers, Community Booking, Admin → Owner, Dispatcher, Supervisor
//   Commercial → those three plus Accountant (billing, reports)
//   Budgeting tile → Owner, Accountant (Roles.BudgetAccess)
// Legacy "Admin" is normalised to Owner before matching. Driver sees nothing — the Driver Field
// App is their home. BoardMember sees nothing either, on purpose: Commercial is an editing
// surface (contracts, POs, invoices) and the architecture puts board members in the future
// read-only Owner/Exec app, not here; until Reports can stand alone there is no safe tile.
const COMMERCIAL_ROLES = [...DISPATCH_ROLES, "Accountant"] as const;

export const APPS: AppDef[] = [
  {
    kind: "internal",
    id: "today",
    label: "Today",
    code: "TD",
    tagline: "What is moving right now — the board and the map.",
    roles: DISPATCH_ROLES,
    screens: [
      { id: "dispatch", label: "Dispatch Board", code: "DB" },
      { id: "map", label: "Live Map", code: "MP" },
    ],
  },
  {
    kind: "internal",
    id: "tripOps",
    label: "Trip Operations",
    code: "TO",
    tagline: "Plan and run trips: schedules, stops, manifests and cargo.",
    roles: DISPATCH_ROLES,
    screens: [
      { id: "trips", label: "Trips", code: "TR" },
      { id: "routes", label: "Routes & Schedules", code: "RT" },
      { id: "stops", label: "Stops", code: "SP" },
      { id: "manifests", label: "Manifests & Demand", code: "MF" },
      { id: "cargo", label: "Cargo & Grocery", code: "CG" },
    ],
  },
  {
    kind: "internal",
    id: "fleet",
    label: "Fleet & Maintenance",
    code: "FL",
    tagline: "Vehicles, inspections, defects and work orders.",
    roles: DISPATCH_ROLES,
    screens: [{ id: "fleet", label: "Fleet", code: "FM", badge: "1" }],
  },
  {
    kind: "internal",
    id: "drivers",
    label: "Drivers & Compliance",
    code: "DC",
    tagline: "Credentials, hours of service, incidents and faults.",
    roles: DISPATCH_ROLES,
    screens: [
      { id: "drivers", label: "Drivers", code: "DR", badge: "2" },
      { id: "incidents", label: "Incidents & Faults", code: "IN", badge: "2" },
    ],
  },
  {
    kind: "internal",
    id: "communityBooking",
    label: "Community Booking",
    code: "CB",
    tagline: "Demand-activated departures — the calendar and its policy.",
    roles: DISPATCH_ROLES,
    screens: [
      { id: "bookings", label: "Booking Calendar", code: "BK" },
      { id: "bookingPolicy", label: "Booking Policy", code: "BP" },
    ],
  },
  {
    kind: "internal",
    id: "commercial",
    label: "Commercial",
    code: "CO",
    tagline: "Clients and contracts, riders, invoicing and reports.",
    roles: COMMERCIAL_ROLES,
    screens: [
      { id: "clients", label: "Clients & Contracts", code: "CL", badge: "1" },
      { id: "riders", label: "Riders", code: "RD" },
      { id: "billing", label: "Billing", code: "BL" },
      { id: "reports", label: "Reports", code: "RP" },
    ],
  },
  {
    kind: "internal",
    id: "admin",
    label: "Admin",
    code: "AD",
    tagline: "Organization settings, users and roles, communications.",
    roles: DISPATCH_ROLES,
    screens: [
      { id: "settings", label: "Settings", code: "ST" },
      { id: "comms", label: "Communications", code: "CM" },
    ],
  },
  {
    kind: "external",
    id: "budgeting",
    label: "Budgeting",
    code: "BG",
    tagline: "Zero-based budgeting: periods, codes and allocations.",
    roles: BUDGET_ROLES,
    href: BUDGETING_URL,
    hint: "Opens in a new tab",
  },
];

export const INTERNAL_APPS: InternalApp[] = APPS.filter((a): a is InternalApp => a.kind === "internal");

/** Whether `role` may open `app`. Legacy "Admin" counts as Owner. Case-sensitive otherwise. */
export function canUseApp(app: AppDef, role: string | null): boolean {
  return hasRole(app.roles, normalizeRole(role));
}

// A literal Record over every ScreenId, so adding a screen to the union without placing it in
// an app is a build error here rather than a blank main area at runtime.
const APP_FOR_SCREEN: Record<ScreenId, AppId> = {
  dispatch: "today",
  map: "today",
  trips: "tripOps",
  routes: "tripOps",
  stops: "tripOps",
  manifests: "tripOps",
  cargo: "tripOps",
  fleet: "fleet",
  drivers: "drivers",
  incidents: "drivers",
  bookings: "communityBooking",
  bookingPolicy: "communityBooking",
  clients: "commercial",
  riders: "commercial",
  billing: "commercial",
  reports: "commercial",
  settings: "admin",
  comms: "admin",
};

const INTERNAL_BY_ID = new Map(INTERNAL_APPS.map((a) => [a.id, a]));

/** The app a screen belongs to. */
export function appForScreen(screen: ScreenId): InternalApp {
  const app = INTERNAL_BY_ID.get(APP_FOR_SCREEN[screen]);
  if (!app) throw new Error(`Screen "${screen}" maps to an app that is not internal`);
  return app;
}

/** The NavRail groups for one app: a single group, headed by the app's label. */
export function railGroupsFor(app: InternalApp): NavGroup[] {
  return [{ label: app.label.toUpperCase(), collapsedLabel: app.code, items: app.screens }];
}

/** Sum of the app's screens' badges (literals today), or undefined when none carries one. */
export function appBadge(app: InternalApp): string | undefined {
  let total = 0;
  let any = false;
  for (const s of app.screens) {
    if (!s.badge) continue;
    const n = Number(s.badge);
    if (Number.isNaN(n)) continue;
    total += n;
    any = true;
  }
  return any ? String(total) : undefined;
}
