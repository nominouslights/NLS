// COPIED FROM Dispatcher/lib/api/format.ts — keep identical below this header.
// Change Dispatcher first, then re-copy. Drift check: see Budgeting/CLAUDE.md.
// ---------------------------------------------------------------------------
// Cross-domain display formatters — used by fleet, billing, clients, trips
// screens alike. Not tied to any one backend module's contract.
// ---------------------------------------------------------------------------

const cadFmt = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  maximumFractionDigits: 0,
});

export function formatCad(value: number): string {
  return cadFmt.format(value);
}

export function formatKm(value: number): string {
  return `${value.toLocaleString("en-CA")} km`;
}

export function formatUtcDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleDateString("en-CA", { year: "numeric", month: "short", day: "numeric" });
}
