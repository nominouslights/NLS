import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  canCorrectDriver,
  clearanceAsOf,
  correctTripDriver,
  DRIVER_CORRECTION_REASON_MAX,
  hasClearanceFor,
  tripActivityReason,
  type TripActivityEntry,
  type TripStatus,
} from "./trips";
import { ApiError } from "./transport";
import type { DriverClearanceRecord } from "./drivers";

// CORRECT DRIVER: the endpoint on the wire, the status gate the console
// pre-applies (the server stays the final authority), and the generic
// activity-reason helper that renders "Reason: …" under any log entry.

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

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("correctTripDriver", () => {
  it("POSTs { driverId, reason } to /api/trips/{id}/correct-driver", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await correctTripDriver("trip-1", "drv-2", "Swap at Thompson never entered");

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [path, init] = fetchMock.mock.calls[0];
    expect(String(path)).toBe("/api/trips/trip-1/correct-driver");
    expect(init?.method).toBe("POST");
    expect(JSON.parse(String(init?.body))).toEqual({ driverId: "drv-2", reason: "Swap at Thompson never entered" });
  });

  it("surfaces the server's code and message verbatim", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, {
        code: "Trips.Trip.UseReassign",
        message: "This trip has not finished — use Reassign instead.",
      }),
    );
    const err = await correctTripDriver("trip-1", "drv-2", "x").catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).code).toBe("Trips.Trip.UseReassign");
    expect((err as ApiError).status).toBe(409);
    expect((err as ApiError).message).toBe("This trip has not finished — use Reassign instead.");
  });
});

describe("DRIVER_CORRECTION_REASON_MAX (mirrors C# Trip.MaxReasonLength)", () => {
  it("equals Trip.MaxReasonLength = 500", () => {
    expect(DRIVER_CORRECTION_REASON_MAX).toBe(500);
  });
});

describe("canCorrectDriver (mirrors C# Trip.DriverCorrectionAllowedFor)", () => {
  const cases: [TripStatus, boolean][] = [
    ["Scheduled", false],
    ["InProgress", false],
    ["ReadyForBilling", true],
    ["Invoiced", true],
    ["Completed", true],
    ["Cancelled", false],
    ["WrittenOff", false],
  ];
  it.each(cases)("Trip.DriverCorrectionAllowedFor(%s) → %s", (status, expected) => {
    expect(canCorrectDriver({ status })).toBe(expected);
  });
});

describe("tripActivityReason", () => {
  const entry = (over: Partial<TripActivityEntry>): TripActivityEntry => ({
    occurredAtUtc: "2026-10-14T12:00:00Z",
    aggregateType: "trip",
    eventType: "trip-driver-corrected",
    source: null,
    enteredBy: "Dana Dispatcher",
    ...over,
  });

  it("returns the trimmed reason when non-blank", () => {
    expect(tripActivityReason(entry({ reason: "  Wrong driver keyed  " }))).toBe("Wrong driver keyed");
  });

  it("returns null for a missing, null, empty or whitespace-only reason", () => {
    expect(tripActivityReason(entry({}))).toBeNull();
    expect(tripActivityReason(entry({ reason: null }))).toBeNull();
    expect(tripActivityReason(entry({ reason: "" }))).toBeNull();
    expect(tripActivityReason(entry({ reason: "   \n\t " }))).toBeNull();
  });

  it("is not gated to one event type — any entry with a reason shows it", () => {
    expect(tripActivityReason(entry({ eventType: "trip-cancelled", reason: "Road closed" }))).toBe("Road closed");
    expect(
      tripActivityReason(entry({ aggregateType: "trip-manifest", eventType: "trip-manifest-updated", reason: "Late add" })),
    ).toBe("Late add");
  });
});

describe("hasClearanceFor — as of a given date (a correction is historical)", () => {
  // "Today" pinned to 2026-10-20; the trip under correction ran 2026-10-10.
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-20T12:00:00"));
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  const clearance = (over: Partial<DriverClearanceRecord>): DriverClearanceRecord => ({
    id: "c-1",
    driverId: "drv-1",
    title: "Site access",
    clientName: "Alamos Gold",
    expiry: null,
    grantedAtUtc: "2026-01-05T15:00:00Z",
    ...over,
  });

  it("an expired-since clearance counts on the trip's date but not today", () => {
    const rows = [clearance({ expiry: "2026-10-12" })];
    expect(hasClearanceFor(rows, "Alamos Gold", "2026-10-10")).toBe(true);
    expect(hasClearanceFor(rows, "Alamos Gold", "2026-10-20")).toBe(false);
  });

  it("a past date ignores grantedAtUtc — it is the ENTRY date, not when the clearance took effect", () => {
    // Entered 2026-10-12 (after the trip), unexpired: a long-held clearance
    // keyed in late must not make the trip look uncleared.
    const rows = [clearance({ grantedAtUtc: "2026-10-12T15:00:00Z" })];
    expect(hasClearanceFor(rows, "Alamos Gold", "2026-10-10")).toBe(true);
    expect(hasClearanceFor(rows, "Alamos Gold", "2026-10-20")).toBe(true);
  });

  it("a past date still warns when the clearance had expired before the trip", () => {
    const rows = [clearance({ expiry: "2026-10-09", grantedAtUtc: "2026-10-12T15:00:00Z" })];
    expect(hasClearanceFor(rows, "Alamos Gold", "2026-10-10")).toBe(false);
  });

  it("today keeps the entry-date floor (a row dated in the future does not count yet)", () => {
    const rows = [clearance({ grantedAtUtc: "2026-10-22T15:00:00Z" })];
    expect(hasClearanceFor(rows, "Alamos Gold")).toBe(false);
    expect(hasClearanceFor(rows, "Alamos Gold", "2026-10-20")).toBe(false);
  });

  it("a different client never counts", () => {
    expect(hasClearanceFor([clearance({ clientName: "Vale" })], "Alamos Gold", "2026-10-10")).toBe(false);
  });

  it("the expiry day itself still counts", () => {
    expect(hasClearanceFor([clearance({ expiry: "2026-10-10" })], "Alamos Gold", "2026-10-10")).toBe(true);
  });

  it("a trip with no client needs no clearance", () => {
    expect(hasClearanceFor([], null, "2026-10-10")).toBe(true);
  });
});

describe("clearanceAsOf — the date the detail panel judges clearance on", () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-20T12:00:00"));
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  const cases: [TripStatus, string][] = [
    ["Scheduled", "2026-10-20"],
    ["InProgress", "2026-10-20"],
    ["ReadyForBilling", "2026-10-10"],
    ["Invoiced", "2026-10-10"],
    ["Completed", "2026-10-10"],
    ["Cancelled", "2026-10-20"],
    ["WrittenOff", "2026-10-20"],
  ];
  it.each(cases)("%s → %s (finished trips use serviceDate, like CORRECT DRIVER)", (status, expected) => {
    expect(clearanceAsOf({ status, serviceDate: "2026-10-10" })).toBe(expected);
  });

  it("panel and modal agree: a finished trip with a late-entered clearance reads cleared", () => {
    const rows: DriverClearanceRecord[] = [
      {
        id: "c-1",
        driverId: "drv-1",
        title: "Site access",
        clientName: "Alamos Gold",
        expiry: null,
        grantedAtUtc: "2026-10-12T15:00:00Z",
      },
    ];
    const trip = { status: "ReadyForBilling" as TripStatus, serviceDate: "2026-10-10" };
    expect(hasClearanceFor(rows, "Alamos Gold", clearanceAsOf(trip))).toBe(true);
  });
});
