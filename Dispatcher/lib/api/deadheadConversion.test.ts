import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  convertTripToDeadhead,
  convertTripToPassengerTrip,
  deadheadConversionBlockReason,
  type ManifestCargo,
  type ManifestPassenger,
  type TripManifest,
  type TripRecord,
} from "./trips";
import { ApiError } from "./transport";

// Deadhead conversion: the two endpoints on the wire, and the client-visible half
// of the server's "nobody is booked on it" rule (Trip.ConvertToDeadhead). The
// server stays the final authority — these pin what the console can pre-gate.

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
    clientId: null,
    clientName: null,
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

const passenger: ManifestPassenger = {
  name: "J. Bighetty",
  pickupStopId: null,
  dropoffStopId: null,
  idVerified: false,
  boardedOn: false,
  boardedOff: false,
  fareAmountCad: null,
  farePaymentMethod: null,
  farePaidAtUtc: null,
  externalRef: null,
};

const cargoItem: ManifestCargo = { description: "Groceries", hazmat: false, secured: true };

function manifest(passengers = 0, cargo = 0): TripManifest {
  return {
    id: "manifest-1",
    tripDate: "2026-10-12",
    tripNumber: "NL-2026-0042",
    route: "Leaf Rapids to Lynn Lake",
    direction: null,
    client: null,
    passengers: Array.from({ length: passengers }, () => passenger),
    allSeatbeltsVerified: false,
    cargo: Array.from({ length: cargo }, () => cargoItem),
    allCargoSecured: null,
    source: "Dispatcher",
    enteredBy: "Dispatch",
    enteredAt: null,
    createdAtUtc: "2026-10-01T10:00:00Z",
    faresCollectedCad: 0,
    faresPaidCount: 0,
    faresWaivedCount: 0,
  };
}

describe("convertTripToDeadhead / convertTripToPassengerTrip", () => {
  it("POSTs convert-to-deadhead with no body", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await convertTripToDeadhead("trip-1");
    expect(callPath()).toBe("/api/trips/trip-1/convert-to-deadhead");
    expect(callInit().method).toBe("POST");
    expect(callInit().body).toBeUndefined();
  });

  it("POSTs convert-to-passenger-trip with no body", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));
    await convertTripToPassengerTrip("trip-1");
    expect(callPath()).toBe("/api/trips/trip-1/convert-to-passenger-trip");
    expect(callInit().method).toBe("POST");
    expect(callInit().body).toBeUndefined();
  });

  it("surfaces the server's 409 message verbatim — it is the final authority", async () => {
    const message =
      "2 imported Bookeo bookings are placed on this trip — cancel or move them in Bookeo and re-import before converting it to a deadhead.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Trips.Trip.DeadheadConversionHasExternalBookings", message }),
    );
    const err = await convertTripToDeadhead("trip-1").catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).message).toBe(message);
  });
});

describe("deadheadConversionBlockReason", () => {
  it("is null for an empty Scheduled trip with no manifest", () => {
    expect(deadheadConversionBlockReason(trip(), null, null, false)).toBeNull();
  });

  it("is null for an empty manifest — the server deletes it on conversion", () => {
    expect(deadheadConversionBlockReason(trip({ manifestId: "manifest-1" }), manifest(), null, false)).toBeNull();
  });

  it("is null when the button never shows: not Scheduled, or already a deadhead", () => {
    expect(deadheadConversionBlockReason(trip({ status: "InProgress" }), null, null, false)).toBeNull();
    expect(deadheadConversionBlockReason(trip({ isEmptyLeg: true }), null, null, false)).toBeNull();
  });

  it("waits while the manifest is loading", () => {
    expect(deadheadConversionBlockReason(trip({ manifestId: "manifest-1" }), null, null, true)).toBe(
      "checking the manifest…",
    );
  });

  it("blocks on confirmed seats, singular and plural", () => {
    expect(deadheadConversionBlockReason(trip({ seatsConfirmed: 1 }), null, null, false)).toBe("1 seat confirmed");
    expect(deadheadConversionBlockReason(trip({ seatsConfirmed: 4 }), null, null, false)).toBe("4 seats confirmed");
  });

  it("blocks on a gift-a-seat pledge, with or without confirmed seats", () => {
    expect(deadheadConversionBlockReason(trip({ demandGuaranteed: true }), null, null, false)).toBe(
      "gift-a-seat pledge on this trip",
    );
    expect(deadheadConversionBlockReason(trip({ demandGuaranteed: true, seatsConfirmed: 2 }), null, null, false)).toBe(
      "gift-a-seat pledge and 2 seats confirmed",
    );
  });

  it("reports demand even while the manifest is still loading — it is already known", () => {
    expect(deadheadConversionBlockReason(trip({ seatsConfirmed: 3 }), null, null, true)).toBe("3 seats confirmed");
  });

  it("blocks on manifest passengers", () => {
    expect(deadheadConversionBlockReason(trip(), manifest(1, 0), null, false)).toBe("manifest lists 1 passenger");
    expect(deadheadConversionBlockReason(trip(), manifest(3, 0), null, false)).toBe("manifest lists 3 passengers");
  });

  it("blocks on manifest cargo items", () => {
    expect(deadheadConversionBlockReason(trip(), manifest(0, 1), null, false)).toBe("manifest lists 1 cargo item");
    expect(deadheadConversionBlockReason(trip(), manifest(0, 2), null, false)).toBe("manifest lists 2 cargo items");
  });

  it("names both when the manifest lists passengers and cargo", () => {
    expect(deadheadConversionBlockReason(trip(), manifest(2, 1), null, false)).toBe(
      "manifest lists 2 passengers and 1 cargo item",
    );
  });

  it("blocks when the paired leg is already a deadhead", () => {
    const partner = trip({ id: "trip-2", tripNumber: "NL-2026-0043", isEmptyLeg: true });
    expect(deadheadConversionBlockReason(trip({ roundTripKey: "rt-1" }), null, partner, false)).toBe(
      "paired leg NL-2026-0043 is already a deadhead",
    );
  });

  it("allows conversion when the paired leg carries passengers", () => {
    const partner = trip({ id: "trip-2", tripNumber: "NL-2026-0043", seatsConfirmed: 9 });
    expect(deadheadConversionBlockReason(trip({ roundTripKey: "rt-1" }), null, partner, false)).toBeNull();
  });
});
