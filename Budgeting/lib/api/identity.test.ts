import { describe, expect, it } from "vitest";
import {
  PROFILE_FIELD_MAX_LENGTH,
  normalizeProfileField,
  profileDisplayName,
  type MyProfile,
} from "@/lib/api/identity";

// Pure functions only, matching lib/api/budgeting.test.ts: everything here re-derives a server
// rule client-side, and the C# it mirrors is named in each comment. That naming is the only
// thing keeping the two copies honest.

function profile(over: Partial<MyProfile> = {}): MyProfile {
  return {
    userId: "8f1d2c3b-4a5e-4f60-9a71-2b3c4d5e6f70",
    email: "planner@northernlink.ca",
    role: "Accountant",
    fullName: null,
    jobTitle: null,
    ...over,
  };
}

describe("normalizeProfileField", () => {
  // Mirrors User.Normalize in Backend/src/Identity/Domain/Users/User.cs.
  it("trims surrounding whitespace", () => {
    expect(normalizeProfileField("  Léa Fontaine  ")).toBe("Léa Fontaine");
  });

  it("treats blank as absent, so a cleared field goes on the wire as null", () => {
    expect(normalizeProfileField("")).toBeNull();
    expect(normalizeProfileField("   ")).toBeNull();
    expect(normalizeProfileField("\t\n")).toBeNull();
  });

  it("leaves interior spacing alone", () => {
    expect(normalizeProfileField("Léa  Fontaine")).toBe("Léa  Fontaine");
  });
});

describe("profileDisplayName", () => {
  it("prefers the name once the user has set one", () => {
    expect(profileDisplayName(profile({ fullName: "Léa Fontaine" }))).toBe("Léa Fontaine");
  });

  it("falls back to the email when there is no name", () => {
    // Every account starts here — the name is null until its owner visits Settings -> Profile.
    expect(profileDisplayName(profile())).toBe("planner@northernlink.ca");
  });

  it("falls back when the name is only whitespace", () => {
    // The server cannot store this, but a client must not render a blank identity if it ever
    // sees one.
    expect(profileDisplayName(profile({ fullName: "   " }))).toBe("planner@northernlink.ca");
  });
});

describe("PROFILE_FIELD_MAX_LENGTH", () => {
  it("matches User.ProfileFieldMaxLength", () => {
    // 128 in Backend/src/Identity/Domain/Users/User.cs, and in both the identity.users and
    // budgeting.user_lookup column definitions. If this diverges, the input stops capping where
    // the server rejects and the user meets a 400 instead of a full field.
    expect(PROFILE_FIELD_MAX_LENGTH).toBe(128);
  });
});
