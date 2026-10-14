import { describe, expect, it } from "vitest";
import { BUDGET_ROLES, COMPLIANCE_ROLES, DISPATCH_ROLES, DRIVERS_APP_ROLES, hasRole, normalizeRole } from "./roles";
import { INVITE_ROLES } from "./auth";
import { APPS, canUseApp } from "./apps";

// The role lists mirror Backend/src/Shared/Kernel/Roles.cs. These pin the two the HOS
// compliance work added — ComplianceAccess and DriverRosterAccess — and that the launcher
// opens exactly the Drivers & Compliance app to a Compliance Officer, nothing else.

describe("COMPLIANCE_ROLES (Roles.ComplianceAccess)", () => {
  it("is Owner and ComplianceOfficer only — dispatch is deliberately absent", () => {
    expect([...COMPLIANCE_ROLES]).toEqual(["Owner", "ComplianceOfficer"]);
    expect(hasRole(COMPLIANCE_ROLES, "Dispatcher")).toBe(false);
    expect(hasRole(COMPLIANCE_ROLES, "Supervisor")).toBe(false);
    expect(hasRole(COMPLIANCE_ROLES, "ComplianceOfficer")).toBe(true);
    expect(hasRole(COMPLIANCE_ROLES, normalizeRole("Admin"))).toBe(true);
  });

  it("matches case-sensitively, like the backend's RequireRole", () => {
    expect(hasRole(COMPLIANCE_ROLES, "complianceofficer")).toBe(false);
    expect(hasRole(COMPLIANCE_ROLES, null)).toBe(false);
  });
});

describe("DRIVERS_APP_ROLES (Roles.DriverRosterAccess)", () => {
  it("is dispatch plus ComplianceOfficer, and DISPATCH_ROLES itself is unchanged", () => {
    expect([...DRIVERS_APP_ROLES]).toEqual(["Owner", "Dispatcher", "Supervisor", "ComplianceOfficer"]);
    expect([...DISPATCH_ROLES]).toEqual(["Owner", "Dispatcher", "Supervisor"]);
    expect([...BUDGET_ROLES]).toEqual(["Owner", "Accountant"]);
  });
});

describe("the invite list", () => {
  it("can name a ComplianceOfficer (Roles.Internal)", () => {
    expect(INVITE_ROLES).toContain("ComplianceOfficer");
  });
});

describe("the launcher for a ComplianceOfficer", () => {
  const byId = new Map(APPS.map((a) => [a.id, a]));

  it("opens Drivers & Compliance and nothing else", () => {
    const allowed = APPS.filter((a) => canUseApp(a, "ComplianceOfficer")).map((a) => a.id);
    expect(allowed).toEqual(["drivers"]);
  });

  it("still opens every dispatch app to a Dispatcher", () => {
    expect(canUseApp(byId.get("drivers")!, "Dispatcher")).toBe(true);
    expect(canUseApp(byId.get("today")!, "Dispatcher")).toBe(true);
    expect(canUseApp(byId.get("drivers")!, "Accountant")).toBe(false);
  });
});
