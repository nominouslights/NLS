import { describe, expect, it } from "vitest";
import { decodeAccessToken } from "./claims";
import { BUDGET_ROLES, hasBudgetAccess } from "./roles";

// Every link in the access chain is tested on its own — claims.test.ts decodes tokens,
// roles.test.ts applies the rule, RoleGate.test.tsx renders the outcome with lib/auth mocked
// out. The CHAIN was tested nowhere: nothing ran a real token through the real decoder into the
// real rule. A rename on either side of that seam (claims.ts stops populating `role`, roles.ts
// starts comparing something else) leaves all three files green and locks every user out.
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
        email: "user@northernlink.ca",
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
function mayUseConsole(token: string | null): boolean {
  return hasBudgetAccess(decodeAccessToken(token)?.role ?? null);
}

describe("token → claims → role → gate", () => {
  it("rejects a real Dispatcher token end to end", () => {
    // US-6.0.1's acceptance criterion, exercised through the whole chain rather than against
    // the rule in isolation. Its server-side counterpart is
    // Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs.
    expect(mayUseConsole(accessTokenFor("Dispatcher"))).toBe(false);
  });

  it.each([...BUDGET_ROLES])("admits a real %s token end to end", (role) => {
    expect(mayUseConsole(accessTokenFor(role))).toBe(true);
  });

  it.each(["Supervisor", "Driver", "BoardMember", "Admin"])(
    "rejects a real %s token end to end",
    (role) => {
      expect(mayUseConsole(accessTokenFor(role))).toBe(false);
    },
  );

  it("rejects a token whose role claim is not a string", () => {
    // The gap between the layers: claims.ts converts an unusable role to "", not to null or
    // undefined, so this is the value hasBudgetAccess actually receives in the wild. Testing
    // the decoder's "" and the rule's null separately never puts the two together.
    expect(decodeAccessToken(accessTokenFor(42))?.role).toBe("");
    expect(mayUseConsole(accessTokenFor(42))).toBe(false);
  });

  it("rejects the empty role a decodable-but-roleless token produces", () => {
    expect(hasBudgetAccess("")).toBe(false);
    expect(mayUseConsole(accessTokenFor(undefined))).toBe(false);
  });

  it("rejects a malformed token rather than failing open", () => {
    expect(mayUseConsole(null)).toBe(false);
    expect(mayUseConsole("")).toBe(false);
    expect(mayUseConsole("not.a.jwt")).toBe(false);
  });

  it("stays case-sensitive across the seam", () => {
    // A lower-cased role survives the decode intact and must still be refused: the backend's
    // RequireRole compares ordinally, so admitting it would show a console the API then 403s.
    expect(decodeAccessToken(accessTokenFor("owner"))?.role).toBe("owner");
    expect(mayUseConsole(accessTokenFor("owner"))).toBe(false);
  });
});
