// ---------------------------------------------------------------------------
// Mock data for the Driver Field App.
//
// EVERY value on every screen comes from this file. Nothing in this app talks to a domain
// endpoint — auth is the only live thing here. Conventions copied from Dispatcher/lib/data.ts
// and Budgeting/lib/data.ts, and they are load-bearing, not style:
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
  ChecklistGroup,
  ClientContract,
  Clearance,
  Credential,
  CrewBadge,
  DriverProfile,
  DvirSubmission,
  EligibilityVerdict,
  FuelEntry,
  HosEntry,
  HosRemaining,
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
  },
  {
    id: "DF-5302",
    vehicleId: "VEH-16",
    reportedOn: "2026-09-08",
    item: "Rear brake line corrosion",
    severity: "Out of Service",
    note: "Unit parked at Thompson yard pending inspection.",
    dk: "over",
    workOrder: "WO-2219",
  },
];

export const openDefects: VehicleDefect[] = vehicleDefects.filter((d) => d.workOrder === null);

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

// --- the §5.4 eligibility engine (client-side MIRROR, never the enforcement point) ---

/**
 * The five rules from architecture §5.4, each with the reason a driver reads.
 *
 * THIS IS A MIRROR OF A RULE THE SERVER MUST OWN. §5.4 is explicit that claiming an Open trip
 * must be an atomic, server-validated operation, and that eligibility — especially hours of
 * service and clearance status — is re-checked at the moment of claiming, not when the list was
 * last loaded. Two drivers must never be able to claim the same trip. None of that exists in
 * the backend yet: there is no claim endpoint and no eligibility engine, only a
 * dispatcher-shaped POST /api/trips/{id}/assign with no concurrency guard. Client-side
 * filtering is a UX convenience; it is never the enforcement point.
 *
 * Same class of thing as Budgeting's previewPeriod() mirroring BudgetPeriod.Create.
 *
 * §5.4 also says an ineligible driver should never SEE an Open trip. This scaffold greys them
 * with the failing rule named instead, because a screen that hides rows cannot demonstrate the
 * engine it exists to prove. The real implementation filters server-side — see
 * DriverField/CLAUDE.md.
 */
export function eligibility(trip: Trip, asOf: string = today): EligibilityVerdict {
  const hos = hosRemaining();
  const vehicle = vehicles.find((v) => v.id === trip.vehicleId);
  const claimed = assignedTrips;

  const rules = [
    {
      rule: "Hours of service",
      pass: hos.drivingRemainingH >= trip.estimatedHours,
      reason:
        hos.drivingRemainingH >= trip.estimatedHours
          ? `${formatDurationH(hos.drivingRemainingH)} driving left, trip needs ${formatDurationH(trip.estimatedHours)}`
          : `Only ${formatDurationH(hos.drivingRemainingH)} driving left — trip needs ${formatDurationH(trip.estimatedHours)}`,
    },
    {
      rule: "Licence class",
      pass: currentDriver.licenceClass === trip.requiredLicenceClass,
      reason:
        currentDriver.licenceClass === trip.requiredLicenceClass
          ? `Class ${currentDriver.licenceClass} covers this vehicle`
          : `Needs Class ${trip.requiredLicenceClass}; you hold Class ${currentDriver.licenceClass}`,
    },
    {
      rule: "Vehicle status",
      pass: vehicle !== undefined && vehicle.status === "Active" && !vehicle.hasFailedDvir,
      reason:
        vehicle === undefined
          ? "Vehicle not found"
          : vehicle.hasFailedDvir
            ? `${vehicle.unit} has an outstanding failed inspection`
            : vehicle.status === "Active"
              ? `${vehicle.unit} is active with no open failures`
              : `${vehicle.unit} is ${vehicle.status}`,
    },
    {
      rule: "Schedule conflict",
      pass: !claimed.some((c) => overlaps(c, trip)),
      reason: claimed.some((c) => overlaps(c, trip))
        ? `Overlaps ${claimed.find((c) => overlaps(c, trip))?.tripNumber ?? "a claimed trip"}`
        : "No overlap with your claimed trips",
    },
    {
      rule: "Client clearance",
      pass:
        trip.requiresClearance === null ||
        activeClearanceClients.includes(trip.requiresClearance),
      reason:
        trip.requiresClearance === null
          ? "No clearance required"
          : activeClearanceClients.includes(trip.requiresClearance)
            ? `${trip.requiresClearance} clearance active`
            : `No active ${trip.requiresClearance} clearance`,
    },
  ];

  void asOf;
  return { eligible: rules.every((r) => r.pass), rules };
}

function overlaps(a: Trip, b: Trip): boolean {
  return a.startsAt < b.endsAt && b.startsAt < a.endsAt;
}

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

// CVDHS daily and cycle limits, named rather than inlined so a screen cannot quietly disagree
// with a report about what "remaining" means.
export const MAX_DRIVING_H_PER_DAY = 13;
export const MAX_ON_DUTY_H_PER_DAY = 14;
export const MAX_CYCLE_H_PER_7_DAYS = 70;

export const hosEntries: HosEntry[] = [
  {
    id: "HOS-9101",
    date: "2026-09-12",
    duty: "Driving",
    onDutyH: 3.5,
    drivingH: 2.75,
    offDutyH: 0,
    source: "Driver App",
    enteredBy: null,
    note: "Thompson → Alamos, morning run",
  },
  {
    id: "HOS-9102",
    date: "2026-09-11",
    duty: "Driving",
    onDutyH: 9.5,
    drivingH: 7.25,
    offDutyH: 14.5,
    source: "Driver App",
    enteredBy: null,
    note: "",
  },
  {
    id: "HOS-9103",
    date: "2026-09-10",
    duty: "Driving",
    onDutyH: 8,
    drivingH: 6,
    offDutyH: 16,
    source: "Manual (paper backup)",
    enteredBy: "M. Sinclair",
    note: "Tablet offline at Leaf Rapids — logged on return",
  },
  {
    id: "HOS-9104",
    date: "2026-09-09",
    duty: "On Duty",
    onDutyH: 4,
    drivingH: 0,
    offDutyH: 20,
    source: "Driver App",
    enteredBy: null,
    note: "Yard work, no driving",
  },
  {
    id: "HOS-9105",
    date: "2026-09-08",
    duty: "Driving",
    onDutyH: 10,
    drivingH: 8.5,
    offDutyH: 14,
    source: "Driver App",
    enteredBy: null,
    note: "",
  },
  {
    id: "HOS-9106",
    date: "2026-09-07",
    duty: "Off Duty",
    onDutyH: 0,
    drivingH: 0,
    offDutyH: 24,
    source: "Driver App",
    enteredBy: null,
    note: "",
  },
  {
    id: "HOS-9107",
    date: "2026-09-06",
    duty: "Driving",
    onDutyH: 7,
    drivingH: 5.5,
    offDutyH: 17,
    source: "Driver App",
    enteredBy: null,
    note: "",
  },
];

export const currentDuty: HosEntry = hosEntries[0];

/**
 * Remaining hours against the CVDHS limits. Banding: under an hour left is `over` (the driver
 * is about to be out of compliance, not merely close), under three is `soon`.
 */
export function hosRemaining(entries: HosEntry[] = hosEntries): HosRemaining {
  const todayEntry = entries.find((e) => e.date === today);
  const drivingToday = todayEntry?.drivingH ?? 0;
  const onDutyToday = todayEntry?.onDutyH ?? 0;
  const cycle = entries.slice(0, 7).reduce((sum, e) => sum + e.onDutyH, 0);

  const drivingRemainingH = round2(MAX_DRIVING_H_PER_DAY - drivingToday);
  const onDutyRemainingH = round2(MAX_ON_DUTY_H_PER_DAY - onDutyToday);
  const cycleRemainingH = round2(MAX_CYCLE_H_PER_7_DAYS - cycle);

  const tightest = Math.min(drivingRemainingH, onDutyRemainingH, cycleRemainingH);
  const hk: StatusKind = tightest < 1 ? "over" : tightest < 3 ? "soon" : "ontime";

  return { drivingRemainingH, onDutyRemainingH, cycleRemainingH, hk };
}

// --- inspections (DVIR, NSC Standard 11) -----------------------------------

/**
 * The 22 checklist items, grouped the way NSC Standard 11 is written.
 *
 * Each item carries a STABLE ID as well as its label, and the ids are the load-bearing half.
 * Answers, the persisted draft and the wizard's resume pointer all key on `id`; `label` is both
 * what the driver reads and the `Item` string ChecklistItemInput carries. Before this, answers
 * were keyed by the display string — so two groups sharing an item name would collide, and
 * renaming "Tires and wheels" would orphan every stored draft. Ids follow the file's prefixed
 * convention (TRP-, HOS-, DVR-) with a two-letter group code; lib/inspectionSteps.test.ts pins
 * that they are unique across all five groups.
 */
export const dvirChecklist: ChecklistGroup[] = [
  {
    group: "Under Hood",
    items: [
      { id: "CHK-UH-1", label: "Engine oil level" },
      { id: "CHK-UH-2", label: "Coolant level" },
      { id: "CHK-UH-3", label: "Belts and hoses" },
      { id: "CHK-UH-4", label: "Battery / cables" },
    ],
  },
  {
    group: "Exterior",
    items: [
      { id: "CHK-EX-1", label: "Tires and wheels" },
      { id: "CHK-EX-2", label: "Lamps and reflectors" },
      { id: "CHK-EX-3", label: "Mirrors" },
      { id: "CHK-EX-4", label: "Body and glass" },
      { id: "CHK-EX-5", label: "Wipers" },
    ],
  },
  {
    group: "Brakes & Steering",
    items: [
      { id: "CHK-BS-1", label: "Service brake" },
      { id: "CHK-BS-2", label: "Parking brake" },
      { id: "CHK-BS-3", label: "Air system (if equipped)" },
      { id: "CHK-BS-4", label: "Steering play" },
    ],
  },
  {
    group: "Interior & Safety",
    items: [
      { id: "CHK-IS-1", label: "Seatbelts" },
      { id: "CHK-IS-2", label: "Emergency exits" },
      { id: "CHK-IS-3", label: "Fire extinguisher" },
      { id: "CHK-IS-4", label: "First aid kit" },
      { id: "CHK-IS-5", label: "Heater / defroster" },
    ],
  },
  {
    group: "Winter Readiness",
    items: [
      { id: "CHK-WR-1", label: "Block heater cord" },
      { id: "CHK-WR-2", label: "Traction aids" },
      { id: "CHK-WR-3", label: "Survival kit" },
      { id: "CHK-WR-4", label: "Extra fuel" },
    ],
  },
];

/**
 * Historical submissions. `mode` and `vehicleId` are what the boarding gate reads: `type` is
 * display prose ("Pre-Trip") and `unit` is free text, and neither is a join key or a contract.
 *
 * DVR-8101 is deliberately dated YESTERDAY (2026-09-11), not today. A gate that is already
 * satisfied on first paint cannot be demonstrated — the same argument this file already makes
 * for greying ineligible trips rather than hiding them. lib/inspectionGate.test.ts pins it, so
 * restoring the old date fails a test that says why.
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
    defectCount: 1,
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
    defectCount: 0,
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
    defectCount: 2,
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

function round2(n: number): number {
  return Math.round(n * 100) / 100;
}
