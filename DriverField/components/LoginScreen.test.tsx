import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import LoginScreen from "./LoginScreen";

// The sign-in screen links the owner's Privacy Policy and Licence Agreement — the document that
// covers this app's on-duty location collection — so a driver can read it BEFORE signing in.
// public/legal/policies.html carries the two anchors these hrefs target (#privacy, #eula).

vi.mock("@/lib/auth", () => ({ login: vi.fn() }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("LoginScreen legal links", () => {
  it("renders a Privacy Policy link to the policy page's #privacy anchor", () => {
    render(<LoginScreen />);
    const link = screen.getByRole("link", { name: "Privacy Policy" });
    expect(link.getAttribute("href")).toBe("/legal/policies.html#privacy");
  });

  it("renders a Licence Agreement link to the policy page's #eula anchor", () => {
    render(<LoginScreen />);
    const link = screen.getByRole("link", { name: "Licence Agreement" });
    expect(link.getAttribute("href")).toBe("/legal/policies.html#eula");
  });

  it("opens both in a new context, so the fullscreen app is never navigated away", () => {
    render(<LoginScreen />);
    for (const name of ["Privacy Policy", "Licence Agreement"]) {
      const link = screen.getByRole("link", { name });
      expect(link.getAttribute("target")).toBe("_blank");
      expect(link.getAttribute("rel")).toContain("noopener");
    }
  });
});
