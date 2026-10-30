import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { POLICY_PATH, policyHref } from "./legal";

// The href must carry basePath: a plain <a> to a public/ file gets no automatic prefix, and the
// image is built with BASE_PATH=/driver. A missing prefix 404s only on the deployed build.

describe("policyHref", () => {
  it("is unprefixed on a local build", () => {
    expect(policyHref("privacy", "")).toBe("/legal/policies.html#privacy");
    expect(policyHref("eula", "")).toBe("/legal/policies.html#eula");
  });

  it("carries the basePath on the image build", () => {
    expect(policyHref("privacy", "/driver")).toBe("/driver/legal/policies.html#privacy");
    expect(policyHref("eula", "/driver")).toBe("/driver/legal/policies.html#eula");
  });

  it("never doubles a slash when basePath ends in one", () => {
    expect(policyHref("eula", "/driver/")).toBe("/driver/legal/policies.html#eula");
  });

  it("targets a file that exists and anchors that exist in it", () => {
    // Read-only check of the shared copy; it is never edited here.
    const html = readFileSync(join(import.meta.dirname, "..", "public", POLICY_PATH), "utf8");
    expect(html).toContain('id="privacy"');
    expect(html).toContain('id="eula"');
  });

  it("is precached by the service worker", () => {
    const sw = readFileSync(join(import.meta.dirname, "..", "public", "sw.js"), "utf8");
    expect(sw).toContain(`".${POLICY_PATH}"`);
  });
});
