import { colors, fonts } from "@/lib/theme";

// Typographic wordmark — no logo assets exist yet, so the brand is set in type.
export default function Wordmark({
  onDark = false,
  size = 22,
}: {
  onDark?: boolean;
  size?: number;
}) {
  return (
    <span style={{ display: "inline-flex", flexDirection: "column", lineHeight: 1 }}>
      <span
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: size,
          letterSpacing: "0.04em",
          textTransform: "uppercase",
          color: onDark ? "#FFFFFF" : colors.ink,
        }}
      >
        Northern <span style={{ color: colors.teal }}>Link</span>
      </span>
      <span
        style={{
          fontFamily: fonts.semiCondensed,
          fontWeight: 600,
          fontSize: size * 0.42,
          letterSpacing: "0.28em",
          textTransform: "uppercase",
          color: onDark ? colors.footerTextDim : colors.textMuted,
          marginTop: 3,
        }}
      >
        Shuttle &amp; Cargo
      </span>
    </span>
  );
}
