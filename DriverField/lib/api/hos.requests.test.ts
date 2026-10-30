import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  certifyDay,
  declareHosException,
  getHosStatus,
  getMyDriver,
  listDutyDays,
  listEldMalfunctions,
  listHosExceptions,
  recordDutyEvent,
  reportEldMalfunction,
  withdrawHosException,
  type DutyStatusWire,
  type EventSourceWire,
  type HosExceptionKindWire,
  type HosExceptionStatus,
} from "./hos";
import { ApiError } from "./transport";
import { DUTY_STATUSES, EVENT_SOURCES, HosWire } from "../hos/engine";

// What actually goes on the wire for the HOS ledger, from the tablet. The routes and bodies are
// the Drivers module's contract (HosEndpoints.cs / HosResponses.cs / DriversEndpoints.cs); a
// wrong route is a 404 the driver would read as "record not found", and a missing
// Idempotency-Key makes a retried duty change a duplicate rather than a replay. Mirrors
// Dispatcher/lib/api/hos.requests.test.ts for the shared members, plus the two this app owns.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("../auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

const callPath = (n = 0) => String(fetchMock.mock.calls[n]?.[0]);
const callInit = (n = 0) => (fetchMock.mock.calls[n]?.[1] ?? {}) as RequestInit;
const headersOf = (n = 0) => (callInit(n).headers ?? {}) as Record<string, string>;
const bodyOf = (n = 0) => JSON.parse(String(callInit(n).body)) as unknown;

const DRIVER = "11111111-1111-1111-1111-111111111111";
const EVENT = "22222222-2222-2222-2222-222222222222";

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("wire string unions", () => {
  it("DutyStatusWire is exactly HosWire's four duty strings", () => {
    const statuses: DutyStatusWire[] = DUTY_STATUSES.map((s) => HosWire.status(s) as DutyStatusWire);
    expect(statuses).toEqual(["Off Duty", "On Duty", "Driving", "Personal Conveyance"]);
  });

  it("EventSourceWire is exactly HosWire's five source strings", () => {
    const sources: EventSourceWire[] = EVENT_SOURCES.map((s) => HosWire.source(s) as EventSourceWire);
    expect(sources).toEqual(["Driver App", "ELD transcription", "Manual (paper backup)", "Other carrier (declared)", "System"]);
  });
});

describe("getMyDriver — the Field App's first call", () => {
  it("GETs /api/drivers/me with the bearer token and no driver id in the path", async () => {
    // The id comes from the token's sub on the server; there is nothing for the client to
    // supply and nothing for it to tamper with.
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: DRIVER, userId: "u", name: "R. Beardy", licenceClass: "4", status: "Active" }));

    const me = await getMyDriver();

    expect(callPath()).toBe("/api/drivers/me");
    expect(callInit().method).toBeUndefined();
    expect(headersOf().Authorization).toBe("Bearer access-1");
    expect(me.id).toBe(DRIVER);
  });

  it("surfaces 404 Drivers.NotLinked with the server's message — the 'ask dispatch' case", async () => {
    const message = "This account is not linked to a driver record.";
    fetchMock.mockResolvedValueOnce(jsonResponse(404, { code: "Drivers.NotLinked", message }));

    const err = await getMyDriver().catch((e) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(404);
    expect((err as ApiError).code).toBe("Drivers.NotLinked");
    expect((err as ApiError).message).toBe(message);
  });
});

describe("reads", () => {
  it("listDutyDays GETs …/hos/days with from/to when given, bare when not", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [])).mockResolvedValueOnce(jsonResponse(200, []));

    await listDutyDays(DRIVER, { from: "2026-10-01", to: "2026-10-31" });
    await listDutyDays(DRIVER);

    expect(callPath(0)).toBe(`/api/drivers/${DRIVER}/hos/days?from=2026-10-01&to=2026-10-31`);
    expect(callInit(0).method).toBeUndefined();
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/hos/days`);
    expect(headersOf(0).Authorization).toBe("Bearer access-1");
  });

  it("getHosStatus GETs …/hos/status and returns the evaluation nested verbatim", async () => {
    const evaluation = { asOfUnix: 1, band: "Ontime", blockingReasons: [], violations: [] };
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { driverId: DRIVER, label: "L", evaluation }));

    const status = await getHosStatus(DRIVER);

    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/status`);
    expect(status.evaluation).toEqual(evaluation);
  });

  it("surfaces a 403 Drivers.NotYourRecord with the server's message — the own-record check", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(403, { code: "Drivers.NotYourRecord", message: "You may only access your own driver record." }));
    const err = await getHosStatus(DRIVER).catch((e) => e);
    expect((err as ApiError).status).toBe(403);
    expect((err as ApiError).code).toBe("Drivers.NotYourRecord");
    expect((err as ApiError).message).toBe("You may only access your own driver record.");
  });
});

describe("recordDutyEvent", () => {
  it("POSTs the body as JSON and repeats the eventId as the Idempotency-Key header", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { eventId: EVENT, dutyDayId: "d", localDate: "2026-10-09", created: true }));
    const input = {
      eventId: EVENT,
      atUtc: "2026-10-09T13:00:00.000Z",
      status: "Driving" as const,
      source: "Driver App" as const,
    };

    const res = await recordDutyEvent(DRIVER, input);

    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/duty-events`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual(input);
    expect(headersOf()["Idempotency-Key"]).toBe(EVENT);
    expect(headersOf()["Content-Type"]).toBe("application/json");
    expect(res.created).toBe(true);
  });

  it("a 200 replay resolves with created=false rather than throwing", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { eventId: EVENT, dutyDayId: "d", localDate: "2026-10-09", created: false }));
    const res = await recordDutyEvent(DRIVER, { eventId: EVENT, atUtc: "2026-10-09T13:00:00Z", status: "Off Duty", source: "Driver App" });
    expect(res.created).toBe(false);
  });

  it("surfaces the server's 400 message verbatim as an ApiError", async () => {
    const message = "The event's instant falls outside the duty day it was recorded against (America/Winnipeg local date).";
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { code: "Drivers.Hos.EventOutsideDay", message }));

    const err = await recordDutyEvent(DRIVER, { eventId: EVENT, atUtc: "2026-10-09T13:00:00Z", status: "Driving", source: "Driver App" }).catch((e) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(400);
    expect((err as ApiError).code).toBe("Drivers.Hos.EventOutsideDay");
    expect((err as ApiError).message).toBe(message);
  });
});

describe("certifyDay", () => {
  it("POSTs …/hos/days/{date}/certify with no body and resolves on 204", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(certifyDay(DRIVER, "2026-10-09")).resolves.toBeUndefined();

    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/days/2026-10-09/certify`);
    expect(callInit().method).toBe("POST");
    expect(callInit().body).toBeUndefined();
    expect(headersOf().Authorization).toBe("Bearer access-1");
  });

  it("surfaces PcSegmentOpen (400) and DayNotFound (404) with the server's sentences", async () => {
    const pcMessage = "The day ends in personal conveyance with no closing odometer; record the end of the segment before certifying.";
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { code: "Drivers.Hos.PcSegmentOpen", message: pcMessage }));
    const pc = await certifyDay(DRIVER, "2026-10-09").catch((e) => e);
    expect((pc as ApiError).status).toBe(400);
    expect((pc as ApiError).code).toBe("Drivers.Hos.PcSegmentOpen");
    expect((pc as ApiError).message).toBe(pcMessage);

    fetchMock.mockResolvedValueOnce(jsonResponse(404, { code: "Drivers.Hos.DayNotFound", message: "No duty record exists for that driver on that day." }));
    const missing = await certifyDay(DRIVER, "2026-10-01").catch((e) => e);
    expect((missing as ApiError).status).toBe(404);
    expect((missing as ApiError).code).toBe("Drivers.Hos.DayNotFound");
  });

  it("surfaces 403 CertifyDriverOnly — dispatch cannot certify on a driver's behalf", async () => {
    const message = "Only the driver may certify their own record of duty status; dispatch cannot certify on a driver's behalf.";
    fetchMock.mockResolvedValueOnce(jsonResponse(403, { code: "Drivers.Hos.CertifyDriverOnly", message }));
    const err = await certifyDay(DRIVER, "2026-10-09").catch((e) => e);
    expect((err as ApiError).status).toBe(403);
    expect((err as ApiError).message).toBe(message);
  });
});

describe("exceptions and ELD malfunctions — the compliance routes under HosAccess", () => {
  it("pins the kind and status unions to HosWire.ExceptionKind and the HosExceptionStatus members", () => {
    const kinds: HosExceptionKindWire[] = [HosWire.AdverseDrivingConditionsKind, HosWire.EmergencyKind, HosWire.OffDutyDeferralKind] as HosExceptionKindWire[];
    expect(kinds).toEqual(["Adverse driving conditions", "Emergency", "Off-duty deferral (CO approved)"]);
    const statuses: HosExceptionStatus[] = ["Declared", "Acknowledged", "Approved", "Rejected", "Withdrawn"];
    expect(statuses).toHaveLength(5);
  });

  it("GETs …/hos/exceptions with the optional range", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));
    await listHosExceptions(DRIVER);
    expect(callPath(0)).toBe(`/api/drivers/${DRIVER}/hos/exceptions`);
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));
    await listHosExceptions(DRIVER, { from: "2026-09-19", to: "2026-10-10" });
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/hos/exceptions?from=2026-09-19&to=2026-10-10`);
  });

  it("POSTs a declaration as DeclareHosExceptionRequest, with the command id as Idempotency-Key when given", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: EVENT, status: "Declared" }));
    const input = {
      kind: "Adverse driving conditions" as const,
      effectiveFromUtc: "2026-10-10T20:00:00.000Z",
      reason: "Whiteout",
      location: null,
      tripId: null,
      notKnownBeforeDeparture: true,
      extraDrivingMinutes: 60,
      eldRemarkRecorded: false,
    };
    const record = await declareHosException(DRIVER, input, { idempotencyKey: "cmd-1" });
    expect(record.id).toBe(EVENT);
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/exceptions`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual(input);
    expect(headersOf()["Idempotency-Key"]).toBe("cmd-1");

    // Without a key, no header — the console's call shape.
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: EVENT, status: "Declared" }));
    await declareHosException(DRIVER, input);
    expect(headersOf(1)["Idempotency-Key"]).toBeUndefined();
  });

  it("surfaces the declaration's 400s verbatim — AdverseKnowabilityRequired, AdverseExtensionTooLong, ExceptionBackdated", async () => {
    const cases: [string, string][] = [
      ["Drivers.Hos.AdverseKnowabilityRequired", "Adverse driving conditions can only be declared when they could not reasonably have been known before departure (s.76)."],
      ["Drivers.Hos.AdverseExtensionTooLong", "Adverse driving conditions extend the limits by at most two hours (s.76)."],
      ["Drivers.Hos.ExceptionBackdated", "An adverse-conditions or emergency exception must be declared within 24 hours of the conditions arising."],
    ];
    for (const [code, message] of cases) {
      fetchMock.mockResolvedValueOnce(jsonResponse(400, { code, message }));
      const err = await declareHosException(DRIVER, { kind: "Emergency", effectiveFromUtc: "x", reason: "r" }).catch((e) => e);
      expect((err as ApiError).status).toBe(400);
      expect((err as ApiError).code).toBe(code);
      expect((err as ApiError).message).toBe(message);
    }
  });

  it("POSTs a withdrawal to …/exceptions/{id}/withdraw with the note body, and surfaces the 403 for one no longer Declared", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: EVENT, status: "Withdrawn" }));
    await withdrawHosException(DRIVER, EVENT, "cleared", { idempotencyKey: "cmd-2" });
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/exceptions/${EVENT}/withdraw`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual({ note: "cleared" });
    expect(headersOf()["Idempotency-Key"]).toBe("cmd-2");

    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: EVENT, status: "Withdrawn" }));
    await withdrawHosException(DRIVER, EVENT);
    expect(bodyOf(1)).toEqual({ note: null });

    const message = "Only a compliance officer may withdraw an exception that has already been acknowledged or approved.";
    fetchMock.mockResolvedValueOnce(jsonResponse(403, { code: "Drivers.Hos.ExceptionWithdrawRequiresComplianceOfficer", message }));
    const err = await withdrawHosException(DRIVER, EVENT).catch((e) => e);
    expect((err as ApiError).status).toBe(403);
    expect((err as ApiError).message).toBe(message);
  });

  it("GETs …/hos/eld-malfunctions and POSTs a report as ReportEldMalfunctionRequest", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));
    await listEldMalfunctions(DRIVER);
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/eld-malfunctions`);

    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: EVENT, status: "Open", code: "P" }));
    const input = { code: "P", noticedAtUtc: "2026-10-10T19:50:00.000Z", description: "Frozen", vehicleId: null };
    const record = await reportEldMalfunction(DRIVER, input, { idempotencyKey: "cmd-3" });
    expect(record.status).toBe("Open");
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/hos/eld-malfunctions`);
    expect(callInit(1).method).toBe("POST");
    expect(bodyOf(1)).toEqual(input);
    expect(headersOf(1)["Idempotency-Key"]).toBe("cmd-3");

    const message = "The malfunction code must be at most 32 characters.";
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { code: "Drivers.Hos.EldCodeTooLong", message }));
    const err = await reportEldMalfunction(DRIVER, input).catch((e) => e);
    expect((err as ApiError).code).toBe("Drivers.Hos.EldCodeTooLong");
    expect((err as ApiError).message).toBe(message);
  });
});
