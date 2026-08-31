// COPIED FROM Dispatcher/lib/api/transport.ts — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
// ---------------------------------------------------------------------------
// Transport layer — fetch plumbing shared by every domain API client
// (fleet, trips, drivers, clients, billing, maintenance). No endpoint or
// contract shapes live here.
// ---------------------------------------------------------------------------

// Relative — requests go to this app's own origin (/api/...) and Next.js's rewrite
// (next.config.ts) proxies them server-side to the real Fleet API. Same-origin means the
// browser never needs CORS. Override only for exotic setups (e.g. a static export) where
// the rewrite proxy isn't available.
export const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL ?? "";

/** Error body shape the backend returns: { code, message }. */
export class ApiError extends Error {
  readonly code: string;
  readonly status: number;

  constructor(code: string, message: string, status: number) {
    super(message);
    this.name = "ApiError";
    this.code = code;
    this.status = status;
  }
}

/** Low-level fetch: JSON headers + optional bearer token. Throws only on network failure. */
async function send(path: string, init: RequestInit | undefined, token: string | null): Promise<Response> {
  const headers: Record<string, string> = {
    ...(init?.headers as Record<string, string> | undefined),
  };
  // Only default to JSON content-type if body is not FormData (FormData needs browser-set multipart boundary)
  if (!(init?.body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }
  if (token) headers.Authorization = `Bearer ${token}`;
  try {
    return await fetch(`${API_BASE}${path}`, { ...init, headers });
  } catch {
    throw new ApiError(
      "Network.Unreachable",
      `Cannot reach the API at ${API_BASE}. Is the backend running?`,
      0,
    );
  }
}

/** Parses the backend's { code, message } error body, with an HTTP fallback. */
async function parseError(res: Response): Promise<ApiError> {
  let code = `Http.${res.status}`;
  let message = res.statusText || `Request failed with status ${res.status}`;
  try {
    const body = (await res.json()) as { code?: string; message?: string };
    if (body?.code) code = body.code;
    if (body?.message) message = body.message;
  } catch {
    // no structured error body — keep the HTTP fallback
  }
  return new ApiError(code, message, res.status);
}

/**
 * Authenticated fetch with token refresh + 401 retry logic (returns raw Response).
 *
 * `retryOn401` must be false for endpoints where a 401 means "the thing you sent was
 * wrong" rather than "your token expired" — the step-up password check is the one such
 * endpoint today. Refreshing there is actively harmful: refresh tokens are single-use, so
 * every mistyped password would rotate the session, and a refresh that loses a race calls
 * clearLocalAuth() — signing the dispatcher out for a typo.
 */
async function authenticatedFetch(
  path: string,
  init?: RequestInit,
  retryOn401 = true,
): Promise<Response> {
  const { getAccessToken, getValidAccessToken, refreshAccessToken } = await import("../auth");

  const token = await getValidAccessToken();
  let res = await send(path, init, token);

  if (res.status === 401 && retryOn401) {
    let retryToken: string | null = null;
    const current = getAccessToken();
    if (current && current !== token) {
      retryToken = current;
    } else {
      try {
        retryToken = await refreshAccessToken();
      } catch {
        retryToken = null;
      }
    }
    if (retryToken) res = await send(path, init, retryToken);
  }

  return res;
}

/**
 * Unauthenticated POST for the Identity auth endpoints (login/refresh/logout/setup).
 * Used by lib/auth.ts — no bearer header, no 401 retry.
 */
export async function identityRequest<T>(path: string, body: unknown): Promise<T> {
  const res = await send(path, { method: "POST", body: JSON.stringify(body) }, null);
  if (!res.ok) throw await parseError(res);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

/**
 * Unauthenticated GET for the Identity first-run setup check. Separate from
 * `identityRequest` (which is POST-only) and from `request` (which attaches a
 * bearer token) — this one runs before any session can exist.
 */
export async function identityGet<T>(path: string): Promise<T> {
  const res = await send(path, { method: "GET" }, null);
  if (!res.ok) throw await parseError(res);
  return (await res.json()) as T;
}

/**
 * Authenticated request. Attaches `Authorization: Bearer <accessToken>`
 * (refreshing proactively when the token is near expiry). On a 401, refreshes
 * once and retries the original request once with the new token; if the
 * refresh fails, lib/auth.ts clears the session (surfacing the login screen)
 * and the original 401 is thrown. Never loops.
 *
 * Pass `{ retryOn401: false }` when a 401 from the endpoint means the request was
 * wrong rather than the token stale — see authenticatedFetch.
 *
 * Exported for lib/auth.ts (admin invite minting needs an authenticated call);
 * the domain endpoint wrappers remain the preferred surface for feature code.
 */
export async function request<T>(
  path: string,
  init?: RequestInit,
  opts?: { retryOn401?: boolean },
): Promise<T> {
  const res = await authenticatedFetch(path, init, opts?.retryOn401 ?? true);

  if (!res.ok) throw await parseError(res);
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

/**
 * Authenticated blob fetch (e.g. image download). Returns raw bytes without parsing.
 * Handles 401 refresh/retry like `request<T>`.
 */
export async function requestBlob(path: string, init?: RequestInit): Promise<Blob> {
  const res = await authenticatedFetch(path, init);

  if (!res.ok) throw await parseError(res);
  return await res.blob();
}
