import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ApiError, identityGet, request } from "./transport";

// transport.ts is a COPIED file (see Budgeting/CLAUDE.md's manifest) — this test is not, so it
// may live here. It exists because transport.ts's doc comment on `request<T>` makes a promise
// that nothing enforced: on a 401 it "refreshes once and retries the original request once...
// Never loops." An unbounded refresh loop against Identity is the worst failure mode this app
// has — every screen mounts through it, and the retry budget is invisible from the call site.
//
// The 401 path lives in `authenticatedFetch`, which pulls lib/auth in via a dynamic
// `import("../auth")`. Mocking that specifier from this file resolves to the same module id,
// so the token lifecycle is under the test's control without a backend or a browser.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("../auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

/** A JSON response with an explicit status — the shape the backend actually returns. */
function jsonResponse(status: number, body: unknown, statusText?: string): Response {
  return new Response(JSON.stringify(body), {
    status,
    statusText,
    headers: { "Content-Type": "application/json" },
  });
}

/** The backend's { code, message } error envelope. */
function errorResponse(status: number, code: string, message: string): Response {
  return jsonResponse(status, { code, message });
}

/** Authorization header actually sent on the nth (0-based) fetch call. */
function bearerOnCall(n: number): string | undefined {
  const init = fetchMock.mock.calls[n]?.[1] as RequestInit | undefined;
  return (init?.headers as Record<string, string> | undefined)?.Authorization;
}

/** Path actually requested on the nth (0-based) fetch call. */
function pathOnCall(n: number): string {
  return String(fetchMock.mock.calls[n]?.[0]);
}

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  // The common case: a live, non-expired access token, and no concurrent refresh has landed.
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("request — the 401 refresh-and-retry path", () => {
  it("refreshes once and retries once, with the new token", async () => {
    refreshAccessToken.mockResolvedValue("access-2");
    fetchMock
      .mockResolvedValueOnce(errorResponse(401, "Auth.TokenExpired", "Token expired."))
      .mockResolvedValueOnce(jsonResponse(200, [{ id: "p1" }]));

    await expect(request("/api/budgeting/periods")).resolves.toEqual([{ id: "p1" }]);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(refreshAccessToken).toHaveBeenCalledTimes(1);
    expect(bearerOnCall(0)).toBe("Bearer access-1");
    expect(bearerOnCall(1)).toBe("Bearer access-2");
  });

  it("gives up after the retry rather than looping — exactly two attempts, one refresh", async () => {
    // The assertion the doc comment's "Never loops" is worth: a second 401 must surface as an
    // error, not trigger another refresh. Anything other than 2 here is an unbounded retry
    // budget pointed at Identity.
    refreshAccessToken.mockResolvedValue("access-2");
    fetchMock
      .mockResolvedValueOnce(errorResponse(401, "Auth.TokenExpired", "Token expired."))
      .mockResolvedValueOnce(errorResponse(401, "Auth.TokenExpired", "Token expired."));

    await expect(request("/api/budgeting/periods")).rejects.toThrow(ApiError);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(refreshAccessToken).toHaveBeenCalledTimes(1);
  });

  it("throws the original 401 when the refresh itself fails, without a second attempt", async () => {
    // lib/auth's doRefresh has already cleared the session by this point (surfacing the login
    // screen); transport's job is only to stop trying.
    refreshAccessToken.mockRejectedValue(
      new ApiError("Auth.RefreshRejected", "Session expired — please sign in.", 401),
    );
    fetchMock.mockResolvedValueOnce(
      errorResponse(401, "Auth.TokenExpired", "Token expired."),
    );

    await expect(request("/api/budgeting/periods")).rejects.toMatchObject({
      code: "Auth.TokenExpired",
      status: 401,
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("reuses a token a concurrent caller already refreshed instead of refreshing again", async () => {
    // Single-flight lives in lib/auth; this is transport's half of it. Two screens mounting at
    // once must not each burn a rotating refresh token.
    getAccessToken.mockReturnValue("access-2");
    fetchMock
      .mockResolvedValueOnce(errorResponse(401, "Auth.TokenExpired", "Token expired."))
      .mockResolvedValueOnce(jsonResponse(200, []));

    await expect(request("/api/budgeting/codes")).resolves.toEqual([]);

    expect(refreshAccessToken).not.toHaveBeenCalled();
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(bearerOnCall(1)).toBe("Bearer access-2");
  });

  it("does not retry a non-401 failure", async () => {
    fetchMock.mockResolvedValueOnce(errorResponse(500, "Server.Error", "Something broke."));

    await expect(request("/api/budgeting/periods")).rejects.toThrow(ApiError);

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(refreshAccessToken).not.toHaveBeenCalled();
  });
});

describe("ApiError construction", () => {
  // Both Console.tsx and screens/BudgetCodes.tsx branch on `e instanceof ApiError` to decide
  // whether to show the server's own message or a generic one, so the class identity and the
  // { code, status } pair are load-bearing, not incidental.

  it("carries the backend's code and message through verbatim", async () => {
    fetchMock.mockResolvedValueOnce(
      errorResponse(409, "BudgetCode.InUse", "Retire this code instead — it has been used."),
    );

    const err = await request("/api/budgeting/codes/abc").catch((e: unknown) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect(err).toMatchObject({
      name: "ApiError",
      code: "BudgetCode.InUse",
      message: "Retire this code instead — it has been used.",
      status: 409,
    });
  });

  it("falls back to Http.<status> + statusText when there is no structured body", async () => {
    fetchMock.mockResolvedValueOnce(
      new Response("<html>502</html>", { status: 502, statusText: "Bad Gateway" }),
    );

    const err = await request("/api/budgeting/periods").catch((e: unknown) => e);

    expect(err).toMatchObject({ code: "Http.502", message: "Bad Gateway", status: 502 });
  });

  it("keeps the HTTP fallback when only one of code/message is present", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { message: "Ordinal out of range." }, "Bad Request"));

    const err = await request("/api/budgeting/periods").catch((e: unknown) => e);

    expect(err).toMatchObject({
      code: "Http.400",
      message: "Ordinal out of range.",
      status: 400,
    });
  });

  it("reports an unreachable API as status 0, not as an HTTP failure", async () => {
    // The API being down is the single most common local-dev failure. Status 0 is what tells a
    // screen this is not a server rejection, and `request` must not swallow it as a null result.
    fetchMock.mockRejectedValueOnce(new TypeError("Failed to fetch"));

    const err = await request("/api/budgeting/periods").catch((e: unknown) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect(err).toMatchObject({ code: "Network.Unreachable", status: 0 });
    expect((err as ApiError).message).toContain("Is the backend running?");
    expect(refreshAccessToken).not.toHaveBeenCalled();
  });

  it("reports an unreachable API the same way on the unauthenticated paths", async () => {
    fetchMock.mockRejectedValueOnce(new TypeError("Failed to fetch"));

    await expect(identityGet("/api/identity/setup/status")).rejects.toMatchObject({
      code: "Network.Unreachable",
      status: 0,
    });
  });
});

describe("request — success shapes", () => {
  it("returns undefined for a 204 rather than trying to parse a body", async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }));

    await expect(request<void>("/api/budgeting/codes/abc", { method: "DELETE" })).resolves.toBeUndefined();
  });

  it("attaches the bearer token and a JSON content type", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: "p1" }));

    await request("/api/budgeting/periods", { method: "POST", body: "{}" });

    const init = fetchMock.mock.calls[0][1] as RequestInit;
    const headers = init.headers as Record<string, string>;
    expect(headers.Authorization).toBe("Bearer access-1");
    expect(headers["Content-Type"]).toBe("application/json");
    expect(pathOnCall(0)).toBe("/api/budgeting/periods");
  });

  it("sends no Authorization header when there is no session", async () => {
    getValidAccessToken.mockResolvedValue(null);
    fetchMock.mockResolvedValueOnce(jsonResponse(200, {}));

    await request("/api/budgeting/periods");

    expect(bearerOnCall(0)).toBeUndefined();
  });
});
