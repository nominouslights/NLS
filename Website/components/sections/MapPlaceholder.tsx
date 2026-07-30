import { bodyStyle, cardStyle, chipStyle, colors, fonts } from "@/lib/theme";

// Stylized corridor art — the five real communities on the Thompson ↔ Lynn Lake
// corridor, drawn as an inline SVG (no map tiles, no external assets). Reused as
// the hero art and as the contact page's map placeholder.

const STOPS = [
  { name: "Lynn Lake", x: 110, y: 72, anchor: "start" as const, dx: 14, dy: 5 },
  { name: "Leaf Rapids", x: 190, y: 178, anchor: "start" as const, dx: 14, dy: 5 },
  { name: "South Indian Lake", x: 330, y: 112, anchor: "start" as const, dx: 14, dy: 5 },
  { name: "Black Sturgeon Falls", x: 318, y: 268, anchor: "end" as const, dx: -14, dy: -10 },
  { name: "Thompson", x: 402, y: 342, anchor: "start" as const, dx: 16, dy: 5 },
];

export function CorridorArt({ title = "Northern Link service corridor" }: { title?: string }) {
  return (
    <svg
      viewBox="0 0 520 420"
      role="img"
      aria-label={`${title}: route linking Thompson, Black Sturgeon Falls, Leaf Rapids, Lynn Lake and South Indian Lake, with the hub in Thompson`}
      style={{ width: "100%", height: "auto", display: "block" }}
    >
      <rect x="0" y="0" width="520" height="420" rx="16" fill={colors.sectionAlt} />
      {/* faint lakes / terrain suggestion */}
      <ellipse cx="300" cy="80" rx="52" ry="20" fill="#DFF0EA" />
      <ellipse cx="120" cy="300" rx="60" ry="24" fill="#DFF0EA" />
      <ellipse cx="440" cy="180" rx="40" ry="16" fill="#DFF0EA" />

      {/* main corridor: Thompson → Black Sturgeon Falls → Leaf Rapids → Lynn Lake */}
      <path
        d="M402 342 L318 268 L190 178 L110 72"
        fill="none"
        stroke={colors.teal}
        strokeWidth="6"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      {/* branch: Leaf Rapids → South Indian Lake */}
      <path
        d="M190 178 L330 112"
        fill="none"
        stroke={colors.teal}
        strokeWidth="4"
        strokeDasharray="2 10"
        strokeLinecap="round"
      />

      {STOPS.map((s) => (
        <g key={s.name}>
          <circle cx={s.x} cy={s.y} r={s.name === "Thompson" ? 13 : 8} fill="#FFFFFF" stroke={s.name === "Thompson" ? colors.gold : colors.tealDark} strokeWidth={s.name === "Thompson" ? 5 : 4} />
          {s.name === "Thompson" && <circle cx={s.x} cy={s.y} r="4.5" fill={colors.gold} />}
          <text
            x={s.x + s.dx}
            y={s.y + s.dy}
            textAnchor={s.anchor}
            style={{
              fontFamily: fonts.semiCondensed,
              fontWeight: 600,
              fontSize: 16,
              fill: colors.ink,
            }}
          >
            {s.name}
          </text>
          {s.name === "Thompson" && (
            <text
              x={s.x + s.dx}
              y={s.y + s.dy + 18}
              textAnchor={s.anchor}
              style={{ fontFamily: fonts.body, fontSize: 12.5, fill: colors.textMuted }}
            >
              Hub &amp; depot
            </text>
          )}
        </g>
      ))}

      <text
        x="24"
        y="36"
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 17,
          letterSpacing: "0.08em",
          textTransform: "uppercase",
          fill: colors.tealDark,
        }}
      >
        {title}
      </text>
      <text x="24" y="398" style={{ fontFamily: fonts.body, fontSize: 12.5, fill: colors.textMuted }}>
        Stylized — not to scale
      </text>
    </svg>
  );
}

export default function MapPlaceholder() {
  return (
    <div style={{ ...cardStyle(16) }}>
      <CorridorArt title="Where you'll find us" />
      <div style={{ display: "flex", flexWrap: "wrap", alignItems: "center", gap: 10, marginTop: 12 }}>
        <span style={chipStyle(colors.goldTint, colors.gold, "#7A6000")}>◔ Interactive map coming soon</span>
        <p style={bodyStyle(14, colors.textMuted)}>
          Our hub and depot are in Thompson, Manitoba, with scheduled service along the
          Thompson ↔ Lynn Lake corridor.
        </p>
      </div>
    </div>
  );
}
