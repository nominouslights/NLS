import { colors, fonts } from "@/lib/theme";
import { periodLabel, stepPeriod, withGranularity, type Granularity, type Period } from "@/lib/period";

const GRANULARITIES: { value: Granularity; label: string }[] = [
  { value: "month", label: "Month" },
  { value: "quarter", label: "Quarter" },
];

/** Shared pill treatment — the same one the Trips filter row uses. */
function pill(active: boolean) {
  return {
    fontFamily: fonts.body,
    fontWeight: active ? 600 : 500,
    fontSize: 12,
    padding: "5px 12px",
    borderRadius: 7,
    background: active ? colors.cardBgActive : colors.cardBg,
    border: `1px solid ${active ? colors.borderActive : colors.border}`,
    color: active ? colors.headingBright : colors.textMuted,
    cursor: "pointer",
  } as const;
}

function Arrow({ glyph, label, onClick }: { glyph: string; label: string; onClick: () => void }) {
  return (
    <span
      onClick={onClick}
      role="button"
      aria-label={label}
      title={label}
      style={{
        ...pill(false),
        padding: "5px 10px",
        fontSize: 14,
        lineHeight: 1.1,
        color: colors.textSecondary,
      }}
    >
      {glyph}
    </span>
  );
}

/**
 * Period navigator for a list bounded by a month or a quarter: ‹ / › step one whole
 * period, and the granularity pills re-bucket around the period currently shown
 * (so Month→Quarter on August lands on that August's quarter, not today's).
 * A caller locked to one granularity (e.g. the monthly accruals report) passes
 * `granularities={["month"]}` — a single choice renders no pills at all.
 */
export function PeriodNav({
  period,
  onChange,
  granularities = ["month", "quarter"],
}: {
  period: Period;
  onChange: (next: Period) => void;
  /** Which granularity pills to offer; defaults to both. */
  granularities?: Granularity[];
}) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
      <Arrow glyph="‹" label="Previous period" onClick={() => onChange(stepPeriod(period, -1))} />
      <span
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 15,
          letterSpacing: ".02em",
          color: colors.headingBright,
          minWidth: 150,
          textAlign: "center",
        }}
      >
        {periodLabel(period)}
      </span>
      <Arrow glyph="›" label="Next period" onClick={() => onChange(stepPeriod(period, 1))} />

      {granularities.length > 1 && (
        <span style={{ display: "flex", gap: 6, marginLeft: 6 }}>
          {GRANULARITIES.filter((g) => granularities.includes(g.value)).map((g) => (
            <span
              key={g.value}
              onClick={() => onChange(withGranularity(period, g.value))}
              style={pill(period.granularity === g.value)}
            >
              {g.label}
            </span>
          ))}
        </span>
      )}
    </div>
  );
}
