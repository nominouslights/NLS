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
