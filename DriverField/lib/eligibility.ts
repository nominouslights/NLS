// ---------------------------------------------------------------------------
// The §5.4 eligibility engine (client-side MIRROR, never the enforcement point).
//
// Moved out of lib/data.ts when the hours-of-service rule went onto the engine: this file reads
// the ledger store (lib/hos/ledger.ts), which is LIVE against the API, and lib/data.ts is the
// read-only mock layer — a mock file importing the live ledger is the wrong direction. The
// other four rules still read the mock arrays exactly as before, and screens keep their props.
//
// The five rules from architecture §5.4, each with the reason a driver reads.
//
// THIS IS A MIRROR OF A RULE THE SERVER MUST OWN. §5.4 is explicit that claiming an Open trip
// must be an atomic, server-validated operation, and that eligibility — especially hours of
// service and clearance status — is re-checked at the moment of claiming, not when the list was
// last loaded. Two drivers must never be able to claim the same trip. The backend's
// DriverFitnessGate (`GET /api/trips/{id}/driver-fitness`) and `POST /api/trips/{id}/claim`
// are the real thing; until they ship, client-side filtering is a UX convenience and never the
// enforcement point.
//
// Same class of thing as Budgeting's previewPeriod() mirroring BudgetPeriod.Create.
//
// §5.4 also says an ineligible driver should never SEE an Open trip. This scaffold greys them
// with the failing rule named instead, because a screen that hides rows cannot demonstrate the
// engine it exists to prove. The real implementation filters server-side — see
// DriverField/CLAUDE.md.
// ---------------------------------------------------------------------------

import { activeClearanceClients, assignedTrips, currentDriver, formatDurationH, vehicles } from "./data";
import { DEFAULT_PLANNING_MARGIN, project, type TripPlan } from "./hos/engine";
import { durationLabel, localIsoToUnix } from "./hos/display";
import { CALENDAR, getSnapshot, type LedgerSnapshot } from "./hos/ledger";
import type { EligibilityVerdict, Trip } from "./types";

/**
 * The plan `project` evaluates for a trip: one leg of driving for the trip's estimate, starting
 * at the trip's start or now, whichever is later. The planning allowances (pre-trip, loading,
 * fuelling, post-trip, weather) are tenant policy on the server (HosPlanningPolicy) and are
 * not re-derived here — the default margin (1h + 10%) is applied by `project` itself.
 */
export function tripPlan(trip: Trip, asOfUnix: number): TripPlan {
  const startUnix = Math.max(localIsoToUnix(trip.startsAt), asOfUnix);
  return {
    legs: [
      {
        startUnix,
        segments: [{ status: "Driving", seconds: Math.round(trip.estimatedHours * 3600) }],
        tripId: trip.id,
      },
    ],
    offDutyFromUnix: null,
  };
}

export function eligibility(trip: Trip, snapshot: LedgerSnapshot = getSnapshot()): EligibilityVerdict {
  const projection = project(snapshot.ledger, snapshot.asOfUnix, tripPlan(trip, snapshot.asOfUnix), CALENDAR, DEFAULT_PLANNING_MARGIN);
  const vehicle = vehicles.find((v) => v.id === trip.vehicleId);
  const claimed = assignedTrips;

  const rules = [
    {
      rule: "Hours of service",
      // HosEngine.Project on the planning ledger: the trip plus the default margin must fit
      // every daily, shift and cycle limit from now, with no rest assumed before the start.
      pass: projection.feasible,
      reason: projection.feasible
        ? `${formatDurationH(trip.estimatedHours)} driving + ${durationLabel(projection.marginAppliedSeconds)} margin fits — ${durationLabel(projection.atPlanEnd.remainingDrivingTodaySeconds)} driving left after`
        : (projection.firstBreach?.message ?? projection.problems[0]?.message ?? "Hours of service cannot be projected"),
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

  return { eligible: rules.every((r) => r.pass), rules };
}

function overlaps(a: Trip, b: Trip): boolean {
  return a.startsAt < b.endsAt && b.startsAt < a.endsAt;
}
