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

      expect(screen.queryByText("budget console contents")).toBeNull();
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
  });
});
