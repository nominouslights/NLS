import { describe, expect, it } from "vitest";
import { decodeAccessToken } from "./claims";

// Tokens are built here rather than fetched: the decoder does not verify signatures (see
// claims.ts), so an unsigned three-segment token is exactly as valid an input as a real one,
// and building them keeps the test free of a backend.

function base64Url(value: string): string {
  return Buffer.from(value, "utf8")
    .toString("base64")
    .replace(/\+/g, "-")
    .replace(/\//g, "_")
    .replace(/=+$/, "");
}

function tokenFor(payload: Record<string, unknown>): string {
  return [
    base64Url(JSON.stringify({ alg: "HS256", typ: "JWT" })),
    base64Url(JSON.stringify(payload)),
    "not-a-real-signature",
  ].join(".");
}

const FULL_PAYLOAD = {
  sub: "8f1c0f8e-1b3a-4a9d-9d6e-1c2b3a4d5e6f",
  email: "owner@northernlink.ca",
  role: "Owner",
  tenant_id: "00000000-0000-0000-0000-000000000001",
  tenant_type: "Internal",
  exp: 1_800_000_000,
};

describe("decodeAccessToken", () => {
  it("reads every claim JwtAccessTokenIssuer stamps", () => {
    expect(decodeAccessToken(tokenFor(FULL_PAYLOAD))).toEqual({
      sub: FULL_PAYLOAD.sub,
      email: FULL_PAYLOAD.email,
      role: "Owner",
      tenantId: FULL_PAYLOAD.tenant_id,
      tenantType: "Internal",
      exp: FULL_PAYLOAD.exp,
    });
  });

  it.each(["Owner", "Accountant", "Dispatcher", "Supervisor", "Driver"])(
    "extracts the %s role",
    (role) => {
      expect(decodeAccessToken(tokenFor({ ...FULL_PAYLOAD, role }))?.role).toBe(role);
    },
  );

  it("round-trips a non-ASCII email", () => {
    // atob yields a binary string, not UTF-8. A naive implementation mangles this; claims.ts
    // routes the bytes through TextDecoder specifically to get it right.
    const email = "réal.provençal@northernlink.ca";
    expect(decodeAccessToken(tokenFor({ ...FULL_PAYLOAD, email }))?.email).toBe(email);
  });

  it.each([
    ["null", null],
    ["an empty string", ""],
    ["a two-segment token", "header.payload"],
    ["a four-segment token", "a.b.c.d"],
    ["a token with an undecodable payload", "header.@@@not-base64@@@.sig"],
    ["a token whose payload is not JSON", `header.${base64Url("plain text")}.sig`],
    ["a token whose payload is a JSON array", `header.${base64Url("[1,2,3]")}.sig`],
  ])("returns null for %s", (_label, token) => {
    expect(decodeAccessToken(token as string | null)).toBeNull();
  });

  it("returns empty strings, never undefined, for absent claims", () => {
    // A partial token must not produce an object whose role is undefined — hasBudgetAccess
    // would then be deciding on a value the type system said could not exist.
    const claims = decodeAccessToken(tokenFor({ sub: "abc" }));

    expect(claims).not.toBeNull();
    expect(claims?.role).toBe("");
    expect(claims?.email).toBe("");
    expect(claims?.exp).toBe(0);
  });

  it("ignores claims of the wrong type rather than passing them through", () => {
    const claims = decodeAccessToken(tokenFor({ ...FULL_PAYLOAD, role: 42, exp: "soon" }));

    expect(claims?.role).toBe("");
    expect(claims?.exp).toBe(0);
  });
});
