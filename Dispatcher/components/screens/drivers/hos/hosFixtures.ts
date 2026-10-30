import type { DutyDaySummary, DutyEventRecord, EldMalfunctionRecord, HosExceptionRecord, HosStatus, OutOfServiceRecord } from "@/lib/api/hos";
import type { DayTotals, HosEvaluation } from "@/lib/hos/engine";

// Test fixtures for the HOS tab components: a compliant evaluation shaped exactly like the
// server's HosStatusResponse (the engine mirror's HosEvaluation nested verbatim), and a
// duty day shaped like DutyDaySummaryResponse. Tests override only what they assert on.

export const FIXTURE_DATE = "2026-10-09";
/** 2026-10-09 00:00 CDT (UTC-5). */
export const FIXTURE_DAY_START = Date.UTC(2026, 9, 9, 5) / 1000;
/** 14:00 local on the fixture day. */
export const FIXTURE_AS_OF = FIXTURE_DAY_START + 14 * 3600;

export function dayTotals(overrides: Partial<DayTotals> = {}): DayTotals {
  return {
    date: FIXTURE_DATE,
    known: true,
    complete: false,
    drivingSeconds: 5 * 3600,
    onDutySeconds: 7 * 3600,
    otherCarrierOnDutySeconds: 0,
    offDutySeconds: 7 * 3600,
    rawOffDutySeconds: 7 * 3600,
    pcDecikm: 0,
    hasOwnEvents: true,
    certified: false,
    eightHourConsecutiveMet: true,
    dailyOffRequiredSeconds: 10 * 3600,
    offDutyShortfallSeconds: 0,
    dailyOffMet: false,
    ...overrides,
  };
}

export function evaluation(overrides: Partial<HosEvaluation> = {}): HosEvaluation {
  const today = dayTotals();
  return {
    asOfUnix: FIXTURE_AS_OF,
    currentStatus: "Driving",
    currentStatusSinceUnix: FIXTURE_DAY_START + 8 * 3600,
    days: [today],
    today,
    remainingDrivingTodaySeconds: 8 * 3600,
    remainingOnDutyTodaySeconds: 7 * 3600,
    shift: {
      startUnix: FIXTURE_DAY_START + 7 * 3600,
      drivingSeconds: 5 * 3600,
      onDutySeconds: 7 * 3600,
      elapsedSeconds: 7 * 3600,
      drivingCeilingSeconds: 13 * 3600,
      onDutyCeilingSeconds: 14 * 3600,
      elapsedCeilingSeconds: 16 * 3600,
      adverseExtensionSeconds: 0,
      mustStopDrivingByUnix: FIXTURE_DAY_START + 21 * 3600,
    },
    cycle: {
      onDutySeconds: 30 * 3600,
      limitSeconds: 70 * 3600,
      remainingSeconds: 40 * 3600,
      windowStartDate: "2026-10-03",
      lastResetEndUnix: null,
      recordsIncomplete: false,
    },
    rest24: {
      satisfied: true,
      qualifyingRunEndUnix: FIXTURE_DAY_START - 2 * 86400,
      expiresAtUnix: FIXTURE_DAY_START + 10 * 86400,
      bestOffSeconds: 30 * 3600,
      recordsIncomplete: false,
    },
    pcTodayDecikm: 0,
    pcRemainingDecikm: 750,
    violations: [],
    canDriveNow: true,
    blockingReasons: [],
    nextRequiredOffDuty: {
      mustBeOffByUnix: FIXTURE_DAY_START + 21 * 3600,
      reason: "ShiftOnDuty14h",
      minimumSeconds: 8 * 3600,
      dailyShortfallSeconds: 0,
    },
    tightestRemainingSeconds: 7 * 3600,
    band: "Ontime",
    ledgerProblems: [],
    ...overrides,
  };
}

export function hosStatus(overrides: Partial<HosStatus> = {}, evaluationOverrides: Partial<HosEvaluation> = {}): HosStatus {
  return {
    driverId: "driver-1",
    label: "Planning figures, not the record of duty status — RODS is on the bus's ELD.",
    currentDuty: "Driving",
    currentDutySinceUtc: new Date((FIXTURE_DAY_START + 8 * 3600) * 1000).toISOString(),
    recordsOnFile: true,
    knownFromUtc: "2026-09-01T05:00:00+00:00",
    outOfService: null,
    eldMalfunctionOpen: false,
    priorRecords: null,
    evaluation: evaluation(evaluationOverrides),
    ...overrides,
  };
}

/** An out-of-service declaration in force: declared 06:00 on the fixture day, 72h. */
export function outOfService(overrides: Partial<OutOfServiceRecord> = {}): OutOfServiceRecord {
  return {
    id: "oos-1",
    fromUtc: new Date((FIXTURE_DAY_START + 6 * 3600) * 1000).toISOString(),
    untilUtc: new Date((FIXTURE_DAY_START + 78 * 3600) * 1000).toISOString(),
    reason: "Roadside inspection — hours exceeded",
    ...overrides,
  };
}

/** A declared adverse-conditions exception from 10:00 on the fixture day, +60 min. */
export function hosException(overrides: Partial<HosExceptionRecord> = {}): HosExceptionRecord {
  const at = new Date((FIXTURE_DAY_START + 10 * 3600) * 1000).toISOString();
  return {
    id: "ex-1",
    driverId: "driver-1",
    kind: "Adverse driving conditions",
    status: "Declared",
    applying: true,
    tripId: null,
    declaredAtUtc: at,
    declaredByUserId: "user-1",
    effectiveFromUtc: at,
    effectiveToUtc: null,
    reason: "Whiteout on PTH 6",
    location: "North of Grand Rapids",
    notKnownBeforeDeparture: true,
    extraDrivingMinutes: 60,
    deferredOffDutyMinutes: null,
    eldRemarkRecorded: true,
    dispatchAcknowledgedAtUtc: null,
    dispatchAcknowledgedByUserId: null,
    approvedAtUtc: null,
    approvedByUserId: null,
    closedAtUtc: null,
    closedByUserId: null,
    closureNote: null,
    ...overrides,
  };
}

/** An open ELD malfunction noticed 07:00 on the fixture day. */
export function eldMalfunction(overrides: Partial<EldMalfunctionRecord> = {}): EldMalfunctionRecord {
  const at = new Date((FIXTURE_DAY_START + 7 * 3600) * 1000).toISOString();
  return {
    id: "eld-1",
    driverId: "driver-1",
    vehicleId: null,
    code: "P-01",
    status: "Open",
    noticedAtUtc: at,
    reportedAtUtc: at,
    reportedByUserId: "user-1",
    description: "Display frozen; no duty changes recorded since 07:00.",
    resolvedAtUtc: null,
    resolvedByUserId: null,
    resolutionNote: null,
    ...overrides,
  };
}

export function dutyEvent(overrides: Partial<DutyEventRecord> = {}): DutyEventRecord {
  return {
    eventId: "e-1",
    sequence: 1,
    atUtc: new Date((FIXTURE_DAY_START + 7 * 3600) * 1000).toISOString(),
    recordedAtUtc: new Date((FIXTURE_DAY_START + 7 * 3600) * 1000).toISOString(),
    status: "On Duty",
    source: "Driver App",
    recordedByUserId: null,
    enteredBy: null,
    carrierName: null,
    note: null,
    odometerStartKm: null,
    odometerEndKm: null,
    pcNoPassengersOrCargo: false,
    pcNoTrailer: false,
    pcNoWork: false,
    amendsEventId: null,
    amendmentReason: null,
    isVoided: false,
    ...overrides,
  };
}

export function dutyDay(overrides: Partial<DutyDaySummary> = {}): DutyDaySummary {
  const onDuty = dutyEvent();
  const driving = dutyEvent({ eventId: "e-2", sequence: 2, atUtc: new Date((FIXTURE_DAY_START + 8 * 3600) * 1000).toISOString(), status: "Driving" });
  const off = dutyEvent({ eventId: "e-3", sequence: 3, atUtc: new Date((FIXTURE_DAY_START + 14 * 3600) * 1000).toISOString(), status: "Off Duty" });
  return {
    driverId: "driver-1",
    date: FIXTURE_DATE,
    hasRecords: true,
    dutyDayId: "dd-1",
    totals: dayTotals({ complete: true, offDutySeconds: 17 * 3600, dailyOffMet: true }),
    personalConveyanceKm: 0,
    certified: false,
    certifiedAtUtc: null,
    recertificationRequired: false,
    segments: [
      { status: "On Duty", source: "Driver App", fromUtc: onDuty.atUtc, toUtc: driving.atUtc, durationSeconds: 3600, eventId: onDuty.eventId },
      { status: "Driving", source: "Driver App", fromUtc: driving.atUtc, toUtc: off.atUtc, durationSeconds: 6 * 3600, eventId: driving.eventId },
    ],
    events: [onDuty, driving, off],
    violations: [],
    exceptions: [],
    outOfService: null,
    label: "Planning figures, not the record of duty status — RODS is on the bus's ELD.",
    ...overrides,
  };
}
