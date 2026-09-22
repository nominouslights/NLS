import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import RoleGate from "./RoleGate";

// lib/roles.test.ts proves the rule; this proves the gate actually applies it — that a wrong
// role renders the denial screen and never the app behind it.
//
// Every denial case asserts that the denial SCREEN renders, not merely that the children are
// absent. Absence alone would also pass for a gate that rendered null for everyone, which on a
// mounted tablet with no address bar is a bricked device, not a denial.

const { getRole } = vi.hoisted(() => ({ getRole: vi.fn<() => string | null>() }));

vi.mock("@/lib/auth", () => ({
  getRole,
  // AccessDeniedScreen imports logout for its sign-out button.
  logout: vi.fn(),
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("RoleGate", () => {
  it("does not render the app for an Accountant account", () => {
    getRole.mockReturnValue("Accountant");

    render(
      <RoleGate>
        <div>driver app contents</div>
      </RoleGate>,
    );

    expect(screen.queryByText("driver app contents")).toBeNull();
    expect(screen.getByText("This app is restricted.")).toBeTruthy();
    expect(screen.getByText(/signed in as Accountant/)).toBeTruthy();
  });

  it("offers a way out, so a wrong-role session is not a trap", () => {
    // Without this the user is stuck: the session is valid, so every reload restores it and
    // lands back on this screen — and a company tablet in kiosk mode has no address bar to
    // escape through.
    getRole.mockReturnValue("Accountant");

    render(
      <RoleGate>
        <div>driver app contents</div>
      </RoleGate>,
    );

    expect(screen.getByText("Sign out")).toBeTruthy();
  });

  it.each(["BoardMember", "Admin"])("blocks a %s account too", (role) => {
    getRole.mockReturnValue(role);

    render(
      <RoleGate>
        <div>driver app contents</div>
      </RoleGate>,
    );

    expect(screen.queryByText("driver app contents")).toBeNull();
    expect(screen.getByText("This app is restricted.")).toBeTruthy();
    expect(screen.getByText(new RegExp(`signed in as ${role}`))).toBeTruthy();
    expect(screen.getByText("Sign out")).toBeTruthy();
  });

  it.each(["Owner", "Dispatcher", "Supervisor", "Driver"])("renders the app for %s", (role) => {
    getRole.mockReturnValue(role);

    render(
      <RoleGate>
        <div>driver app contents</div>
      </RoleGate>,
    );

    expect(screen.getByText("driver app contents")).toBeTruthy();
    expect(screen.queryByText("This app is restricted.")).toBeNull();
  });

  it("blocks rather than fails open when the role is unreadable", () => {
    getRole.mockReturnValue(null);

    render(
      <RoleGate>
        <div>driver app contents</div>
      </RoleGate>,
    );

    expect(screen.queryByText("driver app contents")).toBeNull();
    expect(screen.getByText("This app is restricted.")).toBeTruthy();
    expect(screen.getByText(/an account without driver access/)).toBeTruthy();
    expect(screen.getByText("Sign out")).toBeTruthy();
  });

  it("blocks the empty role a decodable token with no role claim produces", () => {
    // null is what getRole returns with no session at all; "" is what it returns for a token
    // that decoded fine but carried no usable role claim (claims.ts converts a missing or
    // wrongly-typed role to ""). The real path emits both, so both need covering — and "" is
    // the one that reaches AccessDeniedScreen's `role.trim()` fallback.
    getRole.mockReturnValue("");

    render(
      <RoleGate>
        <div>driver app contents</div>
      </RoleGate>,
    );

    expect(screen.queryByText("driver app contents")).toBeNull();
    expect(screen.getByText("This app is restricted.")).toBeTruthy();
    expect(screen.getByText(/an account without driver access/)).toBeTruthy();
    expect(screen.getByText("Sign out")).toBeTruthy();
  });
});
