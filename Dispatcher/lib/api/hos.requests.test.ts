import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  amendDutyEvent,
  getDutyDay,
  getHosStatus,
  listDutyDays,
  listHosStatusBatch,
  recordDutyEvent,
  transcribeDay,
  type DutyStatusWire,
  type EventSourceWire,
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
