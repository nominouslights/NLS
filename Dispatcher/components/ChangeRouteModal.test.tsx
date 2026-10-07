import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import ChangeRouteModal from "./ChangeRouteModal";
import type { TripRecord, TripRouteChangePreview } from "@/lib/api/trips";

// CHANGE ROUTE dialog: pick a route → preview → chips / acknowledgement → POST.
// fetch is mocked at the transport seam, the same way BookeoImportModal.test does.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("@/lib/auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

function route(id: string, name: string, origin: string, distanceKm: number) {
  return {
    id,
    name,
    stops: [],
    origin,
    destination: "Thompson",
    distanceKm,
    estimatedDurationMinutes: 270,
    requiredLicenceClass: null,
    active: true,
    createdAtUtc: "2026-01-01T00:00:00Z",
    updatedAtUtc: "2026-01-01T00:00:00Z",
  };
}

const ROUTES = [
  route("route-old", "Lynn Lake Run", "Lynn Lake", 320),
  route("route-new", "Leaf Rapids Run", "Leaf Rapids", 250),
  { ...route("route-retired", "Retired Run", "Pukatawagan", 400), active: false },
];

const TRIP: TripRecord = {
  id: "trip-1",
  tripNumber: "TR-0001",
  serviceDate: "2026-10-10",
  windowStart: "06:30:00",
  windowEnd: "11:00:00",
  serviceType: "Community",
  routeId: "route-old",
  routeName: "Lynn Lake Run",
  origin: "Lynn Lake",
  destination: "Thompson",
  stops: [],
  distanceKm: 320,
  scheduleTemplateId: null,
  roundTripKey: "rt-1",
  direction: "Outbound",
  isEmptyLeg: false,
  clientId: null,
  clientName: null,
  poNumber: null,
  driverId: null,
  driverName: null,
  vehicleId: null,
  vehicleUnit: null,
  seatsCapacity: 7,
  seatsConfirmed: 3,
  seatsMinimum: null,
  demandGuaranteed: false,
  status: "Scheduled",
  manifestId: "m-1",
  hasPostTripInspection: false,
  operationsFinishedAtUtc: null,
  completedAtUtc: null,
  cancelledReason: null,
  writtenOffReason: null,
  createdAtUtc: "2026-10-01T00:00:00Z",
  updatedAtUtc: "2026-10-01T00:00:00Z",
  billing: null,
} as TripRecord;

function leg(tripId: string, tripNumber: string, isRequestedTrip: boolean, direction: "Outbound" | "Inbound") {
  return {
    tripId,
    tripNumber,
    isRequestedTrip,
    status: "Scheduled",
    direction,
    willChange: true,
    currentRouteName: "Lynn Lake Run",
    currentOrigin: direction === "Outbound" ? "Lynn Lake" : "Thompson",
    currentDestination: direction === "Outbound" ? "Thompson" : "Lynn Lake",
    currentDistanceKm: 320,
    newOrigin: direction === "Outbound" ? "Leaf Rapids" : "Thompson",
    newDestination: direction === "Outbound" ? "Thompson" : "Leaf Rapids",
    newDistanceKm: 250,
    newStops: [],
    windowStart: "06:30:00",
    currentWindowEnd: "11:00:00",
    newWindowEnd: "10:40:00",
  };
}

function preview(over: Partial<TripRouteChangePreview> = {}): TripRouteChangePreview {
  return {
    tripId: "trip-1",
    tripNumber: "TR-0001",
    currentRouteId: "route-old",
    currentRouteName: "Lynn Lake Run",
    newRouteId: "route-new",
    newRouteName: "Leaf Rapids Run",
    canChange: true,
    requiresAcknowledgement: false,
    partner: { tripId: "trip-2", tripNumber: "TR-0002", status: "Scheduled", direction: "Inbound" },
    legs: [leg("trip-1", "TR-0001", true, "Outbound"), leg("trip-2", "TR-0002", false, "Inbound")],
    blockers: [],
    warnings: [],
    notices: [],
    ...over,
  } as TripRouteChangePreview;
}

const SENT_DISPATCH = {
  id: "d-1",
  tripId: "trip-1",
  tripNumber: "TR-0001",
  manifestId: "m-1",
  templateId: "tpl-1",
  templateName: "Pickup",
  serviceType: "Community",
  clientId: null,
  bookingId: null,
  bookingReference: null,
  status: "Sent",
  sentAtUtc: "2026-10-05T15:00:00Z",
  recipients: [],
};

let previewBody: TripRouteChangePreview = preview();
let dispatches: Record<string, unknown[]> = {};
let postBodies: unknown[] = [];

beforeEach(() => {
  previewBody = preview();
  dispatches = {};
  postBodies = [];
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
  fetchMock.mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url === "/api/trips/routes") return json(200, ROUTES);
    if (url.startsWith("/api/notifications/emails?")) {
      const tripId = new URLSearchParams(url.split("?")[1]).get("tripId") ?? "";
      return json(200, dispatches[tripId] ?? []);
    }
    if (url === "/api/trips/trip-1/change-route/preview?routeId=route-new") return json(200, previewBody);
    if (url === "/api/trips/trip-1/change-route" && method === "POST") {
      postBodies.push(JSON.parse(String(init!.body)));
      return new Response(null, { status: 204 });
    }
    return json(404, { code: "Test.Unexpected", message: `${method} ${url}` });
  });
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

async function pickNewRoute() {
  await screen.findByRole("option", { name: /Leaf Rapids Run/ });
  fireEvent.change(screen.getByRole("combobox"), { target: { value: "route-new" } });
  await screen.findByTestId("route-change-summary");
}

const submitButton = () => screen.getByText("CHANGE ROUTE");

describe("ChangeRouteModal", () => {
  it("offers active routes only and excludes the current one", async () => {
    render(<ChangeRouteModal trip={TRIP} onClose={() => undefined} onChanged={async () => undefined} />);
    await screen.findByRole("option", { name: /Leaf Rapids Run · Leaf Rapids → Thompson · 250 km/ });
    expect(screen.queryByRole("option", { name: /Lynn Lake Run/ })).toBeNull();
    expect(screen.queryByRole("option", { name: /Retired Run/ })).toBeNull();
  });

  it("renders both legs and the partner line, and a clean preview submits with acknowledgeWarnings false", async () => {
    const onChanged = vi.fn(async () => undefined);
    const onClose = vi.fn();
    render(<ChangeRouteModal trip={TRIP} onClose={onClose} onChanged={onChanged} />);
    await pickNewRoute();

    expect(screen.getAllByTestId("route-change-leg")).toHaveLength(2);
    expect(screen.getByTestId("partner-line").textContent).toBe("Return leg TR-0002 changes too (reversed)");
    expect(screen.getByText("No conflicts")).toBeTruthy();
    expect(screen.queryByRole("checkbox")).toBeNull();

    expect(submitButton().getAttribute("aria-disabled")).toBeNull();
    fireEvent.click(submitButton());
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    expect(postBodies).toEqual([{ routeId: "route-new", acknowledgeWarnings: false }]);
    expect(onChanged).toHaveBeenCalledWith("route-new", "Leaf Rapids Run");
  });

  it("disables CHANGE ROUTE while a blocker exists", async () => {
    previewBody = preview({
      canChange: false,
      blockers: [
        {
          code: "Trips.Trip.RouteChangePartnerNotScheduled",
          message: "The paired leg TR-0002 is InProgress.",
          tripId: "trip-2",
          tripNumber: "TR-0002",
          count: null,
        },
      ],
    });
    render(<ChangeRouteModal trip={TRIP} onClose={() => undefined} onChanged={async () => undefined} />);
    await pickNewRoute();

    expect(screen.getByText("1 blocker")).toBeTruthy();
    expect(screen.getByText("The paired leg TR-0002 is InProgress.")).toBeTruthy();
    expect(submitButton().getAttribute("aria-disabled")).toBe("true");
    fireEvent.click(submitButton());
    expect(postBodies).toEqual([]);
  });

  it("requires the acknowledgement for warnings and sends acknowledgeWarnings true", async () => {
    previewBody = preview({
      requiresAcknowledgement: true,
      warnings: [
        {
          code: "PassengerStopsOffRoute",
          message: "2 passengers on TR-0001 have a pickup or drop-off that is not on Leaf Rapids Run.",
          tripId: "trip-1",
          tripNumber: "TR-0001",
          count: 2,
        },
      ],
    });
    render(<ChangeRouteModal trip={TRIP} onClose={() => undefined} onChanged={async () => undefined} />);
    await pickNewRoute();

    expect(screen.getByText("1 warning")).toBeTruthy();
    expect(screen.getByText(/2 passengers on TR-0001/)).toBeTruthy();
    expect(submitButton().getAttribute("aria-disabled")).toBe("true");

    fireEvent.click(screen.getByRole("checkbox"));
    expect(submitButton().getAttribute("aria-disabled")).toBeNull();
    fireEvent.click(submitButton());
    await waitFor(() => expect(postBodies).toEqual([{ routeId: "route-new", acknowledgeWarnings: true }]));
  });

  it("adds a pickup-email warning when an email was already sent, and requires acknowledgement", async () => {
    dispatches = { "trip-1": [SENT_DISPATCH] };
    render(<ChangeRouteModal trip={TRIP} onClose={() => undefined} onChanged={async () => undefined} />);
    await pickNewRoute();

    const warning = await screen.findByTestId("email-warning");
    expect(warning.textContent).toMatch(/Pickup email sent .+ — times and stops will be out of date/);
    expect(screen.getByText("1 warning")).toBeTruthy();
    expect(submitButton().getAttribute("aria-disabled")).toBe("true");
    fireEvent.click(screen.getByRole("checkbox"));
    expect(submitButton().getAttribute("aria-disabled")).toBeNull();
  });

  it("shows the server's error message inline when the POST fails", async () => {
    fetchMock.mockImplementation(async (input, init) => {
      const url = String(input);
      const method = init?.method ?? "GET";
      if (url === "/api/trips/routes") return json(200, ROUTES);
      if (url.startsWith("/api/notifications/emails?")) return json(200, []);
      if (url.includes("/change-route/preview")) return json(200, previewBody);
      if (url.endsWith("/change-route") && method === "POST") {
        return json(409, { code: "Trips.Trip.ChangedConcurrently", message: "Someone else changed this trip — reload and try again." });
      }
      return json(404, { code: "Test.Unexpected", message: `${method} ${url}` });
    });
    const onChanged = vi.fn(async () => undefined);
    render(<ChangeRouteModal trip={TRIP} onClose={() => undefined} onChanged={onChanged} />);
    await pickNewRoute();
    fireEvent.click(submitButton());
    await screen.findByText("Someone else changed this trip — reload and try again.");
    expect(onChanged).not.toHaveBeenCalled();
  });
});
