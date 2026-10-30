// Signed-money formatters. They live here, apart from lib/data.ts, so a screen on real data
// (the period dashboard's Net tile) never has to import the mock module to print a signed
// figure. The rule both enforce is the platform's: the sign is written out as text, never
// implied by red-versus-green, so it survives grayscale and any colour-vision deficiency.
//
// Also the app's ONE mirror of the server's cent rounding (toHundredths). It lives here — a leaf
// module with no imports — so lib/api/budgeting.ts and lib/vendorShares.ts can both import it
// without a cycle (budgeting.ts imports vendorShares.ts, so the reverse value import is barred).

/**
 * A number as an integer count of hundredths, rounded half AWAY FROM ZERO — the integer half of
 * BudgetAllocation.Round (`Math.Round(value, 2, MidpointRounding.AwayFromZero)`).
 *
 * Never `Math.round(value * 100)`: `1.005 * 100` is 100.49999999999999 in binary floating point
 * and rounds the wrong way. Instead the decimal point is shifted in the number's own shortest
 * decimal spelling (`String(1.005)` is "1.005", so "1.005e2" parses to exactly 100.5). That
 * spelling is also exactly what JSON.stringify puts on the wire and what the server parses into a
 * C# decimal, so this rounds the same digits the server rounds. Math.round is half-UP, which is
 * half-away-from-zero only for non-negative values — hence the rounding on the absolute value.
 *
 * A non-finite value (NaN, ±Infinity — never a figure the server could hold) is 0, so no caller
 * can propagate NaN into a sum or hand Infinity to BigInt (which throws).
 */
export function toHundredths(value: number): number {
  if (!Number.isFinite(value)) return 0;
  const abs = Math.abs(value);
  const [mantissa, exponent] = String(abs).split("e");
  const shifted = Number(`${mantissa}e${Number(exponent ?? 0) + 2}`);
  const rounded = Math.round(shifted);
  return value < 0 && rounded !== 0 ? -rounded : rounded;
}

const invariantAmount = new Intl.NumberFormat("en-US", {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
  useGrouping: true,
});

/**
 * Mirrors C#'s `ToString("#,0.00", CultureInfo.InvariantCulture)` — "1,234.50", no currency sign —
 * the format the server's error messages print amounts in. Rounded through toHundredths first, so
 * the cents are the server's, not Intl's reading of the binary double.
 */
export function formatAmountInvariant(value: number): string {
  return invariantAmount.format(toHundredths(value) / 100);
}

/** Signed percentage, always carrying an explicit + or − so the sign never rests on colour. */
export function formatDeltaPct(deltaPct: number | null): string {
  if (deltaPct === null) return "—";
  const sign = deltaPct > 0 ? "+" : deltaPct < 0 ? "−" : "";
  return `${sign}${Math.abs(deltaPct).toFixed(1)}%`;
}

/** Signed dollar delta, same rule as formatDeltaPct: the sign is text, not a colour. */
export function formatDeltaCad(delta: number): string {
  const sign = delta > 0 ? "+" : delta < 0 ? "−" : "";
  return `${sign}$${Math.abs(delta).toLocaleString("en-CA")}`;
}

const cadWhole = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  maximumFractionDigits: 0,
});
const cadCents = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/**
 * A budget item's amount, to the cent when it has cents: "$5,400" but "$3.03". The copied
 * `formatCad` (lib/api/format.ts) prints whole dollars only, which was right while this app
 * planned in whole dollars — but an item built up as quantity × unit cost lands on cents, and
 * "$3" for a $3.03 item would disagree with what the server stored. Whole-dollar figures keep the
 * shorter form so the dashboard does not fill with ".00".
 */
export function formatCadPrecise(value: number): string {
  return Number.isInteger(Math.round(value * 100) / 100) ? cadWhole.format(value) : cadCents.format(value);
}
