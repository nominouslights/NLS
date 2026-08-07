// ---------------------------------------------------------------------------
// Auth module — token lifecycle for the Dispatch Console.
//
// Contract (Backend Identity module):
//   POST /api/identity/auth/login    { email, password }   → { accessToken, refreshToken, expiresAtUtc }
//   POST /api/identity/auth/refresh  { refreshToken }      → same shape; BOTH tokens rotate (old refresh revoked)
//   POST /api/identity/auth/logout   { refreshToken }      → 204 (revokes the refresh token)
//   POST /api/identity/admin/bootstrap-token  (Bearer, Admin role; no body) → { token, expiresAtUtc }
//   POST /api/identity/admin/bootstrap  { token, email, password } → new user id (NOT tokens)
//
// Storage model: the access token (15 min JWT) lives in memory only; the
// refresh token (30 days, single-use) persists in localStorage so a page
// reload can silently restore the session. Because refresh tokens are
// single-use, refresh is SINGLE-FLIGHT — concurrent 401s share one refresh
// call instead of racing (a second concurrent refresh with an already-rotated
// token would 401 and falsely sign the user out).
//
// `identityRequest` (the raw unauthenticated fetch) lives in lib/api.ts so
// there is exactly one fetch/error-parsing implementation. The two modules
// import each other, but only inside function bodies — safe under ESM.
// ---------------------------------------------------------------------------

import { ApiError, identityGet, identityRequest, request } from "./api";

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
}

const REFRESH_TOKEN_KEY = "nl.dispatcher.refreshToken";

/** Refresh proactively when the access token is within this window of expiry. */
const EXPIRY_SKEW_MS = 30_000;

let accessToken: string | null = null;
let accessTokenExpiresAt = 0; // epoch ms

// --- auth-state change notifications (AuthGate subscribes) -----------------

type AuthListener = (authenticated: boolean) => void;
const listeners = new Set<AuthListener>();

export function onAuthChange(listener: AuthListener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function notify(authenticated: boolean) {
  for (const listener of [...listeners]) listener(authenticated);
}

// --- storage helpers (guarded — Next may evaluate this module server-side) --

function storedRefreshToken(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return window.localStorage.getItem(REFRESH_TOKEN_KEY);
  } catch {
    return null;
  }
}

function storeTokens(tokens: AuthTokens) {
  accessToken = tokens.accessToken;
  const exp = Date.parse(tokens.expiresAtUtc);
  accessTokenExpiresAt = Number.isNaN(exp) ? Date.now() + 15 * 60_000 : exp;
  try {
    window.localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
  } catch {
    // storage unavailable — session still works until the tab closes
  }
}

function clearLocalAuth() {
  accessToken = null;
  accessTokenExpiresAt = 0;
  try {
    window.localStorage.removeItem(REFRESH_TOKEN_KEY);
  } catch {
    // ignore
  }
}

// --- public state ----------------------------------------------------------

export function isAuthenticated(): boolean {
  return accessToken !== null;
}

export function getAccessToken(): string | null {
  return accessToken;
}

/** True when a persisted refresh token exists (session may be restorable). */
export function hasStoredSession(): boolean {
  return storedRefreshToken() !== null;
}

// --- token lifecycle -------------------------------------------------------

export async function login(email: string, password: string): Promise<void> {
  const tokens = await identityRequest<AuthTokens>("/api/identity/auth/login", {
    email,
    password,
  });
  storeTokens(tokens);
  notify(true);
}

/**
 * First-run check: true only while the backend has no users at all, meaning the
 * console should show the create-administrator screen instead of the login form.
 * Callers treat a thrown error as "not required" (fail safe — never surface the
 * setup screen on an errored check).
 */
export async function checkSetupRequired(): Promise<boolean> {
  const status = await identityGet<{ setupRequired: boolean }>("/api/identity/auth/setup-status");
  return status.setupRequired === true;
}

/**
 * Creates the very first Admin account and signs straight in with the token pair
 * the backend returns. Only succeeds while no user exists; afterwards the
 * endpoint is permanently closed (409 Identity.User.SetupAlreadyCompleted).
 */
export async function createFirstAdmin(email: string, password: string): Promise<void> {
  const tokens = await identityRequest<AuthTokens>("/api/identity/auth/setup", {
    email,
    password,
  });
  storeTokens(tokens);
  notify(true);
}

// --- admin invites ---------------------------------------------------------

export interface AdminInvite {
  token: string;
  expiresAtUtc: string;
}

/**
 * The Internal-tenant roles an invite can name. Mirrors Roles.Internal in
 * Backend/src/Shared/Kernel/Roles.cs — the backend rejects anything else with
 * Identity.User.InvalidRole, so keep these in step.
 */
export const INVITE_ROLES = [
  "Owner",
  "Dispatcher",
  "Supervisor",
  "Accountant",
  "BoardMember",
  "Driver",
] as const;

export type InviteRole = (typeof INVITE_ROLES)[number];

/**
 * Mints a one-time invite token (authenticated — requires the Owner role). The
 * backend stores only a hash, so the raw token in the response is visible
 * exactly once: single-use, expires 15 minutes after issue.
 *
 * The role is fixed here, at mint time — whoever redeems the invite is
 * anonymous and must not get to name their own privileges. It rides as a query
 * parameter rather than a body because this call sends no body at all;
 * defaulting to Owner server-side keeps a bare call meaning what it always did.
 */
export function generateAdminInvite(role: InviteRole = "Owner"): Promise<AdminInvite> {
  return request<AdminInvite>(
    `/api/identity/admin/bootstrap-token?role=${encodeURIComponent(role)}`,
    { method: "POST" },
  );
}

/**
 * Redeems an admin invite token, creating a new Admin account (anonymous —
 * this is how a brand-new admin gets in). The endpoint returns the new user
 * id, not tokens, so on success we sign straight in with the fresh
 * credentials (mirrors createFirstAdmin landing the user in the console).
 * Failures surface as ApiError — 401 invalid/expired/used token,
 * 409 duplicate email.
 */
export async function redeemAdminInvite(
  token: string,
  email: string,
  password: string,
): Promise<void> {
  await identityRequest<unknown>("/api/identity/admin/bootstrap", {
    token,
    email,
    password,
  });
  await login(email, password);
}

/**
 * Clears local auth immediately (the UI returns to the login screen), then
 * revokes the refresh token server-side best-effort.
 */
export async function logout(): Promise<void> {
  const refreshToken = storedRefreshToken();
  clearLocalAuth();
  notify(false);
  if (refreshToken) {
    try {
      await identityRequest<void>("/api/identity/auth/logout", { refreshToken });
    } catch {
      // token will age out server-side; local state is already cleared
    }
  }
}

let refreshInFlight: Promise<string> | null = null;

/**
 * Rotates the token pair. Single-flight: concurrent callers share one refresh
 * call. Resolves with the new access token. On a definitive rejection (HTTP
 * 401/4xx — revoked or expired) the local session is cleared and listeners are
 * notified; a network failure (status 0) leaves the session intact.
 */
export function refreshAccessToken(): Promise<string> {
  if (!refreshInFlight) {
    refreshInFlight = doRefresh().finally(() => {
      refreshInFlight = null;
    });
  }
  return refreshInFlight;
}

async function doRefresh(): Promise<string> {
  const refreshToken = storedRefreshToken();
  if (!refreshToken) {
    throw new ApiError("Auth.NoSession", "No active session — please sign in.", 401);
  }
  try {
    const tokens = await identityRequest<AuthTokens>("/api/identity/auth/refresh", {
      refreshToken,
    });
    storeTokens(tokens); // BOTH tokens rotate — always persist the new pair
    return tokens.accessToken;
  } catch (err) {
    if (err instanceof ApiError && err.status !== 0) {
      // Refresh token revoked/expired — the session is over.
      clearLocalAuth();
      notify(false);
    }
    throw err;
  }
}

/**
 * Access token for an outgoing request, refreshing proactively when the
 * current one is expired (or about to). Returns null when no session exists.
 */
export async function getValidAccessToken(): Promise<string | null> {
  if (accessToken && Date.now() < accessTokenExpiresAt - EXPIRY_SKEW_MS) {
    return accessToken;
  }
  if (!storedRefreshToken()) return accessToken;
  try {
    return await refreshAccessToken();
  } catch {
    // Definitive failures already cleared local auth; fall through with
    // whatever we have (likely null) and let the request surface its 401.
    return accessToken;
  }
}

/**
 * Silent session restore on app mount: if a refresh token was persisted,
 * exchange it for a fresh pair. Returns whether the session was restored.
 */
export async function restoreSession(): Promise<boolean> {
  if (!storedRefreshToken()) return false;
  try {
    await refreshAccessToken();
    return true;
  } catch {
    return false;
  }
}
