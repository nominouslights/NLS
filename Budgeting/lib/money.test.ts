import { describe, expect, it } from "vitest";
import { formatAmountInvariant, formatCadPrecise, formatDeltaCad, formatDeltaPct, toHundredths } from "./money";

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

// The app's ONE mirror of BudgetAllocation.Round (Math.Round(value, 2, MidpointRounding.AwayFromZero)).
// lib/api/budgeting.ts (roundCad, computeItemAmount, sumCad) and lib/vendorShares.ts both import it.
describe("toHundredths — BudgetAllocation.Round's integer half", () => {
  it("rounds half away from zero on the decimal spelling, never value * 100", () => {
    expect(toHundredths(1.005)).toBe(101); // 1.005 * 100 is 100.49999999999999
    expect(toHundredths(-1.005)).toBe(-101);
    expect(toHundredths(0.29)).toBe(29);
    expect(toHundredths(0.004)).toBe(0);
    expect(toHundredths(-0.004)).toBe(0);
    expect(toHundredths(1e-7)).toBe(0);
    expect(toHundredths(999_999_999.99)).toBe(99_999_999_999);
  });

  it("treats a non-finite value as 0 — never NaN into a sum, never Infinity into BigInt", () => {
    expect(toHundredths(Number.NaN)).toBe(0);
    expect(toHundredths(Number.POSITIVE_INFINITY)).toBe(0);
    expect(toHundredths(Number.NEGATIVE_INFINITY)).toBe(0);
  });
});

// Mirrors C#'s ToString("#,0.00", CultureInfo.InvariantCulture) — the server's error messages.
describe("formatAmountInvariant", () => {
  it.each<[number, string]>([
    [1234.5, "1,234.50"],
    [2000, "2,000.00"],
    [0.3, "0.30"],
    [1_000_000, "1,000,000.00"],
    [999_999_999.99, "999,999,999.99"],
    [-1234.5, "-1,234.50"],
    [1.005, "1.01"],
  ])("formats %d as %s", (value, expected) => {
    expect(formatAmountInvariant(value)).toBe(expected);
  });
});
