// ---------------------------------------------------------------------------
// Access-token claim reader.
//
// THIS DOES NOT VERIFY THE SIGNATURE, DELIBERATELY. It reads the JWT payload so the UI can
// render the right thing immediately — the signed-in user's role, initials, tenant — without a
// round trip. Anyone can hand-craft a token that satisfies this decoder.
//
// That is fine because this is a UX gate, not a security boundary: the API validates the
// signature on every request, and GET /api/identity/auth/me is the server-confirmed answer when
// one is actually needed. Nothing here is permitted to be the only thing standing between a
// user and data.
//
// Claim names are the literals stamped by
// Backend/src/Identity/Infrastructure/Auth/JwtAccessTokenIssuer.cs — sub, email, tenant_id,
// tenant_type, role. That file's constants and this interface have to move together.
// ---------------------------------------------------------------------------

export interface AccessClaims {
  /** User id. */
  sub: string;
  email: string;
  /** One of Roles.Internal in Backend/src/Shared/Kernel/Roles.cs. */
  role: string;
  tenantId: string;
  tenantType: string;
  /** Expiry, epoch seconds. */
  exp: number;
}

/** base64url → the UTF-8 string it encodes, or null if it is not decodable. */
function decodeBase64Url(segment: string): string | null {
  try {
    const base64 = segment.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), "=");
    // atob yields a binary string, one char per byte — not UTF-8. Feeding it straight to
    // JSON.parse mangles any non-ASCII (an accented name in an email claim, say), so route
    // the bytes through TextDecoder instead.
    const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
    return new TextDecoder().decode(bytes);
  } catch {
    return null;
  }
}

/**
 * Reads the claims out of a JWT's payload segment. Returns null for anything that is not a
 * well-formed three-segment token with a decodable JSON payload — callers treat null as
 * "no usable identity", never as "trusted but empty".
 */
export function decodeAccessToken(token: string | null): AccessClaims | null {
  if (!token) return null;

  const segments = token.split(".");
  if (segments.length !== 3) return null;

  const json = decodeBase64Url(segments[1]);
  if (json === null) return null;

  try {
    const payload: unknown = JSON.parse(json);
    // A JWT payload is a JSON object. Arrays satisfy `typeof x === "object"` too, so they need
    // ruling out explicitly — otherwise a `[1,2,3]` payload yields a claims object full of empty
    // strings, which reads to callers as a valid session belonging to nobody.
    if (typeof payload !== "object" || payload === null || Array.isArray(payload)) return null;

    const claims = payload as Record<string, unknown>;

    return {
      sub: typeof claims.sub === "string" ? claims.sub : "",
      email: typeof claims.email === "string" ? claims.email : "",
      role: typeof claims.role === "string" ? claims.role : "",
      tenantId: typeof claims.tenant_id === "string" ? claims.tenant_id : "",
      tenantType: typeof claims.tenant_type === "string" ? claims.tenant_type : "",
      exp: typeof claims.exp === "number" ? claims.exp : 0,
    };
  } catch {
    return null;
  }
}
