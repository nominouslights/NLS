import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { listTrips, TripListParamsError, type TripListParams, type TripStatus } from "./trips";

// listTrips on the wire: the multi-status filter serialises as ONE comma-joined
// `statuses` param, and the two caller bugs the API would 400 on (status AND
// statuses; an empty statuses list) are stopped before any request is sent.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("../auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

const emptyPage = () =>
  new Response(JSON.stringify({ items: [], page: null, pageSize: null, totalCount: 0 }), {
    status: 200,
    headers: { "Content-Type": "application/json" },
  });

const callUrl = (n = 0) => new URL(String(fetchMock.mock.calls[n]?.[0]), "http://console.test");

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
  fetchMock.mockImplementation(async () => emptyPage());
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

const OPEN: readonly TripStatus[] = ["Scheduled", "InProgress", "ReadyForBilling", "Invoiced"];

describe("listTrips — statuses filter", () => {
  it("sends statuses as ONE comma-joined param, and no `status`", async () => {
    await listTrips({ to: "2026-10-31", clientId: "c1", statuses: OPEN });
    const raw = String(fetchMock.mock.calls[0]?.[0]);
    expect(raw).toBe(
      "/api/trips?to=2026-10-31&statuses=Scheduled%2CInProgress%2CReadyForBilling%2CInvoiced&clientId=c1",
    );
    const url = callUrl();
    expect(url.searchParams.getAll("statuses")).toEqual(["Scheduled,InProgress,ReadyForBilling,Invoiced"]);
    expect(url.searchParams.has("status")).toBe(false);
  });

  it("sends a single-entry statuses list as-is", async () => {
    await listTrips({ statuses: ["Invoiced"] });
    expect(callUrl().searchParams.get("statuses")).toBe("Invoiced");
  });

  it("omits statuses entirely when undefined (and `status` still works alone)", async () => {
    await listTrips({ status: "Scheduled" });
    const url = callUrl();
    expect(url.searchParams.get("status")).toBe("Scheduled");
    expect(url.searchParams.has("statuses")).toBe(false);
  });

  it("throws on status + statuses together, before any request", async () => {
    // The type forbids this; the cast stands in for a caller that slipped past it.
    const both = { status: "Scheduled", statuses: OPEN } as unknown as TripListParams;
    await expect(listTrips(both)).rejects.toThrow(/either `status` or `statuses`/);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("throws on an empty statuses list rather than widening to every status", async () => {
    await expect(listTrips({ statuses: [] })).rejects.toThrow(/must not be empty/);
    await expect(listTrips({ statuses: [] })).rejects.toBeInstanceOf(TripListParamsError);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("guards on what is actually SENT: a blank/null status beside statuses is no conflict", async () => {
    // Serialisation drops a falsy `status`, so it is not 'both'.
    await listTrips({ status: null, statuses: OPEN } as unknown as TripListParams);
    await listTrips({ status: "", statuses: OPEN } as unknown as TripListParams);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("treats statuses: null as absent, never a .length crash", async () => {
    await listTrips({ status: "Scheduled", statuses: null } as unknown as TripListParams);
    const url = callUrl();
    expect(url.searchParams.get("status")).toBe("Scheduled");
    expect(url.searchParams.has("statuses")).toBe(false);
  });

  it("rejects status + statuses at the type level", () => {
    // @ts-expect-error — status and statuses are mutually exclusive.
    const bad: TripListParams = { status: "Scheduled", statuses: OPEN };
    expect(bad).toBeDefined();
  });
});
