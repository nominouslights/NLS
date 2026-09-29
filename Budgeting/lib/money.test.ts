import { describe, expect, it } from "vitest";
import { formatCadPrecise, formatDeltaCad, formatDeltaPct } from "./money";

// The platform rule these two enforce: a signed figure writes its sign out as text, so the
// direction survives grayscale and any colour-vision deficiency. No server rule to mirror — the
// sums come from the server — but the Net tile and the Variance screen both rest on this.

describe("formatDeltaCad", () => {
  it.each<[number, string]>([
    [750, "+$750"],
    [-750, "−$750"],
    [1_023_500, "+$1,023,500"],
    [-1_023_500, "−$1,023,500"],
  ])("formats %d as %s — the sign is always written", (delta, expected) => {
    expect(formatDeltaCad(delta)).toBe(expected);
  });

  it("prints zero unsigned", () => {
    expect(formatDeltaCad(0)).toBe("$0");
  });

  it("uses a true minus sign, not a hyphen", () => {
    expect(formatDeltaCad(-1)).toMatch(/^−/);
    expect(formatDeltaCad(-1)).not.toMatch(/^-/);
  });
});

describe("formatDeltaPct", () => {
  it.each<[number, string]>([
    [5, "+5.0%"],
    [-5, "−5.0%"],
    [12.345, "+12.3%"],
    [0, "0.0%"],
  ])("formats %d as %s", (pct, expected) => {
    expect(formatDeltaPct(pct)).toBe(expected);
  });

  it("prints a dash when there is no baseline (planned = 0)", () => {
    expect(formatDeltaPct(null)).toBe("—");
  });
});

// A built-up budget item (quantity × unit cost) lands on cents, which the copied whole-dollar
// formatCad would silently round away. No server rule — display only.
describe("formatCadPrecise", () => {
  it.each<[number, string]>([
    [5400, "$5,400"],
    [0, "$0"],
    [3.03, "$3.03"],
    [0.5, "$0.50"],
    [1_234_567.89, "$1,234,567.89"],
  ])("formats %d as %s — cents only when there are cents", (value, expected) => {
    expect(formatCadPrecise(value)).toBe(expected);
  });
});
