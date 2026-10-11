import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { authorizeQbo, completeQboConnection, disconnectQbo, getQboConnection } from "./qbo";
import { ApiError } from "./transport";

// What each QuickBooks connection request puts on the wire — route, method, body — and that a
// refusal reaches the caller with the server's own words. The routes are the four qbo/connection
// endpoints in Backend/src/Budgeting/Infrastructure/Endpoints/BudgetingEndpoints.cs, all inside
// the /api/budgeting group that carries the BudgetAccess policy. The messages below are copied
// from QboConnectionErrors.cs; the console shows them verbatim, so a test pins that nothing
// between the response and the caller rewrites them.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("../auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

function callPath(n = 0): string {
  return String(fetchMock.mock.calls[n]?.[0]);
}

function callInit(n = 0): RequestInit {
  return (fetchMock.mock.calls[n]?.[1] ?? {}) as RequestInit;
}

async function refusal(p: Promise<unknown>): Promise<ApiError> {
  try {
    await p;
  } catch (e) {
    if (e instanceof ApiError) return e;
    throw e;
  }
  throw new Error("expected the request to be refused");
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

const CONNECTION = {
  status: "Active",
  realmId: "9341452938471234",
  companyName: "Northern Link Sandbox Co",
  environment: "Sandbox",
  connectedBy: "5f2b1e1c-0000-4000-8000-000000000009",
  connectedByName: "Ada Planner",
  connectedByEmail: "ada@example.com",
  connectedAtUtc: "2026-10-01T15:00:00+00:00",
  refreshTokenExpiresAtUtc: "2027-01-09T15:00:00+00:00",
  lastSyncAtUtc: null,
  lastErrorCode: null,
};

describe("getQboConnection", () => {
  it("GETs qbo/connection — tenant-wide, never under a period — and returns the body as-is", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, CONNECTION));

    const c = await getQboConnection();

    expect(callPath()).toBe("/api/budgeting/qbo/connection");
    expect(callInit().method ?? "GET").toBe("GET");
    expect(c).toEqual(CONNECTION);
  });

  it("passes NotConnected through with its nulls (a 200, never a 404)", async () => {
    const none = { ...CONNECTION, status: "NotConnected" };
    for (const k of Object.keys(none) as (keyof typeof none)[]) if (k !== "status") none[k] = null as never;
    fetchMock.mockResolvedValueOnce(jsonResponse(200, none));

    const c = await getQboConnection();

    expect(c.status).toBe("NotConnected");
    expect(c.companyName).toBeNull();
  });

  it("goes through the transport's refresh-and-retry-once on a 401", async () => {
    getAccessToken.mockReturnValue("access-1");
    refreshAccessToken.mockResolvedValueOnce("access-2");
    fetchMock
      .mockResolvedValueOnce(jsonResponse(401, {}))
      .mockResolvedValueOnce(jsonResponse(200, CONNECTION));

    await getQboConnection();

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(refreshAccessToken).toHaveBeenCalledTimes(1);
    expect((callInit(1).headers as Record<string, string>).Authorization).toBe("Bearer access-2");
  });
});

describe("authorizeQbo", () => {
  it("POSTs qbo/connection/authorize with no body and returns authorizeUrl", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, { authorizeUrl: "https://appcenter.intuit.com/connect/oauth2?state=s1" }),
    );

    const url = await authorizeQbo();

    expect(callPath()).toBe("/api/budgeting/qbo/connection/authorize");
    expect(callInit().method).toBe("POST");
    expect(callInit().body).toBeUndefined();
    expect(url).toBe("https://appcenter.intuit.com/connect/oauth2?state=s1");
  });

  it("surfaces 503 NotConfigured verbatim", async () => {
    const message =
      "QuickBooks Online is not set up on this server yet. Ask the platform administrator to add the Intuit app credentials.";
    fetchMock.mockResolvedValueOnce(jsonResponse(503, { code: "Budgeting.Qbo.NotConfigured", message }));

    const e = await refusal(authorizeQbo());

    expect(e.status).toBe(503);
    expect(e.code).toBe("Budgeting.Qbo.NotConfigured");
    expect(e.message).toBe(message);
  });
});

describe("completeQboConnection", () => {
  it("POSTs { code, state, realmId } to qbo/connection/complete and resolves on 204", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await completeQboConnection({ code: "c-1", state: "s-1", realmId: "9341452938471234" });

    expect(callPath()).toBe("/api/budgeting/qbo/connection/complete");
    expect(callInit().method).toBe("POST");
    expect(JSON.parse(String(callInit().body))).toEqual({
      code: "c-1",
      state: "s-1",
      realmId: "9341452938471234",
    });
  });

  it("sends a missing value as an explicit null, so the server's CallbackIncomplete names it", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await completeQboConnection({ code: "c-1", state: null, realmId: null });

    expect(JSON.parse(String(callInit().body))).toEqual({ code: "c-1", state: null, realmId: null });
  });

  it.each([
    [400, "Budgeting.Qbo.StateInvalid", "This QuickBooks sign-in link is not valid for you, or it has already been used. Start the connection again."],
    [400, "Budgeting.Qbo.StateExpired", "This QuickBooks sign-in took longer than 10 minutes. Start the connection again."],
    [400, "Budgeting.Qbo.CallbackIncomplete", "QuickBooks did not send back everything needed to finish connecting. Start the connection again."],
    [400, "Budgeting.Qbo.RealmIdInvalid", "QuickBooks sent back a company id this platform does not recognise. Start the connection again."],
    [409, "Budgeting.Qbo.DifferentCompany", "This workspace has already been connected to a different QuickBooks company. Connect that same company again."],
    [409, "Budgeting.Qbo.HomeCurrencyNotCad", "That QuickBooks company's home currency is not Canadian dollars. Budgets here are in CAD, so it cannot be connected."],
    [502, "Budgeting.Qbo.TokenExchangeFailed", "QuickBooks did not accept the sign-in. Start the connection again; if it keeps failing, try later."],
    [502, "Budgeting.Qbo.CompanyLookupFailed", "Signed in to QuickBooks, but its company details could not be read. Try connecting again later."],
    [503, "Budgeting.Qbo.NotConfigured", "QuickBooks Online is not set up on this server yet. Ask the platform administrator to add the Intuit app credentials."],
  ])("surfaces %i %s verbatim", async (status, code, message) => {
    fetchMock.mockResolvedValueOnce(jsonResponse(status, { code, message }));

    const e = await refusal(completeQboConnection({ code: "c", state: "s", realmId: "1" }));

    expect(e.status).toBe(status);
    expect(e.code).toBe(code);
    expect(e.message).toBe(message);
  });
});

describe("disconnectQbo", () => {
  it("DELETEs qbo/connection with no body and resolves on 204", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await disconnectQbo();

    expect(callPath()).toBe("/api/budgeting/qbo/connection");
    expect(callInit().method).toBe("DELETE");
    expect(callInit().body).toBeUndefined();
  });

  it("surfaces 404 NotConnected verbatim", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(404, { code: "Budgeting.Qbo.NotConnected", message: "QuickBooks Online is not connected." }),
    );

    const e = await refusal(disconnectQbo());

    expect(e.status).toBe(404);
    expect(e.code).toBe("Budgeting.Qbo.NotConnected");
    expect(e.message).toBe("QuickBooks Online is not connected.");
  });
});
