import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  canReopenTrip,
  reopenLanding,
  reopenTrip,
  tripActivityPreviousReason,
  tripActivityReason,
  type TripActivityEntry,
  type TripRecord,
  type TripStatus,
} from "./trips";
import { ApiError } from "./transport";

// Reopen trip: the endpoint on the wire, the server's refusal surfaced verbatim,
// and which statuses the console offers the button for. The backend decides the
// landing and every refusal — these pin only what the console pre-gates.

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

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

function trip(over: Partial<TripRecord> = {}): TripRecord {
  return {
    id: "trip-1",
    tripNumber: "NL-2026-0042",
    serviceDate: "2026-10-12",
    windowStart: "06:30:00",
    windowEnd: null,
    serviceType: "ContractCrew",
    routeId: "route-1",
    routeName: "Leaf Rapids to Lynn Lake",
    origin: "Leaf Rapids",
    destination: "Lynn Lake",
    stops: [],
    distanceKm: 214,
    scheduleTemplateId: null,
    roundTripKey: null,
    direction: null,
    isEmptyLeg: false,
    clientId: "client-1",
    clientName: "Alamos",
    poNumber: null,
    driverId: null,
    driverName: null,
    vehicleId: null,
    vehicleUnit: null,
    seatsCapacity: 14,
    seatsConfirmed: 0,
    seatsMinimum: null,
    demandGuaranteed: false,
    status: "Scheduled",
    manifestId: null,
    hasPostTripInspection: false,
    operationsFinishedAtUtc: null,
    completedAtUtc: null,
    cancelledReason: null,
    writtenOffReason: null,
    createdAtUtc: "2026-10-01T10:00:00Z",
    updatedAtUtc: "2026-10-01T10:00:00Z",
    billing: null,
    ...over,
  };
}

describe("reopenTrip", () => {
  it("POSTs reopen with the reason as the JSON body", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await reopenTrip("trip-1", "Cancelled the wrong date");
    expect(callPath()).toBe("/api/trips/trip-1/reopen");
    expect(callInit().method).toBe("POST");
    expect(JSON.parse(String(callInit().body))).toEqual({ reason: "Cancelled the wrong date" });
  });

  it("sends expectedStatus unmapped when given", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await reopenTrip("trip-1", "Mistake", "ReadyForBilling");
    expect(JSON.parse(String(callInit().body))).toEqual({ reason: "Mistake", expectedStatus: "ReadyForBilling" });
  });

  it("surfaces the status-changed 409 verbatim", async () => {
    const message = "This trip's status changed since you opened it — refresh and try again";
    fetchMock.mockResolvedValueOnce(jsonResponse(409, { code: "Trips.Trip.ReopenStatusChanged", message }));
    const err = await reopenTrip("trip-1", "Mistake", "Cancelled").catch((e) => e);
    expect((err as ApiError).message).toBe(message);
  });

  it("surfaces the server's refusal message verbatim — it is the final authority", async () => {
    const message = "A Billing worksheet claims this trip — void the draft in Billing first, then reopen the trip.";
    fetchMock.mockResolvedValueOnce(jsonResponse(409, { code: "Trips.Trip.OnWorksheetCannotReopen", message }));
    const err = await reopenTrip("trip-1", "Mistake").catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(409);
    expect((err as ApiError).message).toBe(message);
  });
});

describe("reopenLanding / canReopenTrip", () => {
  const cases: [TripStatus, TripStatus | null][] = [
    ["Cancelled", "Scheduled"],
    ["InProgress", "Scheduled"],
    ["ReadyForBilling", "Scheduled"],
    ["WrittenOff", "Scheduled"],
    ["Scheduled", null],
    ["Completed", null],
    ["Invoiced", null],
  ];

  it.each(cases)("%s lands in %s", (from, to) => {
    expect(reopenLanding(from)).toBe(to);
    expect(canReopenTrip(trip({ status: from }))).toBe(to !== null);
  });

  it.each<TripStatus>(["Cancelled", "InProgress", "ReadyForBilling", "WrittenOff"])(
    "hides the button for a %s trip a billing worksheet claims",
    (status) => {
      const billing = {
        state: "OnWorksheet" as const,
        invoiceId: "inv-1",
        invoiceNumber: "INV-0007",
        qboInvoiceId: null,
        qboEnteredDate: null,
        paymentConfirmedDate: null,
      };
      expect(canReopenTrip(trip({ status, billing }))).toBe(false);
    },
  );

  it("hides the button for a trip written off with its invoice", () => {
    const billing = {
      state: "WrittenOff" as const,
      invoiceId: "inv-1",
      invoiceNumber: "INV-0007",
      qboInvoiceId: null,
      qboEnteredDate: null,
      paymentConfirmedDate: null,
    };
    expect(canReopenTrip(trip({ status: "WrittenOff", billing }))).toBe(false);
  });
});

describe("tripActivityReason / tripActivityPreviousReason", () => {
  const entry = (over: Partial<TripActivityEntry> = {}): TripActivityEntry => ({
    occurredAtUtc: "2026-10-12T14:00:00Z",
    aggregateType: "trip",
    eventType: "trip-reopened",
    source: "Dispatcher",
    enteredBy: "dispatch@northernlink.ca",
    reason: "Cancelled the wrong date",
    ...over,
  });

  it("returns the trimmed reason on a trip-reopened entry", () => {
    expect(tripActivityReason(entry({ reason: "  Cancelled the wrong date  " }))).toBe("Cancelled the wrong date");
  });

  it("is null when the reason is absent, null or blank", () => {
    expect(tripActivityReason(entry({ reason: undefined }))).toBeNull();
    expect(tripActivityReason(entry({ reason: null }))).toBeNull();
    expect(tripActivityReason(entry({ reason: "   " }))).toBeNull();
  });

  it("is generic — returns a reason on any entry type", () => {
    expect(tripActivityReason(entry({ eventType: "trip-cancelled", reason: "Road closed" }))).toBe("Road closed");
  });

  it("returns the trimmed previous reason, null when absent or blank", () => {
    expect(tripActivityPreviousReason(entry({ previousReason: "  Road closed " }))).toBe("Road closed");
    expect(tripActivityPreviousReason(entry())).toBeNull();
    expect(tripActivityPreviousReason(entry({ previousReason: null }))).toBeNull();
    expect(tripActivityPreviousReason(entry({ previousReason: "  " }))).toBeNull();
  });
});
