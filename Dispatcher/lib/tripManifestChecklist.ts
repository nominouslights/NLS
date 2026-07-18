// Canonical NL-TM-01 checklist constants — frontend mirror of the backend's
// Trips ManifestChecklist (Backend/src/Trips/Domain/Manifests/ManifestChecklist.cs).
// The backend owns the contract: every `key` below (item keys and group keys)
// is copied VERBATIM from that file and is what travels over the wire; `label`
// is the long display text printed on the paper form and shown in the UI.
// Dependency-free: plain data, no imports.

export type PreTripGroupId = "exterior" | "safety" | "interior" | "comms";

export interface PreTripItem {
  /** Backend wire item string — exactly ManifestChecklist.cs. */
  key: string;
  /** Long display label as printed on the NL-TM-01 form. */
  label: string;
}

export interface PreTripGroup {
  /** Short stable id used for page-split selection in the PDF composer. */
  group: PreTripGroupId;
  /** Backend wire group string — exactly ManifestChecklist.cs group constants. */
  key: string;
  /** Display title (same text as the backend group label on this form). */
  title: string;
  items: PreTripItem[];
}

/** The 28-item pre-trip inspection, in form order (§3 of NL-TM-01). */
export const PRE_TRIP_GROUPS: PreTripGroup[] = [
  {
    group: "exterior",
    key: "Exterior & Mechanical",
    title: "Exterior & Mechanical",
    items: [
      { key: "Tires", label: "Tires - condition & pressure" },
      { key: "Lights", label: "Lights - headlights, brake, turn, hazard" },
      { key: "Windshield & mirrors", label: "Windshield & mirrors - clean, no cracks" },
      { key: "Wipers & washer fluid", label: "Wipers & washer fluid" },
      { key: "Brakes", label: "Brakes - tested, no warning lights" },
      { key: "Horn", label: "Horn - functioning" },
      { key: "Exhaust", label: "Exhaust - no leaks or damage" },
      { key: "Fluid levels", label: "Fluid levels - oil, coolant, brake" },
      { key: "Battery", label: "Battery - secure, no corrosion" },
      { key: "Block heater cord (winter)", label: "Block heater cord - intact (winter)" },
    ],
  },
  {
    group: "safety",
    key: "Safety Equipment",
    title: "Safety Equipment",
    items: [
      { key: "First aid kit", label: "First aid kit - stocked & accessible" },
      { key: "Fire extinguisher", label: "Fire extinguisher - charged & mounted" },
      { key: "Emergency triangles / flares", label: "Emergency triangles / flares" },
      { key: "Spill kit (mine requirement)", label: "Spill kit (mine requirement)" },
      { key: "Safety beacon / whip flag", label: "Safety beacon / whip flag" },
      { key: "Backup alarm", label: "Backup alarm - functioning" },
      { key: "Seatbelts", label: "Seatbelts - all functioning" },
      { key: "Cargo restraints / tie-downs", label: "Cargo restraints / tie-downs" },
      { key: "Reflective safety vest", label: "Reflective safety vest" },
      { key: "Jumper cables / booster pack", label: "Jumper cables / booster pack" },
    ],
  },
  {
    group: "interior",
    key: "Interior & Comfort",
    title: "Interior & Comfort",
    items: [
      { key: "Interior clean", label: "Interior clean & clear of debris" },
      { key: "Heat / AC", label: "Heat / AC - functioning" },
      { key: "Doors", label: "Doors - open/close/lock properly" },
      { key: "Dashboard — no warning lights", label: "Dashboard - no warning lights" },
    ],
  },
  {
    group: "comms",
    key: "Communications & Navigation",
    title: "Communications & Navigation",
    items: [
      { key: "Cell phone charged", label: "Cell phone - charged" },
      { key: "Starlink / satellite comm connected", label: "Starlink / satellite comm - connected" },
      { key: "GPS / navigation", label: "GPS / navigation - functioning" },
      { key: "Emergency contacts list", label: "Emergency contacts list - in vehicle" },
    ],
  },
];

/**
 * §9 post-trip inspection items, in form order. These strings are the backend
 * wire contract verbatim (ManifestChecklist.PostTripItems) and double as the
 * display labels.
 */
export const POST_TRIP_ITEMS: string[] = [
  "Vehicle exterior — no new damage",
  "All passengers disembarked safely",
  "Vehicle secured / plugged in",
  "Interior cleaned & checked",
  "All cargo delivered / accounted for",
  "Keys returned / secured",
];

/** §10 driver certification attestations, in form order (backend-verbatim). */
export const ATTESTATIONS: string[] = [
  "I have completed the pre-trip inspection and the vehicle is safe to operate",
  "All passengers were transported safely with seatbelts worn",
  "All cargo was properly secured",
  "All information on this manifest is accurate and complete",
  "I have reported all issues, incidents, or defects",
];

/** §5 passenger manifest always renders this many rows. */
export const MAX_PASSENGER_ROWS = 8;

/** §6 cargo manifest always renders this many rows. */
export const MAX_CARGO_ROWS = 4;
