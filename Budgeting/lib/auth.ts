// ---------------------------------------------------------------------------
// Auth module — token lifecycle for the Budgeting Console.
//
// ADAPTED FROM Dispatcher/lib/auth.ts. Four deliberate differences, everything else identical:
//   1. REFRESH_TOKEN_KEY is app-specific, so the two consoles cannot collide if they are ever
//      served from one hostname.
//   2. Imports ./api/transport directly — Dispatcher imports the ./api barrel, which re-exports
//      fleet/trips/drivers/… clients that have no counterpart here.
//   3. No createFirstAdmin / generateAdminInvite / redeemAdminInvite. Account creation belongs
//      to the Dispatch Console: first-run setup is a one-shot global gate and two apps racing
//      for it is a bug factory. checkSetupRequired stays, so this app can say plainly that
//      setup happens elsewhere rather than showing an unsatisfiable login form.
//   4. Adds getClaims/getRole for the role gate (see lib/claims.ts).
//
// Contract (Backend Identity module):
//   POST /api/identity/auth/login    { email, password }  → { accessToken, refreshToken, expiresAtUtc }
//   POST /api/identity/auth/refresh  { refreshToken }     → same shape; BOTH tokens rotate
//   POST /api/identity/auth/logout   { refreshToken }     → 204 (revokes the refresh token)
//   GET  /api/identity/auth/setup-status                  → { setupRequired }
//
// Storage model: the access token (15 min JWT) lives in memory only; the refresh token
// (30 days, single-use) persists in localStorage so a page reload can silently restore the
// session. Because refresh tokens are single-use, refresh is SINGLE-FLIGHT — concurrent 401s
// share one refresh call instead of racing (a second concurrent refresh with an already-rotated
// token would 401 and falsely sign the user out).
// ---------------------------------------------------------------------------

import { ApiError, identityGet, identityRequest } from "./api/transport";
import { decodeAccessToken, type AccessClaims } from "./claims";

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
}

const REFRESH_TOKEN_KEY = "nl.budgeting.refreshToken";

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

/**
 * Claims from the current access token, or null when signed out. Unverified by construction —
 * see lib/claims.ts. Read at call time rather than held in state: the value changes only when
 * the token does, and every path that replaces the token already re-renders through AuthGate.
 */
export function getClaims(): AccessClaims | null {
  return decodeAccessToken(accessToken);
}

/** The signed-in user's role, or null when signed out or the token is unreadable. */
export function getRole(): string | null {
  return getClaims()?.role ?? null;
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
 * First-run check: true only while the backend has no users at all. This console cannot create
 * that first account (see the header note) — it shows SetupPendingScreen pointing at the
 * Dispatch Console instead. Callers treat a thrown error as "not required" (fail safe — never
 * surface the setup screen on an errored check).
 */
export async function checkSetupRequired(): Promise<boolean> {
  const status = await identityGet<{ setupRequired: boolean }>("/api/identity/auth/setup-status");
  return status.setupRequired === true;
}

/**
 * Clears local auth immediately (the UI returns to the login screen), then revokes the refresh
 * token server-side best-effort.
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
 * Rotates the token pair. Single-flight: concurrent callers share one refresh call. Resolves
 * with the new access token. On a definitive rejection (HTTP 401/4xx — revoked or expired) the
 * local session is cleared and listeners are notified; a network failure (status 0) leaves the
 * session intact.
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
 * Access token for an outgoing request, refreshing proactively when the current one is expired
 * (or about to be). Returns null when no session exists.
 */
export async function getValidAccessToken(): Promise<string | null> {
  if (accessToken && Date.now() < accessTokenExpiresAt - EXPIRY_SKEW_MS) {
    return accessToken;
  }
  if (!storedRefreshToken()) return accessToken;
  try {
    return await refreshAccessToken();
  } catch {
    // Definitive failures already cleared local auth; fall through with whatever we have
    // (likely null) and let the request surface its 401.
    return accessToken;
  }
}

/**
 * Silent session restore on app mount: if a refresh token was persisted, exchange it for a
 * fresh pair. Returns whether the session was restored.
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
