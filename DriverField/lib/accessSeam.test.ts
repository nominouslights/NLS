import { describe, expect, it } from "vitest";
import { decodeAccessToken } from "./claims";
import { DRIVER_ROLES, hasDriverAccess } from "./roles";

// Every link in the access chain is tested on its own — claims.test.ts decodes tokens,
// roles.test.ts applies the rule, RoleGate.test.tsx renders the outcome with lib/auth mocked
// out. The CHAIN is tested nowhere else: nothing else runs a real token through the real
// decoder into the real rule. A rename on either side of that seam (claims.ts stops populating
// `role`, roles.ts starts comparing something else) leaves all three files green and locks
// every driver out of the app — in a vehicle, at 05:30, with no way to log a pre-trip.
//
// So: no mocks here, no DOM. A token in, an access decision out — the same two functions
// lib/auth.getRole and RoleGate call in production.

function base64Url(value: string): string {
  return Buffer.from(value, "utf8")
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

/**
 * A token shaped exactly as JwtAccessTokenIssuer stamps it — claim names included. Unsigned,
 * because the decoder does not verify signatures by design (see claims.ts); the signature
 * segment is never read.
 */
function accessTokenFor(role: unknown): string {
  return [
    base64Url(JSON.stringify({ alg: "HS256", typ: "JWT" })),
    base64Url(
      JSON.stringify({
        sub: "8f1c0f8e-1b3a-4a9d-9d6e-1c2b3a4d5e6f",
        email: "driver@northernlink.ca",
        role,
        tenant_id: "00000000-0000-0000-0000-000000000001",
        tenant_type: "Internal",
        exp: 1_800_000_000,
      }),
    ),
    "not-a-real-signature",
  ].join(".");
}

/** The production path, verbatim: lib/auth.getRole() is `decodeAccessToken(t)?.role ?? null`. */
function mayUseApp(token: string | null): boolean {
  return hasDriverAccess(decodeAccessToken(token)?.role ?? null);
}

describe("token → claims → role → gate", () => {
  it("rejects a real Accountant token end to end", () => {
    // This app's acceptance criterion, exercised through the whole chain rather than against
    // the rule in isolation. Its server-side counterpart is
    // Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs.
    expect(mayUseApp(accessTokenFor("Accountant"))).toBe(false);
  });

  it.each([...DRIVER_ROLES])("admits a real %s token end to end", (role) => {
    expect(mayUseApp(accessTokenFor(role))).toBe(true);
  });

  it.each(["BoardMember", "Admin"])("rejects a real %s token end to end", (role) => {
    expect(mayUseApp(accessTokenFor(role))).toBe(false);
  });

  it("rejects a token whose role claim is not a string", () => {
    // The gap between the layers: claims.ts converts an unusable role to "", not to null or
    // undefined, so this is the value hasDriverAccess actually receives in the wild. Testing
    // the decoder's "" and the rule's null separately never puts the two together.
    expect(decodeAccessToken(accessTokenFor(42))?.role).toBe("");
    expect(mayUseApp(accessTokenFor(42))).toBe(false);
  });

  it("rejects the empty role a decodable-but-roleless token produces", () => {
    expect(hasDriverAccess("")).toBe(false);
    expect(mayUseApp(accessTokenFor(undefined))).toBe(false);
  });

  it("rejects a malformed token rather than failing open", () => {
    expect(mayUseApp(null)).toBe(false);
    expect(mayUseApp("")).toBe(false);
    expect(mayUseApp("not.a.jwt")).toBe(false);
  });

  it("stays case-sensitive across the seam", () => {
    // A lower-cased role survives the decode intact and must still be refused: the backend's
    // RequireRole compares ordinally, so admitting it would show an app the API then 403s.
    expect(decodeAccessToken(accessTokenFor("driver"))?.role).toBe("driver");
    expect(mayUseApp(accessTokenFor("driver"))).toBe(false);
  });
});
