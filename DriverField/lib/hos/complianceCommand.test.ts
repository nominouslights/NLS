import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError } from "../api/transport";
import { drain, enqueue, failedCommands, resetQueueForTests, statusOf } from "../sync/queue";
import {
  ADVERSE_EXTENSION_MAX_MINUTES,
  ADVERSE_EXTENSION_MIN_MINUTES,
  declareExceptionPayload,
  DRIVER_EXCEPTION_KINDS,
  ELD_CODE_MAX,
  eldMalfunctionDraftError,
  eldMalfunctionInputFrom,
  eldMalfunctionPayload,
  EXCEPTION_DECLARATION_WINDOW_SECONDS,
  EXCEPTION_REASON_MAX,
  exceptionDraftError,
  exceptionInputFrom,
  isEldMalfunctionCommandPayload,
  isExceptionCommandPayload,
  otherCarrierDraftError,
  otherCarrierEvents,
  withdrawExceptionPayload,
  type ExceptionDraft,
} from "./complianceCommand";
import { HOS_LIMITS, HosWire } from "./engine";
import { CALENDAR, getSnapshot, resetLedgerForTests, startLedger } from "./ledger";

// The compliance commands: the client-side mirrors of HosException.Declare and
// EldMalfunctionReport.Report (both sides of every bound), the payloads the queue captures, the
// bodies they become at send time, the other-carrier day laid out as duty events, and — through
// the real queue and the ledger's registered senders — a 4xx parking each kind with the server's
// own message.

const { getMyDriver, listDutyDays, getHosStatus, recordDutyEvent, certifyDay, listHosExceptions, declareHosException, withdrawHosException, reportEldMalfunction } =
  vi.hoisted(() => ({
    getMyDriver: vi.fn(),
    listDutyDays: vi.fn(),
    getHosStatus: vi.fn(),
    recordDutyEvent: vi.fn(),
    certifyDay: vi.fn(),
    listHosExceptions: vi.fn(),
    declareHosException: vi.fn(),
    withdrawHosException: vi.fn(),
    reportEldMalfunction: vi.fn(),
  }));

vi.mock("../api/hos", () => ({
  getMyDriver,
  listDutyDays,
  getHosStatus,
  recordDutyEvent,
  certifyDay,
  listHosExceptions,
  declareHosException,
  withdrawHosException,
  reportEldMalfunction,
}));

const H = 3600;
const DRIVER = "11111111-1111-1111-1111-111111111111";
/** 15:00 CDT on 2026-10-10. */
const NOW = Date.parse("2026-10-10T20:00:00.000Z") / 1000;
const TODAY = "2026-10-10";

function adverse(fields: Partial<ExceptionDraft> = {}): ExceptionDraft {
  return {
    kind: HosWire.AdverseDrivingConditionsKind as ExceptionDraft["kind"],
    effectiveFromUnix: NOW,
    reason: "Whiteout since km 40",
    location: "Highway 391",
    notKnownBeforeDeparture: true,
    extraDrivingMinutes: "60",
    eldRemarkRecorded: false,
    ...fields,
  };
}

function emergency(fields: Partial<ExceptionDraft> = {}): ExceptionDraft {
  return adverse({ kind: HosWire.EmergencyKind as ExceptionDraft["kind"], notKnownBeforeDeparture: false, extraDrivingMinutes: "", reason: "EMS on board", ...fields });
}

beforeEach(async () => {
  resetLedgerForTests();
  await resetQueueForTests({ wipe: true });
});

afterEach(() => {
  vi.clearAllMocks();
});

describe("exceptionDraftError — HosException.Declare, on the tablet", () => {
  it("offers exactly the two driver-declarable kinds, spelled as HosWire does", () => {
    expect([...DRIVER_EXCEPTION_KINDS]).toEqual(["Adverse driving conditions", "Emergency"]);
    expect(DRIVER_EXCEPTION_KINDS).not.toContain(HosWire.OffDutyDeferralKind);
  });

  it("requires the s.76 knowability attestation for adverse conditions (AdverseKnowabilityRequired)", () => {
    expect(exceptionDraftError(adverse({ notKnownBeforeDeparture: false }), NOW)).toMatch(/could not reasonably have been known before departure/);
    expect(exceptionDraftError(adverse(), NOW)).toBeNull();
    // An emergency has no such attestation.
    expect(exceptionDraftError(emergency(), NOW)).toBeNull();
  });

  it("bounds the adverse extension at 1 and 120 minutes, both sides (AdverseExtensionRequired / TooLong)", () => {
    expect(ADVERSE_EXTENSION_MIN_MINUTES).toBe(1);
    expect(ADVERSE_EXTENSION_MAX_MINUTES).toBe(HOS_LIMITS.adverseMaxExtensionSeconds / 60);
    expect(ADVERSE_EXTENSION_MAX_MINUTES).toBe(120);
    expect(exceptionDraftError(adverse({ extraDrivingMinutes: "0" }), NOW)).toMatch(/at least 1 minute/);
    expect(exceptionDraftError(adverse({ extraDrivingMinutes: "" }), NOW)).toMatch(/at least 1 minute/);
    expect(exceptionDraftError(adverse({ extraDrivingMinutes: "1" }), NOW)).toBeNull();
    expect(exceptionDraftError(adverse({ extraDrivingMinutes: "120" }), NOW)).toBeNull();
    expect(exceptionDraftError(adverse({ extraDrivingMinutes: "121" }), NOW)).toMatch(/at most 120 minutes/);
    expect(exceptionDraftError(adverse({ extraDrivingMinutes: "1.5" }), NOW)).toMatch(/at least 1 minute/);
  });

  it("requires a reason of at most 500 characters (ExceptionReasonRequired / TooLong)", () => {
    expect(exceptionDraftError(adverse({ reason: "   " }), NOW)).toBe("A reason is required.");
    expect(exceptionDraftError(emergency({ reason: "" }), NOW)).toBe("Say why you had to stop.");
    expect(exceptionDraftError(adverse({ reason: "x".repeat(EXCEPTION_REASON_MAX) }), NOW)).toBeNull();
    expect(exceptionDraftError(adverse({ reason: "x".repeat(EXCEPTION_REASON_MAX + 1) }), NOW)).toMatch(/at most 500 characters/);
  });

  it("holds the effective-from inside the 24h declaration window and five minutes of skew, both sides (ExceptionBackdated / InFuture)", () => {
    expect(EXCEPTION_DECLARATION_WINDOW_SECONDS).toBe(24 * H);
    expect(exceptionDraftError(adverse({ effectiveFromUnix: NOW - 24 * H }), NOW)).toBeNull();
    expect(exceptionDraftError(adverse({ effectiveFromUnix: NOW - 24 * H - 1 }), NOW)).toMatch(/within 24 hours/);
    expect(exceptionDraftError(adverse({ effectiveFromUnix: NOW + HOS_LIMITS.futureSkewSeconds }), NOW)).toBeNull();
    expect(exceptionDraftError(adverse({ effectiveFromUnix: NOW + HOS_LIMITS.futureSkewSeconds + 1 }), NOW)).toMatch(/in the future/);
  });
});

describe("the hos.exception payload and its body", () => {
  it("captures a declaration with the wire kind, ISO effective-from, and the adverse fields only for adverse", () => {
    const payload = declareExceptionPayload(adverse(), NOW);
    expect(payload).toEqual({
      action: "declare",
      kind: "Adverse driving conditions",
      effectiveFromUtc: "2026-10-10T20:00:00.000Z",
      reason: "Whiteout since km 40",
      location: "Highway 391",
      notKnownBeforeDeparture: true,
      extraDrivingMinutes: 60,
      eldRemarkRecorded: false,
      tripId: null,
    });
    const em = declareExceptionPayload(emergency({ location: "  " }), NOW);
    expect(em).toMatchObject({ kind: "Emergency", notKnownBeforeDeparture: false, extraDrivingMinutes: null, location: null });
    expect(() => declareExceptionPayload(adverse({ notKnownBeforeDeparture: false }), NOW)).toThrow(/s\.76/);
  });

  it("maps onto DeclareHosExceptionRequest at send time, and refuses anything that is not a declaration", () => {
    const body = exceptionInputFrom(declareExceptionPayload(adverse(), NOW));
    expect(body).toEqual({
      kind: "Adverse driving conditions",
      effectiveFromUtc: "2026-10-10T20:00:00.000Z",
      reason: "Whiteout since km 40",
      location: "Highway 391",
      tripId: null,
      notKnownBeforeDeparture: true,
      extraDrivingMinutes: 60,
      eldRemarkRecorded: false,
    });
    expect(() => exceptionInputFrom(withdrawExceptionPayload("exc-1"))).toThrow(/Not an exception declaration/);
    expect(() => exceptionInputFrom({ kind: "Off-duty deferral (CO approved)", action: "declare", effectiveFromUtc: "x", reason: "r" })).toThrow();
  });

  it("captures a withdrawal with the server's exception id and an optional note", () => {
    expect(withdrawExceptionPayload("exc-1")).toEqual({ action: "withdraw", exceptionId: "exc-1", note: null });
    expect(withdrawExceptionPayload("exc-1", " conditions cleared ")).toEqual({ action: "withdraw", exceptionId: "exc-1", note: "conditions cleared" });
    expect(isExceptionCommandPayload(withdrawExceptionPayload("exc-1"))).toBe(true);
    expect(isExceptionCommandPayload({ action: "withdraw" })).toBe(false);
    expect(isExceptionCommandPayload({ action: "declare", kind: "Off-duty deferral (CO approved)", effectiveFromUtc: "x", reason: "r" })).toBe(false);
  });
});

describe("eldMalfunctionDraftError — EldMalfunctionReport.Report, on the tablet", () => {
  const draft = { code: "P", noticedAtUnix: NOW - 600, description: "Screen frozen" };

  it("requires a code of at most 32 characters and a description, both sides", () => {
    expect(ELD_CODE_MAX).toBe(32);
    expect(eldMalfunctionDraftError({ ...draft, code: " " }, NOW)).toMatch(/code the ELD displayed is required/);
    expect(eldMalfunctionDraftError({ ...draft, code: "x".repeat(32) }, NOW)).toBeNull();
    expect(eldMalfunctionDraftError({ ...draft, code: "x".repeat(33) }, NOW)).toMatch(/at most 32 characters/);
    expect(eldMalfunctionDraftError({ ...draft, description: "" }, NOW)).toMatch(/description of the malfunction is required/);
  });

  it("refuses a noticed-at more than five minutes ahead (EldNoticedInFuture), both sides", () => {
    expect(eldMalfunctionDraftError({ ...draft, noticedAtUnix: NOW + HOS_LIMITS.futureSkewSeconds }, NOW)).toBeNull();
    expect(eldMalfunctionDraftError({ ...draft, noticedAtUnix: NOW + HOS_LIMITS.futureSkewSeconds + 1 }, NOW)).toMatch(/in the future/);
    expect(eldMalfunctionDraftError({ ...draft, noticedAtUnix: Number.NaN }, NOW)).toMatch(/when you noticed/);
  });

  it("captures and maps the report, trimming and never inventing a vehicle id", () => {
    const payload = eldMalfunctionPayload({ ...draft, code: " P " }, NOW);
    expect(payload).toEqual({ code: "P", noticedAtUtc: "2026-10-10T19:50:00.000Z", description: "Screen frozen", vehicleId: null });
    expect(isEldMalfunctionCommandPayload(payload)).toBe(true);
    expect(eldMalfunctionInputFrom(payload)).toEqual({ code: "P", noticedAtUtc: "2026-10-10T19:50:00.000Z", description: "Screen frozen", vehicleId: null });
    expect(() => eldMalfunctionInputFrom({ code: "P" })).toThrow(/Not an ELD malfunction/);
  });
});

describe("other-carrier hours as duty events", () => {
  const draft = { date: "2026-10-09", carrierName: "Gardewine North", onDutyMinutes: "480", drivingMinutes: "300" };

  it("lays out OnDuty → Driving → OffDuty from the regulation day's start, driving last inside the on-duty time", () => {
    const events = otherCarrierEvents(draft, TODAY, CALENDAR);
    const start = CALENDAR.dayStartUnix("2026-10-09");
    expect(events.map((e) => [e.status, e.atUnix - start])).toEqual([
      ["OnDuty", 0],
      ["Driving", 180 * 60],
      ["OffDuty", 480 * 60],
    ]);
    expect(events.map((e) => e.atUtc)).toEqual(events.map((e) => new Date(e.atUnix * 1000).toISOString()));
    expect(CALENDAR.dayOf(events[0].atUnix)).toBe("2026-10-09");
  });

  it("lays out two events for a day with no driving — never a zero-length Driving segment", () => {
    expect(otherCarrierEvents({ ...draft, drivingMinutes: "0" }, TODAY, CALENDAR).map((e) => e.status)).toEqual(["OnDuty", "OffDuty"]);
  });

  it("refuses a missing carrier (CarrierNameRequired), driving beyond on-duty, more than a day, a future day, or one older than 14 days", () => {
    expect(otherCarrierDraftError(draft, TODAY)).toBeNull();
    expect(otherCarrierDraftError({ ...draft, carrierName: " " }, TODAY)).toMatch(/carrier name is required/);
    expect(otherCarrierDraftError({ ...draft, drivingMinutes: "481" }, TODAY)).toMatch(/cannot exceed it/);
    expect(otherCarrierDraftError({ ...draft, onDutyMinutes: "1441" }, TODAY)).toMatch(/24 hours/);
    expect(otherCarrierDraftError({ ...draft, onDutyMinutes: "1440" }, TODAY)).toBeNull();
    expect(otherCarrierDraftError({ ...draft, onDutyMinutes: "0" }, TODAY)).toMatch(/at least one minute/);
    expect(otherCarrierDraftError({ ...draft, onDutyMinutes: "8h" }, TODAY)).toMatch(/whole minutes/);
    expect(otherCarrierDraftError({ ...draft, date: "2026-10-11" }, TODAY)).toMatch(/future/);
    expect(otherCarrierDraftError({ ...draft, date: "2026-09-27" }, TODAY)).toBeNull();
    expect(otherCarrierDraftError({ ...draft, date: "2026-09-26" }, TODAY)).toMatch(/last 14 days/);
    expect(() => otherCarrierEvents({ ...draft, carrierName: "" }, TODAY, CALENDAR)).toThrow();
  });
});

describe("the two senders, through the real queue", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date(NOW * 1000));
    getMyDriver.mockResolvedValue({ id: DRIVER, name: "R. Beardy" });
    listDutyDays.mockResolvedValue([]);
    getHosStatus.mockResolvedValue({
      driverId: DRIVER,
      label: "Planning figures",
      currentDuty: "Off Duty",
      currentDutySinceUtc: "2026-10-10T20:00:00.000Z",
      recordsOnFile: false,
      knownFromUtc: null,
      outOfService: null,
      eldMalfunctionOpen: false,
      priorRecords: null,
      evaluation: {},
    });
    listHosExceptions.mockResolvedValue([]);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  async function started(): Promise<() => void> {
    const stop = startLedger("user-1");
    await vi.waitFor(() => expect(getSnapshot().hydration.source).toBe("server"));
    return stop;
  }

  it("sends a declaration to POST …/hos/exceptions with the command id as the Idempotency-Key, and parks a 4xx with the server's message", async () => {
    const stop = await started();
    declareHosException.mockResolvedValueOnce({ id: "exc-1", status: "Declared" });
    const ok = await enqueue("hos.exception", declareExceptionPayload(adverse(), NOW));
    await drain();
    expect(statusOf(ok)).toBe("sent");
    expect(declareHosException).toHaveBeenCalledWith(DRIVER, exceptionInputFrom(declareExceptionPayload(adverse(), NOW)), { idempotencyKey: ok });

    const message = "Adverse driving conditions extend the limits by at most two hours (s.76).";
    declareHosException.mockRejectedValueOnce(new ApiError("Drivers.Hos.AdverseExtensionTooLong", message, 400));
    const refused = await enqueue("hos.exception", declareExceptionPayload(adverse(), NOW));
    await drain();
    expect(statusOf(refused)).toBe("failed");
    expect(failedCommands()[0]).toMatchObject({ id: refused, error: message, errorCode: "Drivers.Hos.AdverseExtensionTooLong" });
    stop();
  });

  it("sends a withdrawal to POST …/exceptions/{id}/withdraw, and a 403 for one no longer Declared parks it", async () => {
    const stop = await started();
    withdrawHosException.mockResolvedValueOnce({ id: "exc-1", status: "Withdrawn" });
    const ok = await enqueue("hos.exception", withdrawExceptionPayload("exc-1", "cleared"));
    await drain();
    expect(statusOf(ok)).toBe("sent");
    expect(withdrawHosException).toHaveBeenCalledWith(DRIVER, "exc-1", "cleared", { idempotencyKey: ok });

    const message = "Only a compliance officer may withdraw an exception that has already been acknowledged or approved.";
    withdrawHosException.mockRejectedValueOnce(new ApiError("Drivers.Hos.ExceptionWithdrawRequiresComplianceOfficer", message, 403));
    const refused = await enqueue("hos.exception", withdrawExceptionPayload("exc-2"));
    await drain();
    expect(failedCommands().map((c) => [c.id, c.error])).toEqual([[refused, message]]);
    stop();
  });

  it("sends a malfunction report to POST …/hos/eld-malfunctions, and parks a 4xx with the server's message", async () => {
    const stop = await started();
    const payload = eldMalfunctionPayload({ code: "P", noticedAtUnix: NOW - 60, description: "Frozen" }, NOW);
    reportEldMalfunction.mockResolvedValueOnce({ id: "eld-1", status: "Open" });
    const ok = await enqueue("hos.eld-malfunction", payload);
    await drain();
    expect(statusOf(ok)).toBe("sent");
    expect(reportEldMalfunction).toHaveBeenCalledWith(DRIVER, eldMalfunctionInputFrom(payload), { idempotencyKey: ok });

    const message = "The malfunction code must be at most 32 characters.";
    reportEldMalfunction.mockRejectedValueOnce(new ApiError("Drivers.Hos.EldCodeTooLong", message, 400));
    const refused = await enqueue("hos.eld-malfunction", payload);
    await drain();
    expect(statusOf(refused)).toBe("failed");
    expect(failedCommands()[0]).toMatchObject({ id: refused, error: message, errorCode: "Drivers.Hos.EldCodeTooLong" });
    stop();
  });

  it("parks a payload that is not the kind's shape with that reason rather than sending junk", async () => {
    const stop = await started();
    const id = await enqueue("hos.eld-malfunction", { tripId: "TRP-1" });
    await drain();
    expect(reportEldMalfunction).not.toHaveBeenCalled();
    // A thrown non-ApiError is "try again later" by the queue's contract — still pending, never sent.
    expect(statusOf(id)).toBe("pending");
    stop();
  });
});
