import { describe, expect, it } from "vitest";
import {
  latestPickupEmail,
  partnerLegLine,
  pickupEmailLabel,
  routeChangeNeedsAcknowledgement,
  routeChangeOptions,
  routeChangeSubmitEnabled,
  routeChangeSummaryChips,
  windowEndLine,
} from "./routeChange";
import type { RouteRecord, TripRouteChangeFinding, TripRouteChangeLeg, TripRouteChangePreview } from "./api/trips";
import type { EmailDispatchRecord } from "./api/notifications";

function route(over: Partial<RouteRecord>): RouteRecord {
  return {
    id: "r",
    name: "Route",
    stops: [],
    origin: "Lynn Lake",
    destination: "Thompson",
    distanceKm: 320,
    estimatedDurationMinutes: 270,
    requiredLicenceClass: null,
    active: true,
    createdAtUtc: "2026-01-01T00:00:00Z",
    updatedAtUtc: "2026-01-01T00:00:00Z",
    ...over,
  };
}

function leg(over: Partial<TripRouteChangeLeg> = {}): TripRouteChangeLeg {
  return {
    tripId: "t1",
    tripNumber: "TR-0001",
    isRequestedTrip: true,
    status: "Scheduled",
    direction: "Outbound",
    willChange: true,
    currentRouteName: "Old",
    currentOrigin: "Lynn Lake",
    currentDestination: "Thompson",
    currentDistanceKm: 320,
    newOrigin: "Leaf Rapids",
    newDestination: "Thompson",
    newDistanceKm: 250,
    newStops: [],
    windowStart: "06:30:00",
    currentWindowEnd: "11:00:00",
    newWindowEnd: "10:40:00",
    ...over,
  };
}

const finding = (code: string): TripRouteChangeFinding => ({ code, message: code, tripId: null, tripNumber: null, count: null });

function preview(over: Partial<TripRouteChangePreview> = {}): TripRouteChangePreview {
  return {
    tripId: "t1",
    tripNumber: "TR-0001",
    currentRouteId: "old",
    currentRouteName: "Old",
    newRouteId: "new",
    newRouteName: "New",
    canChange: true,
    requiresAcknowledgement: false,
    partner: null,
    legs: [leg()],
    blockers: [],
    warnings: [],
    notices: [],
    ...over,
  };
}

function dispatch(over: Partial<EmailDispatchRecord>): EmailDispatchRecord {
  return {
    id: "d",
    tripId: "t1",
    tripNumber: "TR-0001",
    manifestId: "m1",
    templateId: "tpl",
    templateName: "Pickup",
    serviceType: "Community",
    clientId: null,
    bookingId: null,
    bookingReference: null,
    status: "Sent",
    sentAtUtc: "2026-10-01T15:00:00Z",
    recipients: [],
    ...over,
  } as EmailDispatchRecord;
}

describe("routeChangeOptions", () => {
  it("lists active routes only, excludes the current one, labels corridor + km", () => {
    const opts = routeChangeOptions(
      [
        route({ id: "cur", name: "Current" }),
        route({ id: "off", name: "Retired", active: false }),
        route({ id: "b", name: "Bravo", origin: "Leaf Rapids", distanceKm: 250 }),
        route({ id: "a", name: "Alpha" }),
      ],
      "cur",
    );
    expect(opts).toEqual([
      { value: "a", label: "Alpha · Lynn Lake → Thompson · 320 km" },
      { value: "b", label: "Bravo · Leaf Rapids → Thompson · 250 km" },
    ]);
  });
});

describe("routeChangeSummaryChips", () => {
  it("is a single 'No conflicts' ontime chip when nothing is reported", () => {
    expect(routeChangeSummaryChips(preview(), 0)).toEqual([{ kind: "ontime", label: "No conflicts" }]);
  });

  it("maps blockers → over, warnings (+ local email warnings) → soon, notices → info, with counts", () => {
    const chips = routeChangeSummaryChips(
      preview({ blockers: [finding("b1"), finding("b2")], warnings: [finding("w1")], notices: [finding("n1")] }),
      1,
    );
    expect(chips).toEqual([
      { kind: "over", label: "2 blockers" },
      { kind: "soon", label: "2 warnings" },
      { kind: "info", label: "1 notice" },
    ]);
  });
});

describe("routeChangeSubmitEnabled", () => {
  const base = { routeId: "new", loading: false, busy: false, acknowledged: false, localWarnings: 0 };

  it("is off when the preview is for a different (or no) route than the one picked", () => {
    expect(routeChangeSubmitEnabled({ ...base, routeId: "", preview: preview() })).toBe(false);
    expect(routeChangeSubmitEnabled({ ...base, routeId: "other", preview: preview() })).toBe(false);
  });

  it("is off with no preview, while loading, or while busy", () => {
    expect(routeChangeSubmitEnabled({ ...base, preview: null })).toBe(false);
    expect(routeChangeSubmitEnabled({ ...base, preview: preview(), loading: true })).toBe(false);
    expect(routeChangeSubmitEnabled({ ...base, preview: preview(), busy: true })).toBe(false);
  });

  it("is on for a clean preview", () => {
    expect(routeChangeSubmitEnabled({ ...base, preview: preview() })).toBe(true);
  });

  it("is off while any blocker exists, even acknowledged", () => {
    const p = preview({ canChange: false, blockers: [finding("b")] });
    expect(routeChangeSubmitEnabled({ ...base, preview: p, acknowledged: true })).toBe(false);
  });

  it("needs the acknowledgement for server warnings", () => {
    const p = preview({ requiresAcknowledgement: true, warnings: [finding("w")] });
    expect(routeChangeNeedsAcknowledgement(p, 0)).toBe(true);
    expect(routeChangeSubmitEnabled({ ...base, preview: p })).toBe(false);
    expect(routeChangeSubmitEnabled({ ...base, preview: p, acknowledged: true })).toBe(true);
  });

  it("needs the acknowledgement for a frontend-only email warning", () => {
    expect(routeChangeNeedsAcknowledgement(preview(), 1)).toBe(true);
    expect(routeChangeSubmitEnabled({ ...base, preview: preview(), localWarnings: 1 })).toBe(false);
    expect(routeChangeSubmitEnabled({ ...base, preview: preview(), localWarnings: 1, acknowledged: true })).toBe(true);
  });
});

describe("latestPickupEmail", () => {
  it("returns null with no dispatches or only failed ones", () => {
    expect(latestPickupEmail([], "TR-0001")).toBeNull();
    expect(latestPickupEmail([dispatch({ status: "Failed" })], "TR-0001")).toBeNull();
  });

  it("picks the newest Sent / PartiallyFailed trip-anchored dispatch", () => {
    const w = latestPickupEmail(
      [
        dispatch({ id: "a", sentAtUtc: "2026-10-01T15:00:00Z" }),
        dispatch({ id: "b", status: "PartiallyFailed", sentAtUtc: "2026-10-03T15:00:00Z" }),
        dispatch({ id: "c", status: "Failed", sentAtUtc: "2026-10-05T15:00:00Z" }),
        dispatch({ id: "d", tripId: null, sentAtUtc: "2026-10-06T15:00:00Z" }),
      ],
      "TR-0001",
    );
    expect(w).toEqual({ tripNumber: "TR-0001", sentAtUtc: "2026-10-03T15:00:00Z" });
    expect(pickupEmailLabel(w!, false)).toMatch(/^Pickup email sent .+ — times and stops will be out of date$/);
    expect(pickupEmailLabel(w!, true)).toContain("for TR-0001");
  });
});

describe("partnerLegLine", () => {
  it("is null for an unpaired trip", () => {
    expect(partnerLegLine(preview())).toBeNull();
  });

  it("says the return leg changes too (reversed)", () => {
    const p = preview({
      partner: { tripId: "t2", tripNumber: "TR-0002", status: "Scheduled", direction: "Inbound" },
      legs: [leg(), leg({ tripId: "t2", tripNumber: "TR-0002", isRequestedTrip: false, direction: "Inbound" })],
    });
    expect(partnerLegLine(p)).toBe("Return leg TR-0002 changes too (reversed)");
  });

  it("says when the partner is already on the route", () => {
    const p = preview({
      partner: { tripId: "t2", tripNumber: "TR-0002", status: "Scheduled", direction: "Inbound" },
      legs: [leg(), leg({ tripId: "t2", tripNumber: "TR-0002", isRequestedTrip: false, willChange: false })],
    });
    expect(partnerLegLine(p)).toBe("Return leg TR-0002 is already on this route");
  });

  it("says a partner that has already left or been cancelled keeps its route", () => {
    const p = preview({
      partner: { tripId: "t2", tripNumber: "TR-0002", status: "InProgress", direction: "Inbound" },
      legs: [leg(), leg({ tripId: "t2", tripNumber: "TR-0002", isRequestedTrip: false, willChange: false })],
    });
    expect(partnerLegLine(p)).toBe("Return leg TR-0002 is InProgress — it keeps its route");
  });
});

describe("windowEndLine", () => {
  it("shows the before → after window end", () => {
    expect(windowEndLine(leg())).toBe("Ends 11:00 AM → 10:40 AM");
  });

  it("marks an unchanged or open-ended window", () => {
    expect(windowEndLine(leg({ willChange: false }))).toBe("Ends 11:00 AM (unchanged)");
    expect(windowEndLine(leg({ currentWindowEnd: null, newWindowEnd: null }))).toBe("Open-ended window");
  });
});
