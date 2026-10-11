// ---------------------------------------------------------------------------
// Mock data for the Driver Field App.
//
// Every value on every screen EXCEPT the hours of service comes from this file. Hours are live
// (lib/hos/ledger.ts against /api/drivers/{id}/hos/*); trips, manifests, vehicles, defects,
// incidents, credentials and the driver's own profile are still mock, and the screens that
// show them still carry a MockTag. Conventions copied from Dispatcher/lib/data.ts and
// Budgeting/lib/data.ts, and they are load-bearing, not style:
//
//   • flat exported const arrays of plain objects — no factories, no classes, no generators
//   • string ids with a domain prefix (TRP-, HOS-, DVR-)
//   • ISO date strings, never Date objects
//   • every status-bearing row carries its own StatusKind, so rendering is a pure lookup and a
//     colour can never be chosen without also choosing the glyph and label that travel with it
//   • derived arrays are COMPUTED, never duplicated — the two can then never disagree
//   • threshold and label logic lives here, not in a screen, so any future report agrees with
//     the screen by construction
//   • no screen invents an API shape. Each array is replaced by additions to lib/api/<domain>.ts
//     as its backend slice lands, and screens keep their props.
//
// One deliberate deviation from Budgeting: it keys mock rows to ids no real record can match,
// so screens already on real data show their empty states. There is no real data here at all,
// so instead EVERY screen carries a <MockTag/>. Nobody should be able to demo this as working
// software.
//
// Today is pinned rather than computed from the clock so screenshots, tests and the seven-day
// HOS strip stay stable.
// ---------------------------------------------------------------------------

import type { StatusKind } from "./theme";
import type {
  ClientContract,
  Clearance,
  Credential,
  CrewBadge,
  DriverProfile,
  DvirSubmission,
  FuelEntry,
  Incident,
  ManifestRow,
  Trip,
  Vehicle,
  VehicleDefect,
} from "./types";

/** The reference date every mock row is positioned against. */
export const today = "2026-09-12";

// --- the signed-in driver --------------------------------------------------

// The one justified non-array in this file: there is exactly one signed-in driver, and an
// array of length 1 would only invite a screen to render a picker that must never exist.
export const currentDriver: DriverProfile = {
  id: "DRV-2041",
  name: "R. Beardy",
  licenceClass: "4",
  licenceExpiresOn: "2027-04-30",
  phone: "204-555-0142",
  homeBase: "Thompson",
  employeeNumber: "NL-0142",
};

// --- credentials & clearances ----------------------------------------------

/**
 * Expiry banding for anything that lapses. Shared by credentials and clearances so a licence
 * and an Alamos clearance can never be read as differently urgent at the same number of days.
 */
export function credentialKind(expiresOn: string, asOf: string = today): StatusKind {
  const days = daysBetween(asOf, expiresOn);
  if (days < 0) return "over";
  if (days <= 30) return "soon";
  return "ontime";
}

export const credentials: Credential[] = [
  {
    id: "CR-3101",
    kind: "Class 4 Licence",
    reference: "B1234-56789",
    expiresOn: "2027-04-30",
    ck: credentialKind("2027-04-30"),
  },
  {
    id: "CR-3102",
    kind: "Medical Certificate",
    reference: "MED-88421",
    expiresOn: "2026-10-02",
    ck: credentialKind("2026-10-02"),
  },
  {
    id: "CR-3103",
    kind: "First Aid / CPR",
    reference: "SJA-20419",
    expiresOn: "2028-01-15",
    ck: credentialKind("2028-01-15"),
  },
  {
    id: "CR-3104",
    kind: "Air Brake Endorsement",
    reference: "AB-4471",
    expiresOn: "2026-08-28",
    ck: credentialKind("2026-08-28"),
  },
];

export const clearances: Clearance[] = [
  {
    id: "CL-4201",
    client: "Alamos",
    kind: "Contractor Site Clearance",
    grantedOn: "2026-03-04",
    expiresOn: "2027-03-04",
    ck: credentialKind("2027-03-04"),
  },
  {
    id: "CL-4202",
    client: "Alamos",
    kind: "Site Safety Orientation",
    grantedOn: "2026-03-04",
    expiresOn: "2026-09-30",
    ck: credentialKind("2026-09-30"),
  },
];

/** The clearances that are actually usable today — eligibility rule 5 reads this, not the list. */
export const activeClearanceClients: string[] = clearances
  .filter((c) => c.ck !== "over")
  .map((c) => c.client);

// --- vehicles --------------------------------------------------------------

export const vehicles: Vehicle[] = [
  {
    id: "VEH-11",
    unit: "NL-01",
    description: "2023 Ford Transit 350",
    seats: 7,
    odometerKm: 184_920,
    status: "Active",
    vk: "ontime",
    licenceClassRequired: "4",
    hasFailedDvir: false,
  },
  {
    id: "VEH-14",
    unit: "NL-04",
    description: "2021 MCI Coach",
    seats: 24,
    odometerKm: 412_006,
    status: "Active",
    vk: "ontime",
    // Not a Class 4 vehicle — this is what makes eligibility rule 2 visible on screen rather
    // than a rule nobody can demonstrate.
    licenceClassRequired: "2",
    hasFailedDvir: false,
  },
  {
    id: "VEH-16",
    unit: "NL-06",
    description: "2019 Chevrolet Express",
    seats: 12,
    odometerKm: 268_431,
    status: "Out of Service",
    vk: "over",
    licenceClassRequired: "4",
    hasFailedDvir: true,
  },
];

export const assignedVehicleId = "VEH-11";

export const assignedVehicle: Vehicle =
  vehicles.find((v) => v.id === assignedVehicleId) ?? vehicles[0];

// A defect is OPEN until a work-order completion records a resolving outcome for it (Repaired
// or No fault found). A linked work order does not close it — "On WO-2231" is still a fault the
// driver must see before the pre-trip — and a Deferred outcome leaves it open too.
export const vehicleDefects: VehicleDefect[] = [
  {
    id: "DF-5301",
    vehicleId: "VEH-11",
    reportedOn: "2026-09-10",
    item: "Windshield washer pump intermittent",
    severity: "Minor",
    note: "Works after a few tries. Not a road hazard in current conditions.",
    dk: "soon",
    workOrder: null,
    resolvedOn: null,
    resolution: null,
  },
  {
    id: "DF-5302",
    vehicleId: "VEH-16",
    reportedOn: "2026-09-08",
    item: "Rear brake line corrosion",
    severity: "Out of Service",
    note: "Unit parked at Thompson yard pending inspection.",
    dk: "over",
    // Open WITH a work order — the unit is in the shop, and the defect is still open.
    workOrder: "WO-2219",
    resolvedOn: null,
    resolution: null,
  },
  {
    id: "DF-5303",
    vehicleId: "VEH-11",
    reportedOn: "2026-09-09",
    item: "Driver-side mirror loose in bracket",
    severity: "Minor",
    note: "Holds position but drifts on washboard gravel.",
    dk: "soon",
    // Open WITH a work order — parts on order, defect still open.
    workOrder: "WO-2231",
    resolvedOn: null,
    resolution: null,
  },
  {
    id: "DF-5304",
    vehicleId: "VEH-11",
    reportedOn: "2026-09-04",
    item: "Rear marker lamp out",
    severity: "Minor",
    note: "Right rear amber marker dark.",
    dk: "soon",
    workOrder: "WO-2224",
    resolvedOn: "2026-09-11",
    resolution: "Repaired",
  },
  {
    id: "DF-5305",
    vehicleId: "VEH-11",
    reportedOn: "2026-09-06",
    item: "Heater fan noise",
    severity: "Minor",
    note: "Rattle at high fan speed.",
    dk: "soon",
    workOrder: "WO-2226",
    resolvedOn: "2026-09-08",
    resolution: "NoFaultFound",
  },
];

/** A defect is open until it is resolved. A linked work order alone does NOT close it. */
export function isDefectOpen(d: VehicleDefect): boolean {
  return d.resolvedOn === null;
}

/** How far back a resolved defect still shows on the tablet, in days. */
export const RECENTLY_RESOLVED_DAYS = 14;

/** Resolved defects whose resolution falls within the last RECENTLY_RESOLVED_DAYS, newest first. */
export function recentlyResolved(
  defects: VehicleDefect[],
  asOf: string = today,
): VehicleDefect[] {
  return defects
    .filter((d) => d.resolvedOn !== null)
    .filter((d) => {
      const days = daysBetween(d.resolvedOn as string, asOf);
      return days >= 0 && days <= RECENTLY_RESOLVED_DAYS;
    })
    .sort((a, b) => (b.resolvedOn as string).localeCompare(a.resolvedOn as string));
}

/** The work-order tag on an OPEN defect row. */
export function openDefectWorkOrderText(d: VehicleDefect): string {
  return d.workOrder ? `On ${d.workOrder}` : "No WO";
}

/**
 * The outcome line on a RESOLVED defect row, in the console's wording:
 * "Repaired under WO-2224 on 2026-09-11" / "No fault found — WO-2226".
 */
export function defectResolutionText(d: VehicleDefect): string {
  if (d.resolution === "Repaired") {
    const under = d.workOrder ? ` under ${d.workOrder}` : "";
    return `Repaired${under} on ${d.resolvedOn}`;
  }
  if (d.resolution === "NoFaultFound") {
    return d.workOrder ? `No fault found — ${d.workOrder}` : "No fault found";
  }
  return "Open";
}

/** Short chip label for a resolved defect. */
export function defectResolutionLabel(d: VehicleDefect): string {
  return d.resolution === "NoFaultFound" ? "No fault found" : "Repaired";
}

/** Worst status among a set of defects — "ontime" when there are none. */
export function worstDefectKind(defects: VehicleDefect[]): StatusKind {
  if (defects.some((d) => d.dk === "over")) return "over";
  if (defects.length > 0) return "soon";
  return "ontime";
}

export const openDefects: VehicleDefect[] = vehicleDefects.filter(isDefectOpen);

/** Open defects on the driver's assigned vehicle — what Today's tile counts. */
export const assignedVehicleOpenDefects: VehicleDefect[] = openDefects.filter(
  (d) => d.vehicleId === assignedVehicleId,
);

// --- trips -----------------------------------------------------------------

export const trips: Trip[] = [
  {
    id: "TRP-7401",
    tripNumber: "T-26091201",
    mode: "Assigned",
    serviceType: "Alamos",
    clientName: "Alamos Gold",
    origin: "Thompson",
    destination: "Alamos Mine Site",
    startsAt: "2026-09-12T06:30:00",
    endsAt: "2026-09-12T09:15:00",
    estimatedHours: 2.75,
    vehicleId: "VEH-11",
    requiredLicenceClass: "4",
    requiresClearance: "Alamos",
    seats: 7,
    status: "Scheduled",
    tk: "ontime",
  },
  {
    id: "TRP-7402",
    tripNumber: "T-26091202",
    mode: "Assigned",
    serviceType: "Alamos",
    clientName: "Alamos Gold",
    origin: "Alamos Mine Site",
    destination: "Thompson",
    startsAt: "2026-09-12T17:00:00",
    endsAt: "2026-09-12T19:45:00",
    estimatedHours: 2.75,
    vehicleId: "VEH-11",
    requiredLicenceClass: "4",
    requiresClearance: "Alamos",
    seats: 7,
    status: "Scheduled",
    tk: "ontime",
  },
  {
    id: "TRP-7403",
    tripNumber: "T-26091203",
    mode: "Open",
    serviceType: "Community",
    clientName: "Community Service",
    origin: "Thompson",
    destination: "Leaf Rapids",
    startsAt: "2026-09-12T11:00:00",
    endsAt: "2026-09-12T14:30:00",
    estimatedHours: 3.5,
    vehicleId: "VEH-11",
    requiredLicenceClass: "4",
    requiresClearance: null,
    seats: 7,
    status: "Open",
    tk: "info",
  },
  {
    id: "TRP-7404",
    tripNumber: "T-26091204",
    mode: "Open",
    serviceType: "Community",
    clientName: "Community Service",
    origin: "Thompson",
    destination: "Lynn Lake",
    startsAt: "2026-09-12T12:00:00",
    endsAt: "2026-09-12T18:00:00",
    estimatedHours: 6,
    // A Class 2 coach — rule 2 fails for this driver.
    vehicleId: "VEH-14",
    requiredLicenceClass: "2",
    requiresClearance: null,
    seats: 24,
    status: "Open",
    tk: "info",
  },
  {
    id: "TRP-7405",
    tripNumber: "T-26091205",
    mode: "Open",
    serviceType: "Cargo",
    clientName: "Miller the Mover",
    origin: "Thompson",
    destination: "Nelson House",
    startsAt: "2026-09-12T07:30:00",
    endsAt: "2026-09-12T10:00:00",
    estimatedHours: 2.5,
    // Overlaps TRP-7401 — rule 4 fails.
    vehicleId: "VEH-11",
    requiredLicenceClass: "4",
    requiresClearance: null,
    seats: 7,
    status: "Open",
    tk: "info",
  },
];

// Derived from `trips` so the two can never disagree.
export const assignedTrips: Trip[] = trips.filter((t) => t.mode === "Assigned");
export const openTrips: Trip[] = trips.filter((t) => t.mode === "Open");

/** The trip whose manifest and status the top bar and Today screen are about. */
export const activeTrip: Trip = assignedTrips[0];

// --- the §5.4 eligibility engine: see lib/eligibility.ts ---------------------
//
// `eligibility()` used to live here. It moved when its hours-of-service rule went onto the
// real engine (lib/hos/engine.ts) through the ledger store (lib/hos/ledger.ts): the store is
// live against the API and this file is read-only mock data, and a mock file that imports the
// live ledger would be the wrong direction. The other four rules still read the arrays below.

// --- manifest --------------------------------------------------------------

export const manifestRows: ManifestRow[] = [
  {
    id: "MR-6101",
    tripId: "TRP-7401",
    passenger: "D. Spence",
    employer: "Alamos Gold",
    badgeId: "BDG-40118",
    pickup: "Thompson Terminal",
    dropoff: "Mine Gate 2",
    boarded: true,
    noShow: false,
    bk: "ontime",
  },
  {
    id: "MR-6102",
    tripId: "TRP-7401",
    passenger: "J. Moose",
    employer: "Alamos Gold",
    badgeId: "BDG-40119",
    pickup: "Thompson Terminal",
    dropoff: "Mine Gate 2",
    boarded: true,
    noShow: false,
    bk: "ontime",
  },
  {
    id: "MR-6103",
    tripId: "TRP-7401",
    passenger: "L. Castel",
    employer: "Dumas Contracting",
    badgeId: "BDG-40204",
    pickup: "Thompson Terminal",
    dropoff: "Mine Gate 2",
    boarded: false,
    noShow: false,
    bk: "info",
  },
  {
    id: "MR-6104",
    tripId: "TRP-7401",
    passenger: "A. Flett",
    employer: "Alamos Gold",
    badgeId: "BDG-40220",
    pickup: "Thompson Terminal",
    dropoff: "Mine Gate 2",
    boarded: false,
    noShow: true,
    bk: "over",
  },
  {
    id: "MR-6105",
    tripId: "TRP-7401",
    passenger: "T. Okemow",
    employer: "Dumas Contracting",
    badgeId: "BDG-40231",
    pickup: "Thompson Terminal",
    dropoff: "Mine Gate 2",
    boarded: false,
    noShow: false,
    bk: "info",
  },
];

export const crewBadges: CrewBadge[] = manifestRows.map((r) => ({
  id: `BDG-${r.id.slice(3)}`,
  badgeId: r.badgeId,
  passenger: r.passenger,
}));

/** Computed, so a scan can resolve in one lookup rather than a scan of the manifest. */
export const badgeIndex: Map<string, CrewBadge> = new Map(
  crewBadges.map((b) => [b.badgeId, b]),
);

export function manifestFor(tripId: string): ManifestRow[] {
  return manifestRows.filter((r) => r.tripId === tripId);
}

export function boardedCount(tripId: string): number {
  return manifestFor(tripId).filter((r) => r.boarded).length;
}

export function manifestProgress(tripId: string): { boarded: number; total: number } {
  const rows = manifestFor(tripId);
  return { boarded: rows.filter((r) => r.boarded).length, total: rows.length };
}

// --- hours of service ------------------------------------------------------

// NOTHING HERE. Hours are live: the ledger store (lib/hos/ledger.ts) holds the server's last
// 14 duty days from GET /api/drivers/{id}/hos/days and the engine (lib/hos/engine.ts, the
// byte-identical mirror of Backend/src/Shared/HoursOfService) evaluates them. The per-day mock
// rows that used to sit here, and the adapter that laid them out, are gone — including the
// driver's current duty status, which is `evaluation().currentStatus` and nothing else.

// --- inspections (DVIR, NSC Standard 11) -----------------------------------

/**
 * THE CHECKLIST ITSELF IS NOT HERE. It is form NL-PTI-01, copied byte-for-byte from
 * `Dispatcher/lib/inspectionForm.ts` into `lib/inspectionForm.ts` — 80 rows across 12
 * sub-groups, narrowed per unit and per half of the form by `itemsFor(unit, mode)`.
 *
 * It deliberately does NOT live in this file, unlike every other value on every other screen.
 * A locally-invented 22-item list used to, and that is exactly what went wrong: the tablet and
 * the Dispatch Console collected different legal forms, addressed by different `Item` strings,
 * so a defect filed here could not be matched to a row there. The catalogue is a copy of one
 * source of truth for that reason, and the copy is diffed by `lib/inspectionForm.copy.test.ts`
 * as well as by the drift loop in DriverField/CLAUDE.md. Never edit the copy; change Dispatcher
 * and re-copy.
 */

/**
 * Historical submissions. `mode` and `vehicleId` are what the boarding gate reads: `type` is
 * display prose ("Pre-Trip") and `unit` is free text, and neither is a join key or a contract.
 *
 * DVR-8101 is deliberately dated YESTERDAY (2026-09-11), not today. A gate that is already
 * satisfied on first paint cannot be demonstrated — the same argument this file already makes
 * for greying ineligible trips rather than hiding them. lib/inspectionGate.test.ts pins it, so
 * restoring the old date fails a test that says why.
 *
 * `defects` carry real catalogue KEYS (pinned in lib/newDefects.test.ts) and agree with each
 * row's `result` under deriveResult (pinned in lib/inspectionGate.test.ts). They are what the
 * post-trip's "New defects since the pre-trip" step reads as "already reported" — through
 * preTripDefectItems() in lib/inspectionGate.ts, which picks the SAME pre-trip record the boarding
 * gate does (this device's certification first, then a row here for the same vehicle and service
 * day). Because no row here is dated today, on first paint that is whatever the driver certified
 * on this device; DVR-8101's washer defect is what it looks like asked as of its own day.
 */
export const dvirSubmissions: DvirSubmission[] = [
  {
    id: "DVR-8101",
    performedAt: "2026-09-11T06:05:00",
    type: "Pre-Trip",
    mode: "PreTrip",
    vehicleId: "VEH-11",
    unit: "NL-01",
    odometerKm: 184_920,
    result: "Pass with defects",
    rk: "soon",
    // The intermittent washer pump DF-5301 tracks, filed against the row it concerns.
    defects: [{ item: "Wipers & washers", severity: "Minor" }],
  },
  {
    id: "DVR-8102",
    performedAt: "2026-09-11T19:50:00",
    type: "Post-Trip",
    mode: "PostTrip",
    vehicleId: "VEH-11",
    unit: "NL-01",
    odometerKm: 184_612,
    result: "Pass",
    rk: "ontime",
    defects: [],
  },
  {
    id: "DVR-8103",
    performedAt: "2026-09-08T06:10:00",
    type: "Pre-Trip",
    mode: "PreTrip",
    vehicleId: "VEH-16",
    unit: "NL-06",
    odometerKm: 268_431,
    result: "Fail",
    rk: "over",
    // DF-5302's corroded rear brake line, plus a worn tire found on the same walk-around.
    defects: [
      { item: "Service brake pedal", severity: "Out of Service" },
      { item: "Tire condition", severity: "Minor" },
    ],
  },
];

// --- incidents -------------------------------------------------------------

export const incidentTypes: string[] = [
  "Wildlife on roadway",
  "Road conditions",
  "Passenger incident",
  "Vehicle fault",
  "Collision / near miss",
  "Site access refused",
  "Other",
];

export const incidents: Incident[] = [
  {
    id: "INC-9201",
    reportedAt: "2026-09-10T14:20:00",
    type: "Wildlife on roadway",
    severity: "Low",
    location: "PR 391, km 84",
    narrative: "Bear on shoulder, slowed and passed. No contact.",
    status: "Closed",
    ik: "ontime",
  },
  {
    id: "INC-9202",
    reportedAt: "2026-09-11T08:05:00",
    type: "Road conditions",
    severity: "Medium",
    location: "PR 280 north of Thompson",
    narrative: "Washboard and soft shoulder after rain. Reduced to 60 km/h for 20 km.",
    status: "Under review",
    ik: "soon",
  },
];

// --- fuel (no backend at all — see DriverField/CLAUDE.md, out of scope) -----

export const fuelEntries: FuelEntry[] = [
  {
    id: "FL-7101",
    filledAt: "2026-09-11T19:20:00",
    unit: "NL-01",
    litres: 78.4,
    costCad: 142,
    odometerKm: 184_612,
    location: "Thompson Co-op",
  },
  {
    id: "FL-7102",
    filledAt: "2026-09-09T07:40:00",
    unit: "NL-01",
    litres: 81.1,
    costCad: 148,
    odometerKm: 184_190,
    location: "Thompson Co-op",
  },
];

// --- client & contract (§6's narrow read-only slice) -----------------------

export const clientContracts: ClientContract[] = [
  {
    id: "CTR-5101",
    client: "Alamos Gold",
    manifestTemplate: "Alamos Crew Manifest v4",
    requiresClearance: true,
    note: "Badge scan required at boarding. Gate 2 access only.",
  },
  {
    id: "CTR-5102",
    client: "Community Service",
    manifestTemplate: "Community Passenger List",
    requiresClearance: false,
    note: "Fares collected on board. Cash or Interac e-Transfer.",
  },
];

// --- formatting ------------------------------------------------------------

/** Always writes the unit, and never leans on a trailing decimal to carry meaning. */
export function formatDurationH(hours: number): string {
  const whole = Math.floor(Math.abs(hours));
  const minutes = Math.round((Math.abs(hours) - whole) * 60);
  const sign = hours < 0 ? "−" : "";
  if (whole === 0) return `${sign}${minutes}m`;
  if (minutes === 0) return `${sign}${whole}h`;
  return `${sign}${whole}h ${minutes}m`;
}

/** "06:30" from an ISO timestamp, without dragging in a date library. */
export function formatClock(iso: string): string {
  return iso.slice(11, 16);
}

// --- internals -------------------------------------------------------------

function daysBetween(from: string, to: string): number {
  const ms = Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`);
  return Math.round(ms / 86_400_000);
}

