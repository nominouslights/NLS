// Signed-money formatters. They live here, apart from lib/data.ts, so a screen on real data
// (the period dashboard's Net tile) never has to import the mock module to print a signed
// figure. The rule both enforce is the platform's: the sign is written out as text, never
// implied by red-versus-green, so it survives grayscale and any colour-vision deficiency.

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
