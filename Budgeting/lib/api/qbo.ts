import { request } from "./transport";
import type { StatusKind } from "@/lib/theme";

// ---------------------------------------------------------------------------
// QuickBooks Online connection client — contract owned by Backend/ (Budgeting module,
// BudgetingEndpoints.cs, the four qbo/connection routes in the BudgetAccess group). Shapes
// mirror QboConnectionResponse, QboAuthorizeResponse and CompleteQboConnectionRequest exactly
// (JSON camelCase, enums as PascalCase strings). Separate from budgeting.ts because the
// connection is tenant-wide, never under a period.
//
// Read-only by owner decision (2026-10-08): the platform reads QuickBooks and never writes to
// it. No token, and nothing derived from one, is ever on these shapes — tokens live only in the
// API's encrypted vault. Do not invent fields; extend only when the backend contract changes.
// ---------------------------------------------------------------------------

/**
 * QboConnectionResponse.Status. `NotConnected` is the response's own constant (never a domain
 * state); the other three are QboConnectionStatus's members.
 */
export type QboConnectionStatus = "NotConnected" | "Active" | "NeedsReconnect" | "Disconnected";

/** QboEnvironment, persisted by name. */
export type QboEnvironment = "Sandbox" | "Production";

/** Mirrors QboConnectionResponse. Every field but `status` is null when NotConnected. */
export interface QboConnection {
  status: QboConnectionStatus;
  realmId: string | null;
  companyName: string | null;
  environment: QboEnvironment | null;
  connectedBy: string | null;
  /** From the user replica — null when the person has not set a name. */
  connectedByName: string | null;
  connectedByEmail: string | null;
  connectedAtUtc: string | null;
  /** Null when Disconnected (the server blanks it) and when NotConnected. */
  refreshTokenExpiresAtUtc: string | null;
  /** Always null until the expense import ships. */
  lastSyncAtUtc: string | null;
  lastErrorCode: string | null;
}

/** The three values Intuit puts on the callback's query string — CompleteQboConnectionRequest. */
export interface QboCallbackInput {
  code: string | null;
  state: string | null;
  realmId: string | null;
}

const CONNECTION = "/api/budgeting/qbo/connection";

/** GET qbo/connection → always 200 (NotConnected rather than a 404). */
export function getQboConnection(): Promise<QboConnection> {
  return request<QboConnection>(CONNECTION);
}

/**
 * POST qbo/connection/authorize (no body) → the Intuit URL to send the browser to. 503
 * Budgeting.Qbo.NotConfigured when the server has no Intuit credentials or vault key.
 */
export async function authorizeQbo(): Promise<string> {
  const res = await request<{ authorizeUrl: string }>(`${CONNECTION}/authorize`, { method: "POST" });
  return res.authorizeUrl;
}

/**
 * POST qbo/connection/complete → 204. Refusals (all Budgeting.Qbo.*): 400 StateInvalid /
 * StateExpired / CallbackIncomplete / RealmIdInvalid, 409 DifferentCompany / HomeCurrencyNotCad,
 * 502 TokenExchangeFailed / CompanyLookupFailed, 503 NotConfigured. Always sends all three keys;
 * a missing one is null, which the server answers with CallbackIncomplete.
 */
export async function completeQboConnection(input: QboCallbackInput): Promise<void> {
  const body: QboCallbackInput = { code: input.code, state: input.state, realmId: input.realmId };
  await request<void>(`${CONNECTION}/complete`, { method: "POST", body: JSON.stringify(body) });
}

/** DELETE qbo/connection → 204. 404 Budgeting.Qbo.NotConnected when nothing is live. */
export async function disconnectQbo(): Promise<void> {
  await request<void>(CONNECTION, { method: "DELETE" });
}

// --- Pure presentation helpers ---------------------------------------------------------------

export interface QboStatusDisplay {
  kind: StatusKind;
  /** Passed to StatusChip's `glyph` — colour never stands alone. */
  glyph: string;
  label: string;
}

/**
 * Status → chip. Active teal ✓ "Connected", NeedsReconnect gold ! "Reconnect needed",
 * Disconnected / NotConnected gray "Not connected" — the console treats a disconnected company
 * as not connected; the card says which company it last was.
 */
export const QBO_STATUS_DISPLAY: Record<QboConnectionStatus, QboStatusDisplay> = {
  Active: { kind: "ontime", glyph: "✓", label: "Connected" },
  NeedsReconnect: { kind: "soon", glyph: "!", label: "Reconnect needed" },
  Disconnected: { kind: "off", glyph: "—", label: "Not connected" },
  NotConnected: { kind: "off", glyph: "—", label: "Not connected" },
};

/** A live connection holds tokens — Active or NeedsReconnect (QboConnection.IsLive). */
export function isLiveQboConnection(status: QboConnectionStatus): boolean {
  return status === "Active" || status === "NeedsReconnect";
}

/** How far ahead of the refresh token's expiry the card starts warning. */
export const RECONNECT_WARNING_DAYS = 14;
const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * True when the refresh token expires less than 14 days from `now` (an already-passed expiry
 * included). Null expiry — NotConnected or Disconnected — never warns. Strictly less than: at
 * exactly 14 days there is no warning yet.
 */
export function reconnectWarningDue(refreshTokenExpiresAtUtc: string | null, now: Date): boolean {
  if (!refreshTokenExpiresAtUtc) return false;
  const expires = Date.parse(refreshTokenExpiresAtUtc);
  if (Number.isNaN(expires)) return false;
  return expires - now.getTime() < RECONNECT_WARNING_DAYS * DAY_MS;
}

/**
 * The callback page's reading of Intuit's redirect. Intuit sends `?error=access_denied` (and
 * friends) when the person declines, in which case nothing is posted; otherwise
 * `code`, `state` and `realmId`, posted as they came (missing → null, so the server's own
 * CallbackIncomplete message names the problem).
 */
export type QboCallbackParams =
  | { kind: "error"; error: string; description: string | null }
  | { kind: "complete"; input: QboCallbackInput };

export function parseQboCallback(search: string): QboCallbackParams {
  const q = new URLSearchParams(search);
  const error = q.get("error");
  if (error) {
    return { kind: "error", error, description: q.get("error_description") };
  }
  return {
    kind: "complete",
    input: { code: q.get("code"), state: q.get("state"), realmId: q.get("realmId") },
  };
}
