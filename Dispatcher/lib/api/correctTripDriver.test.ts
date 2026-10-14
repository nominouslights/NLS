import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  canCorrectDriver,
  correctTripDriver,
  tripActivityReason,
  type TripActivityEntry,
  type TripStatus,
} from "./trips";
import { ApiError } from "./transport";

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

describe("canCorrectDriver", () => {
  const cases: [TripStatus, boolean][] = [
    ["Scheduled", false],
    ["InProgress", false],
    ["ReadyForBilling", true],
    ["Invoiced", true],
    ["Completed", true],
    ["Cancelled", false],
    ["WrittenOff", false],
  ];
  it.each(cases)("%s → %s", (status, expected) => {
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
