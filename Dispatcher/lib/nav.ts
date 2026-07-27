export type ScreenId =
  | "dispatch"
  | "map"
  | "trips"
  | "drivers"
  | "fleet"
  | "routes"
  | "stops"
  | "manifests"
  | "grocery"
  | "clients"
  | "riders"
  | "billing"
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

export const NAV_GROUPS: NavGroup[] = [
  {
    label: "OPERATIONS",
    collapsedLabel: "OPS",
    items: [
      { id: "dispatch", label: "Dispatch Board", code: "DB" },
      { id: "map", label: "Live Map", code: "MP" },
      { id: "trips", label: "Trips", code: "TR" },
      { id: "drivers", label: "Drivers & Compliance", code: "DR", badge: "2" },
      { id: "fleet", label: "Fleet & Maintenance", code: "FM", badge: "1" },
      { id: "routes", label: "Routes & Schedules", code: "RT" },
      { id: "stops", label: "Stops", code: "SP" },
      { id: "manifests", label: "Manifests & Demand", code: "MF" },
      { id: "grocery", label: "Grocery & Parcel", code: "GP" },
    ],
  },
  {
    label: "BUSINESS",
    collapsedLabel: "BIZ",
    items: [
      { id: "clients", label: "Clients & Contracts", code: "CL", badge: "1" },
      { id: "riders", label: "Riders", code: "RD" },
      { id: "billing", label: "Billing & Invoicing", code: "BL", badge: "1" },
      { id: "incidents", label: "Incidents & Faults", code: "IN", badge: "2" },
      { id: "comms", label: "Communications", code: "CM" },
      { id: "settings", label: "Settings", code: "ST" },
    ],
  },
];
