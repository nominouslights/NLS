import { describe, expect, it } from "vitest";
import { DRIVER_ROLES, hasDriverAccess } from "./roles";

// Mirrors Roles.DriverAccess in Backend/src/Shared/Kernel/Roles.cs. Its server-side counterpart
// is the "DriverAccess" policy, tested in
// Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs. Both lists exist because
// this app calls no domain endpoint yet, so this list is the gate a user actually meets.

describe("hasDriverAccess", () => {
  it.each([...DRIVER_ROLES])("admits %s", (role) => {
    expect(hasDriverAccess(role)).toBe(true);
  });

  it("rejects an Accountant account", () => {
    // The acceptance criterion for this app's gate, and the mirror image of Budgeting's
    // "a Dispatcher account is rejected". An Accountant has real business on this platform and
    // a perfectly valid session — which is exactly why the rejection has to be deliberate
    // rather than incidental.
    expect(hasDriverAccess("Accountant")).toBe(false);
  });

  it.each(["BoardMember", "Admin"])("rejects %s", (role) => {
    // "Admin" is Roles.LegacyAdmin — deliberately excluded from every policy, including this
    // one, so a transitional account cannot inherit driver access by accident.
    expect(hasDriverAccess(role)).toBe(false);
  });

  it("is case-sensitive, matching the backend's ordinal RequireRole", () => {
    // Admitting "driver" here would show a driver the app and then have the API 403 every
    // request it makes — the worst of both, and invisible until a real request is attempted.
    expect(hasDriverAccess("driver")).toBe(false);
    expect(hasDriverAccess("DRIVER")).toBe(false);
    expect(hasDriverAccess("Driver")).toBe(true);
  });

  it("rejects absent, empty and whitespace roles rather than failing open", () => {
    expect(hasDriverAccess(null)).toBe(false);
    expect(hasDriverAccess(undefined)).toBe(false);
    expect(hasDriverAccess("")).toBe(false);
    expect(hasDriverAccess(" Driver")).toBe(false);
  });
});
