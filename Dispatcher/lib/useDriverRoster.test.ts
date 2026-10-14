import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, renderHook, waitFor } from "@testing-library/react";
import { loadDriverRoster, useDriverRoster } from "./useDriverRoster";

// The one roster + clearance loader behind REASSIGN (AssignModal), the
// return-leg modal and CORRECT DRIVER. REASSIGN's contract must not move:
// Active drivers only, clearance as of today, a failed clearance fetch warns
// rather than fails. fetch is mocked at the transport seam.

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

const driver = (id: string, status: "Active" | "Inactive" | "Deactivated") => ({
  id,
  name: id,
  licenceClass: "Class 2",
  status,
});

const DRIVERS = [driver("a1", "Active"), driver("a2", "Active"), driver("i1", "Inactive"), driver("d1", "Deactivated")];

const clearance = (over: Record<string, unknown> = {}) => ({
  id: "c",
  driverId: "x",
  title: "Site access",
  clientName: "Alamos Gold",
  expiry: null,
  grantedAtUtc: "2026-01-05T15:00:00Z",
  ...over,
});

let clearancesById: Record<string, unknown[] | "fail"> = {};
let clearanceRequests: string[] = [];
let driversResponse: () => Response = () => json(200, DRIVERS);

beforeEach(() => {
  clearancesById = {};
  clearanceRequests = [];
  driversResponse = () => json(200, DRIVERS);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date("2026-10-20T12:00:00"));
  fetchMock.mockImplementation(async (input) => {
    const url = String(input);
    if (url === "/api/drivers") return driversResponse();
    const m = /^\/api\/drivers\/([^/]+)\/clearances$/.exec(url);
    if (m) {
      clearanceRequests.push(m[1]);
      const rows = clearancesById[m[1]];
      if (rows === "fail") return json(500, { code: "Boom", message: "boom" });
      return json(200, rows ?? []);
    }
    return json(404, { code: "Test.Unexpected", message: url });
  });
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  cleanup();
  vi.useRealTimers();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

const summary = (rows: Awaited<ReturnType<typeof loadDriverRoster>>) =>
  rows.map((r) => `${r.driver.id}:${r.cleared ? "cleared" : "uncleared"}`);

describe("loadDriverRoster — REASSIGN / return leg (include: active, as of today)", () => {
  it("lists Active drivers only and fetches clearances only for them", async () => {
    clearancesById = { a1: [clearance()] };
    const rows = await loadDriverRoster({ clientName: "Alamos Gold", include: "active" });
    expect(summary(rows)).toEqual(["a1:cleared", "a2:uncleared"]);
    expect(clearanceRequests.sort()).toEqual(["a1", "a2"]);
  });

  it("defaults to today: a clearance expired before today does not count", async () => {
    clearancesById = { a1: [clearance({ expiry: "2026-10-15" })] };
    const rows = await loadDriverRoster({ clientName: "Alamos Gold", include: "active" });
    expect(summary(rows)).toEqual(["a1:uncleared", "a2:uncleared"]);
  });

  it("a failed clearance fetch degrades to uncleared (warn), not a failed roster", async () => {
    clearancesById = { a1: "fail", a2: [clearance()] };
    const rows = await loadDriverRoster({ clientName: "Alamos Gold", include: "active" });
    expect(summary(rows)).toEqual(["a1:uncleared", "a2:cleared"]);
  });
});

describe("loadDriverRoster — CORRECT DRIVER (include: all, as of the trip date)", () => {
  it("lists every driver on record except the excluded one", async () => {
    const rows = await loadDriverRoster({ clientName: "Alamos Gold", include: "all", excludeDriverId: "a1", asOf: "2026-10-10" });
    expect(rows.map((r) => r.driver.id)).toEqual(["a2", "i1", "d1"]);
    expect(clearanceRequests.sort()).toEqual(["a2", "d1", "i1"]);
  });

  it("judges clearance on asOf, not today — both directions", async () => {
    clearancesById = {
      a2: [clearance({ expiry: "2026-10-15" })], // expired since the trip → cleared then
      i1: [clearance({ grantedAtUtc: "2026-10-12T15:00:00Z" })], // granted after the trip → not cleared then
    };
    const rows = await loadDriverRoster({ clientName: "Alamos Gold", include: "all", excludeDriverId: "a1", asOf: "2026-10-10" });
    expect(summary(rows)).toEqual(["a2:cleared", "i1:uncleared", "d1:uncleared"]);
  });
});

describe("loadDriverRoster — clientless trip", () => {
  it("makes no clearance requests and marks everyone cleared", async () => {
    const rows = await loadDriverRoster({ clientName: null, include: "all" });
    expect(clearanceRequests).toEqual([]);
    expect(rows.every((r) => r.cleared)).toBe(true);
    expect(rows).toHaveLength(4);
  });
});

describe("useDriverRoster", () => {
  it("is null while loading, then the roster", async () => {
    const { result } = renderHook(() => useDriverRoster({ clientName: "Alamos Gold", include: "active" }));
    expect(result.current.roster).toBeNull();
    await waitFor(() => expect(result.current.roster).not.toBeNull());
    expect(result.current.roster!.map((r) => r.driver.id)).toEqual(["a1", "a2"]);
    expect(result.current.error).toBeNull();
  });

  it("surfaces the drivers request's server message verbatim", async () => {
    driversResponse = () => json(503, { code: "Drivers.Unavailable", message: "Drivers are unavailable." });
    const { result } = renderHook(() => useDriverRoster({ clientName: "Alamos Gold", include: "active" }));
    await waitFor(() => expect(result.current.error).toBe("Drivers are unavailable."));
    expect(result.current.roster).toBeNull();
  });
});
