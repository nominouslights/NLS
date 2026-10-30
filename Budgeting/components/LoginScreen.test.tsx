import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import LoginScreen from "@/components/LoginScreen";
import { legalHref } from "@/lib/legal";

// The Privacy Policy and EULA must be reachable WITHOUT signing in: their #privacy / #eula URLs
// are registered on the Intuit (QuickBooks) app profile. The sign-in screen is the one page every
// visitor sees, so it is where the links are pinned.

afterEach(cleanup);

describe("LoginScreen legal links", () => {
  it.each([
    ["Privacy Policy", "/legal/policies.html#privacy"],
    ["Licence Agreement", "/legal/policies.html#eula"],
  ])("renders %s pointing at %s, in a new tab", (label, href) => {
    render(<LoginScreen />);
    const link = screen.getByRole("link", { name: label });
    expect(link.getAttribute("href")).toBe(href);
    expect(link.getAttribute("target")).toBe("_blank");
    expect(link.getAttribute("rel")).toContain("noopener");
  });
});

describe("legalHref", () => {
  it("is root-relative when the console is unmounted (hostname-per-app)", () => {
    expect(legalHref("privacy", "")).toBe("/legal/policies.html#privacy");
    expect(legalHref("eula", "")).toBe("/legal/policies.html#eula");
  });

  it("carries the build-time basePath when mounted under a prefix", () => {
    expect(legalHref("privacy", "/budget")).toBe("/budget/legal/policies.html#privacy");
    expect(legalHref("eula", "/budget/")).toBe("/budget/legal/policies.html#eula");
  });
});
