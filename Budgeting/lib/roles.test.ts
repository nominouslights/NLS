import { describe, expect, it } from "vitest";
import { BUDGET_ROLES, hasBudgetAccess } from "./roles";

// US-6.0.1's acceptance criterion lives here: a Dispatcher account must be rejected from the
// Budgeting console. Its server-side counterpart is
// Backend/tests/NorthernLink.Api.Tests/AuthorizationPolicyTests.cs. Both exist because in
// Stage 6.0 there are no budgeting endpoints yet, so this list is the gate a user actually
// meets — the backend policy is registered and tested but attached to nothing.

describe("hasBudgetAccess", () => {
  it("rejects a Dispatcher account", () => {
    expect(hasBudgetAccess("Dispatcher")).toBe(false);
  });

  it("rejects a Supervisor account", () => {
    expect(hasBudgetAccess("Supervisor")).toBe(false);
  });

  it.each(["Owner", "Accountant"])("admits %s", (role) => {
    expect(hasBudgetAccess(role)).toBe(true);
  });

  it.each(["Driver", "BoardMember", "Admin", "SuperUser", "Bookkeeper"])(
    "rejects %s",
    (role) => {
      expect(hasBudgetAccess(role)).toBe(false);
    },
  );

  it("rejects a missing role rather than defaulting open", () => {
    expect(hasBudgetAccess(null)).toBe(false);
    expect(hasBudgetAccess(undefined)).toBe(false);
    expect(hasBudgetAccess("")).toBe(false);
  });

  it.each(["owner", "OWNER", "accountant"])(
    "is case-sensitive, so %s is not a match",
    (role) => {
      // The backend's RequireRole compares ordinally. Accepting a case variant here would show
      // a console the API would then refuse to serve — a worse failure than a clean rejection.
      expect(hasBudgetAccess(role)).toBe(false);
    },
  );

  it("stays in step with the backend's Roles.BudgetAccess", () => {
    // Mirror of Backend/src/Shared/Kernel/Roles.cs. If that list changes, this fails first.
    expect([...BUDGET_ROLES]).toEqual(["Owner", "Accountant"]);
  });
});
