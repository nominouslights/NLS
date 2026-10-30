import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  acknowledgeHosException,
  amendDutyEvent,
  approveOffDutyDeferral,
  attestPriorRecords,
  declareHosException,
  getDutyDay,
  getHosAlertsToday,
  getHosAudit,
  getHosStatus,
  liftOutOfService,
  listComplianceFindings,
  listDutyDays,
  listEldMalfunctions,
  listHosExceptions,
  listHosStatusBatch,
  listOutOfService,
  openComplianceFinding,
  recordDutyEvent,
  recordOutOfService,
  rejectHosException,
  reportEldMalfunction,
  resolveEldMalfunction,
  transcribeDay,
  updateComplianceFinding,
  withdrawHosException,
  type DutyStatusWire,
  type EventSourceWire,
  type HosExceptionKindWire,
} from "./hos";
import { ApiError } from "./transport";
import { DUTY_STATUSES, EVENT_SOURCES, HosWire } from "../hos/engine";

// What actually goes on the wire for the HOS ledger. The routes and bodies are the
// Drivers module's contract (HosEndpoints.cs / HosResponses.cs); a wrong route is a 404 the
// console would report as "record not found", and a wrong header makes a retried write a
// duplicate duty event rather than a replay.

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

  it("HosExceptionKindWire is exactly HosWire's three kind strings", () => {
    const kinds: HosExceptionKindWire[] = [HosWire.AdverseDrivingConditionsKind, HosWire.EmergencyKind, HosWire.OffDutyDeferralKind] as HosExceptionKindWire[];
    expect(kinds).toEqual(["Adverse driving conditions", "Emergency", "Off-duty deferral (CO approved)"]);
  });
});

// ---- Compliance records: the HosAccess group (exceptions, ELD malfunctions) ----

describe("exceptions (HosAccess)", () => {
  it("listHosExceptions GETs …/hos/exceptions with from/to when given, bare when not", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [])).mockResolvedValueOnce(jsonResponse(200, []));
    await listHosExceptions(DRIVER, { from: "2026-10-01", to: "2026-10-14" });
    await listHosExceptions(DRIVER);
    expect(callPath(0)).toBe(`/api/drivers/${DRIVER}/hos/exceptions?from=2026-10-01&to=2026-10-14`);
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/hos/exceptions`);
  });

  it("declareHosException POSTs the adverse body — kind as the wire string, attestation and minutes", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: "x", status: "Declared" }));
    const input = {
      kind: "Adverse driving conditions" as const,
      effectiveFromUtc: "2026-10-09T15:00:00.000Z",
      reason: "Whiteout on PTH 6",
      effectiveToUtc: null,
      location: "North of Grand Rapids",
      notKnownBeforeDeparture: true,
      extraDrivingMinutes: 60,
      eldRemarkRecorded: true,
    };
    const res = await declareHosException(DRIVER, input);
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/exceptions`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual(input);
    expect(res.status).toBe("Declared");
  });

  it("surfaces the adverse attestation refusal verbatim", async () => {
    const message = "Adverse driving conditions must not have been knowable before departure (s.76).";
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { code: "Drivers.Hos.AdverseKnowabilityRequired", message }));
    const err = await declareHosException(DRIVER, { kind: "Adverse driving conditions", effectiveFromUtc: "2026-10-09T15:00:00Z", reason: "r" }).catch((e) => e);
    expect((err as ApiError).code).toBe("Drivers.Hos.AdverseKnowabilityRequired");
    expect((err as ApiError).message).toBe(message);
  });

  it("acknowledge POSTs …/exceptions/{id}/acknowledge with no body", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: EVENT, status: "Acknowledged" }));
    await acknowledgeHosException(DRIVER, EVENT);
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/exceptions/${EVENT}/acknowledge`);
    expect(callInit().method).toBe("POST");
    expect(callInit().body).toBeUndefined();
  });

  it("withdraw and reject POST their routes with {note} — null when none", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, {})).mockResolvedValueOnce(jsonResponse(200, {}));
    await withdrawHosException(DRIVER, EVENT, "Declared in error");
    await rejectHosException(DRIVER, EVENT);
    expect(callPath(0)).toBe(`/api/drivers/${DRIVER}/hos/exceptions/${EVENT}/withdraw`);
    expect(bodyOf(0)).toEqual({ note: "Declared in error" });
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/hos/exceptions/${EVENT}/reject`);
    expect(bodyOf(1)).toEqual({ note: null });
  });

  it("approveOffDutyDeferral POSTs …/hos/exceptions/deferral with the trip and minutes", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: "d", status: "Approved" }));
    const input = { tripId: EVENT, effectiveFromUtc: "2026-10-11T12:00:00.000Z", deferredOffDutyMinutes: 90, reason: "Late return from Thompson", location: null, eldRemarkRecorded: false };
    await approveOffDutyDeferral(DRIVER, input);
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/exceptions/deferral`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual(input);
  });
});

describe("ELD malfunctions", () => {
  it("list GETs and report POSTs …/hos/eld-malfunctions; resolve POSTs …/{id}/resolve with {note}", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [])).mockResolvedValueOnce(jsonResponse(201, {})).mockResolvedValueOnce(jsonResponse(200, {}));
    await listEldMalfunctions(DRIVER);
    const input = { code: "P-01", noticedAtUtc: "2026-10-09T12:00:00.000Z", description: "Display frozen" };
    await reportEldMalfunction(DRIVER, input);
    await resolveEldMalfunction(DRIVER, EVENT, "Replaced unit");
    expect(callPath(0)).toBe(`/api/drivers/${DRIVER}/hos/eld-malfunctions`);
    expect(callInit(0).method).toBeUndefined();
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/hos/eld-malfunctions`);
    expect(callInit(1).method).toBe("POST");
    expect(bodyOf(1)).toEqual(input);
    expect(callPath(2)).toBe(`/api/drivers/${DRIVER}/hos/eld-malfunctions/${EVENT}/resolve`);
    expect(bodyOf(2)).toEqual({ note: "Replaced unit" });
  });
});

// ---- The ComplianceAccess group ----

describe("out of service (ComplianceAccess)", () => {
  it("list GETs and record POSTs …/out-of-service; lift POSTs …/{id}/lift with {reason}", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [])).mockResolvedValueOnce(jsonResponse(201, { id: "o" })).mockResolvedValueOnce(jsonResponse(200, {}));
    await listOutOfService(DRIVER);
    const input = { declaredAtUtc: "2026-10-09T12:00:00.000Z", reason: "Roadside inspection", untilUtc: "2026-10-12T12:00:00.000Z", evidence: null };
    await recordOutOfService(DRIVER, input);
    await liftOutOfService(DRIVER, EVENT, "Records produced");
    expect(callPath(0)).toBe(`/api/drivers/${DRIVER}/out-of-service`);
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/out-of-service`);
    expect(callInit(1).method).toBe("POST");
    expect(bodyOf(1)).toEqual(input);
    expect(callPath(2)).toBe(`/api/drivers/${DRIVER}/out-of-service/${EVENT}/lift`);
    expect(bodyOf(2)).toEqual({ reason: "Records produced" });
  });

  it("surfaces the 409 Drivers.Hos.AlreadyOutOfService sentence verbatim", async () => {
    const message = "This driver already has an active out-of-service declaration; lift it before recording another.";
    fetchMock.mockResolvedValueOnce(jsonResponse(409, { code: "Drivers.Hos.AlreadyOutOfService", message }));
    const err = await recordOutOfService(DRIVER, { declaredAtUtc: "2026-10-09T12:00:00Z", reason: "r" }).catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(409);
    expect((err as ApiError).code).toBe("Drivers.Hos.AlreadyOutOfService");
    expect((err as ApiError).message).toBe(message);
  });
});

describe("findings, attestation, audit (ComplianceAccess)", () => {
  it("findings: GET with range, POST, PUT …/{id}", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [])).mockResolvedValueOnce(jsonResponse(201, {})).mockResolvedValueOnce(jsonResponse(200, {}));
    await listComplianceFindings(DRIVER, { from: "2026-10-01" });
    const input = { localDate: "2026-10-09", rule: "DailyDriving13h", severity: "Major" as const, description: "Drove 14h" };
    await openComplianceFinding(DRIVER, input);
    await updateComplianceFinding(DRIVER, EVENT, { status: "Corrected", followUpNote: "Coached" });
    expect(callPath(0)).toBe(`/api/drivers/${DRIVER}/findings?from=2026-10-01`);
    expect(callPath(1)).toBe(`/api/drivers/${DRIVER}/findings`);
    expect(callInit(1).method).toBe("POST");
    expect(bodyOf(1)).toEqual(input);
    expect(callPath(2)).toBe(`/api/drivers/${DRIVER}/findings/${EVENT}`);
    expect(callInit(2).method).toBe("PUT");
    expect(bodyOf(2)).toEqual({ status: "Corrected", followUpNote: "Coached" });
  });

  it("attestPriorRecords POSTs …/prior-records-attestation", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { coveredThroughDate: "2026-10-09" }));
    const input = { coveredThroughDate: "2026-10-09", note: null, worksForOtherCarriers: true };
    const res = await attestPriorRecords(DRIVER, input);
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/prior-records-attestation`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual(input);
    expect(res.coveredThroughDate).toBe("2026-10-09");
  });

  it("getHosAudit GETs …/hos/audit with from/to", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));
    await getHosAudit(DRIVER, { from: "2026-09-26", to: "2026-10-09" });
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/audit?from=2026-09-26&to=2026-10-09`);
  });
});

describe("today alerts (DriverRosterAccess)", () => {
  it("getHosAlertsToday GETs /api/drivers/hos/alerts/today with no query and returns the body as-is", async () => {
    const body = {
      asOfUtc: "2026-10-12T14:00:00+00:00",
      driversNearLimit: 1,
      driversOverLimit: 0,
      outOfService: 0,
      openMalfunctions: 0,
      uncertifiedYesterday: 0,
      unassignedExceptions: 0,
      items: [{ driverId: DRIVER, driverName: "K. Beardy", kind: "NearLimit", message: "K. Beardy has 45m left before the tightest limit." }],
      label: "Planning figures",
    };
    fetchMock.mockResolvedValueOnce(jsonResponse(200, body));
    const result = await getHosAlertsToday();
    expect(callPath()).toBe("/api/drivers/hos/alerts/today");
    expect(callInit().method ?? "GET").toBe("GET");
    expect(result).toEqual(body);
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

  it("getDutyDay GETs …/hos/days/{date}", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, {}));
    await getDutyDay(DRIVER, "2026-10-09");
    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/days/2026-10-09`);
  });

  it("getHosStatus GETs …/hos/status and returns the evaluation nested verbatim", async () => {
    const evaluation = { asOfUnix: 1, band: "Ontime", blockingReasons: [], violations: [] };
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { driverId: DRIVER, label: "L", evaluation }));

    const status = await getHosStatus(DRIVER);

    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/status`);
    expect(status.evaluation).toEqual(evaluation);
  });

  it("listHosStatusBatch GETs the roster route once, with no driver in the path", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));
    await listHosStatusBatch();
    expect(callPath()).toBe("/api/drivers/hos/status");
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});

describe("recordDutyEvent", () => {
  it("POSTs the body as JSON and repeats the eventId as the Idempotency-Key header", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { eventId: EVENT, dutyDayId: "d", localDate: "2026-10-09", created: true }));
    const input = {
      eventId: EVENT,
      atUtc: "2026-10-09T13:00:00.000Z",
      status: "Driving" as const,
      source: "Manual (paper backup)" as const,
      enteredBy: "Dispatch — L. Moose",
      note: "Paper log",
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
    const res = await recordDutyEvent(DRIVER, { eventId: EVENT, atUtc: "2026-10-09T13:00:00Z", status: "Off Duty" });
    expect(res.created).toBe(false);
  });
});

describe("transcribeDay", () => {
  it("POSTs …/hos/days/{date}/transcribe with the ordered entries and transcriptionId", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { dutyDayId: "d", localDate: "2026-10-09", appended: 4, alreadyRecorded: 0 }));
    const input = {
      transcriptionId: "33333333-3333-3333-3333-333333333333",
      source: "ELD transcription" as const,
      enteredBy: "Dispatch — R. Sinclair",
      carrierName: null,
      entries: [
        { time: "06:00", status: "On Duty" as const, note: null },
        { time: "06:30", status: "Driving" as const, note: null },
        { time: "14:00", status: "On Duty" as const, note: "Post-trip" },
        { time: "14:30", status: "Off Duty" as const, note: null },
      ],
    };

    const res = await transcribeDay(DRIVER, "2026-10-09", input);

    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/days/2026-10-09/transcribe`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual(input);
    expect(res.appended).toBe(4);
  });

  it("surfaces the server's 400 message verbatim as an ApiError", async () => {
    const message = "Entry 3 (06:15) is not after entry 2 (06:30); times must be strictly increasing.";
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { code: "Drivers.Hos.TimesNotIncreasing", message }));

    const err = await transcribeDay(DRIVER, "2026-10-09", {
      transcriptionId: "t",
      source: "Manual (paper backup)",
      enteredBy: "x",
      entries: [],
    }).catch((e) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(400);
    expect((err as ApiError).code).toBe("Drivers.Hos.TimesNotIncreasing");
    expect((err as ApiError).message).toBe(message);
  });

  it("surfaces a 403 Drivers.NotYourRecord with the server's message", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(403, { code: "Drivers.NotYourRecord", message: "That is not your driver record." }));
    const err = await getHosStatus(DRIVER).catch((e) => e);
    expect((err as ApiError).code).toBe("Drivers.NotYourRecord");
    expect((err as ApiError).message).toBe("That is not your driver record.");
  });
});

describe("amendDutyEvent", () => {
  it("POSTs …/hos/duty-events/{eventId}/amend with the reason and replacement", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(201, {
        originalEventId: EVENT,
        replacementEventId: "r",
        dutyDayId: "d",
        localDate: "2026-10-09",
        recertificationRequired: true,
        created: true,
      }),
    );
    const input = {
      replacementEventId: "44444444-4444-4444-4444-444444444444",
      atUtc: "2026-10-09T13:15:00.000Z",
      status: "Driving" as const,
      reason: "Paper log read 08:15, not 08:00.",
      source: "Manual (paper backup)" as const,
      enteredBy: "Dispatch — L. Moose",
      note: null,
    };

    const res = await amendDutyEvent(DRIVER, EVENT, input);

    expect(callPath()).toBe(`/api/drivers/${DRIVER}/hos/duty-events/${EVENT}/amend`);
    expect(callInit().method).toBe("POST");
    expect(bodyOf()).toEqual(input);
    expect(res.recertificationRequired).toBe(true);
  });

  it("surfaces a 409 message verbatim", async () => {
    const message = "The replacement must fall on the same local day as the original (2026-10-09).";
    fetchMock.mockResolvedValueOnce(jsonResponse(409, { code: "Drivers.Hos.AmendCrossesDay", message }));
    const err = await amendDutyEvent(DRIVER, EVENT, { atUtc: "2026-10-10T13:15:00Z", status: "Driving", reason: "r" }).catch((e) => e);
    expect((err as ApiError).status).toBe(409);
    expect((err as ApiError).message).toBe(message);
  });
});
