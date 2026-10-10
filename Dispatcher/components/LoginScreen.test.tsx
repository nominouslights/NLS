import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";

// The Privacy Policy / EULA page is a public URL (Intuit + app-store submissions), so the
// sign-in screen — the only thing a signed-out visitor sees — must link to both anchors,
// in a new tab, with the build-time basePath applied.

vi.mock("@/lib/auth", () => ({ login: vi.fn(), redeemAdminInvite: vi.fn() }));

afterEach(() => {
  cleanup();
  vi.unstubAllEnvs();
  vi.resetModules();
});

async function renderLogin() {
  const { default: LoginScreen } = await import("./LoginScreen");
  render(<LoginScreen />);
}

describe("LoginScreen legal links", () => {
  it("links the Privacy Policy and Licence Agreement, opening in a new tab", async () => {
    vi.stubEnv("NEXT_PUBLIC_BASE_PATH", "");
    await renderLogin();

    const privacy = screen.getByRole("link", { name: "Privacy Policy" });
    const eula = screen.getByRole("link", { name: "Licence Agreement" });

    expect(privacy.getAttribute("href")).toBe("/legal/policies.html#privacy");
    expect(eula.getAttribute("href")).toBe("/legal/policies.html#eula");
    for (const a of [privacy, eula]) {
      expect(a.getAttribute("target")).toBe("_blank");
      expect(a.getAttribute("rel")).toContain("noopener");
    }
  });

  it("prefixes the hrefs with the configured basePath", async () => {
    vi.stubEnv("NEXT_PUBLIC_BASE_PATH", "/dispatch");
    await renderLogin();

    expect(screen.getByRole("link", { name: "Privacy Policy" }).getAttribute("href")).toBe(
      "/dispatch/legal/policies.html#privacy",
    );
    expect(screen.getByRole("link", { name: "Licence Agreement" }).getAttribute("href")).toBe(
      "/dispatch/legal/policies.html#eula",
    );
  });
});
