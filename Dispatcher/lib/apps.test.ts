import { describe, expect, it } from "vitest";
import { APPS, INTERNAL_APPS, appForScreen, canUseApp, canUseScreen, railGroupsFor, screensFor } from "./apps";
import { NAV_GROUPS } from "./nav";

// The Compliance screen narrows the Drivers & Compliance app to Roles.ComplianceAccess: the
// rail, the launcher tile's screen list and Console.navigate all go through the same gate.

const drivers = INTERNAL_APPS.find((a) => a.id === "drivers")!;
const railIds = (role: string | null) => railGroupsFor(drivers, role).flatMap((g) => g.items.map((i) => i.id));

describe("the Compliance screen", () => {
  it("lives in Drivers & Compliance", () => {
    expect(appForScreen("compliance").id).toBe("drivers");
  });

  it("is on a ComplianceOfficer's rail, and an Owner's (legacy Admin included)", () => {
    expect(railIds("ComplianceOfficer")).toEqual(["drivers", "incidents", "compliance"]);
    expect(railIds("Owner")).toContain("compliance");
    expect(railIds("Admin")).toContain("compliance");
    expect(canUseScreen("compliance", "ComplianceOfficer")).toBe(true);
    expect(canUseScreen("compliance", "Admin")).toBe(true);
  });

  it("is not on a Dispatcher's or Supervisor's rail, and navigate refuses it", () => {
    expect(railIds("Dispatcher")).toEqual(["drivers", "incidents"]);
    expect(railIds("Supervisor")).not.toContain("compliance");
    expect(canUseScreen("compliance", "Dispatcher")).toBe(false);
    // The rest of the app stays open to dispatch.
    expect(canUseScreen("drivers", "Dispatcher")).toBe(true);
  });

  it("an Accountant gets neither the app nor the screen", () => {
    expect(canUseApp(drivers, "Accountant")).toBe(false);
    expect(canUseScreen("drivers", "Accountant")).toBe(false);
    expect(canUseScreen("compliance", "Accountant")).toBe(false);
    expect(screensFor(drivers, "Accountant").map((s) => s.id)).toEqual(["drivers", "incidents"]);
  });

  it("matches the role case-sensitively", () => {
    expect(canUseScreen("compliance", "complianceofficer")).toBe(false);
    expect(canUseScreen("compliance", null)).toBe(false);
  });

  it("stays in NavRail's default NAV_GROUPS (the unfiltered manifest)", () => {
    expect(NAV_GROUPS.flatMap((g) => g.items.map((i) => i.id))).toContain("compliance");
  });

  it("is the only role-gated screen, so every other screen follows its app's gate", () => {
    const gated = APPS.flatMap((a) => (a.kind === "internal" ? a.screens.filter((s) => s.roles).map((s) => s.id) : []));
    expect(gated).toEqual(["compliance"]);
  });
});
