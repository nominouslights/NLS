import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import RoleGate from "./RoleGate";

// The frontend half of US-6.0.1's rejection criterion. lib/roles.test.ts proves the rule;
// this proves the gate actually applies it — that a Dispatcher session renders the denial
// screen and never the children behind it.

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
  it("does not render the console for a Dispatcher account", () => {
    getRole.mockReturnValue("Dispatcher");

    render(
      <RoleGate>
        <div>budget console contents</div>
      </RoleGate>,
    );

    expect(screen.queryByText("budget console contents")).toBeNull();
    expect(screen.getByText("This console is restricted.")).toBeTruthy();
    expect(screen.getByText(/signed in as Dispatcher/)).toBeTruthy();
  });

  it("offers a way out, so a wrong-role session is not a trap", () => {
    // Without this the user is stuck: the session is valid, so every reload restores it and
    // lands back on this screen.
    getRole.mockReturnValue("Dispatcher");

    render(
      <RoleGate>
        <div>budget console contents</div>
      </RoleGate>,
    );

    expect(screen.getByText("SIGN OUT")).toBeTruthy();
  });

  it.each(["Supervisor", "Driver", "BoardMember", "Admin"])(
    "blocks a %s account too",
    (role) => {
      getRole.mockReturnValue(role);

      render(
        <RoleGate>
          <div>budget console contents</div>
        </RoleGate>,
      );

      // Both halves are needed. Absence of the children alone would also pass for a gate that
      // rendered null for everyone — which is a broken console, not a working denial.
      expect(screen.queryByText("budget console contents")).toBeNull();
      expect(screen.getByText("This console is restricted.")).toBeTruthy();
      expect(screen.getByText(new RegExp(`signed in as ${role}`))).toBeTruthy();
      expect(screen.getByText("SIGN OUT")).toBeTruthy();
    },
  );

  it.each(["Owner", "Accountant"])("renders the console for %s", (role) => {
    getRole.mockReturnValue(role);

    render(
      <RoleGate>
        <div>budget console contents</div>
      </RoleGate>,
    );

    expect(screen.getByText("budget console contents")).toBeTruthy();
    expect(screen.queryByText("This console is restricted.")).toBeNull();
  });

  it("blocks rather than fails open when the role is unreadable", () => {
    getRole.mockReturnValue(null);

    render(
      <RoleGate>
        <div>budget console contents</div>
      </RoleGate>,
    );

    expect(screen.queryByText("budget console contents")).toBeNull();
    // ...and says so, with a way out. Without these the test would pass for a gate that showed
    // nobody anything, and a user with an unreadable token would face a blank page.
    expect(screen.getByText("This console is restricted.")).toBeTruthy();
    expect(screen.getByText(/an account without budget access/)).toBeTruthy();
    expect(screen.getByText("SIGN OUT")).toBeTruthy();
  });

  it("blocks the empty role a decodable token with no role claim produces", () => {
    // null is what getRole returns with no session at all; "" is what it returns for a token
    // that decoded fine but carried no usable role claim (claims.ts converts a missing or
    // wrongly-typed role to ""). The real path emits both, so both need covering — and "" is
    // the one that reaches AccessDeniedScreen's `role.trim()` fallback.
    getRole.mockReturnValue("");

    render(
      <RoleGate>
        <div>budget console contents</div>
      </RoleGate>,
    );

    expect(screen.queryByText("budget console contents")).toBeNull();
    expect(screen.getByText("This console is restricted.")).toBeTruthy();
    expect(screen.getByText(/an account without budget access/)).toBeTruthy();
    expect(screen.getByText("SIGN OUT")).toBeTruthy();
  });
});
