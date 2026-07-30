import { bodyStyle, colors, containerStyle, fonts, headingStyle } from "@/lib/theme";

export default function PageHero({
  kicker,
  title,
  lead,
}: {
  kicker?: string;
  title: string;
  lead?: string;
}) {
  return (
    <section
      style={{
        background: `linear-gradient(180deg, ${colors.sectionAlt} 0%, ${colors.pageBg} 100%)`,
        borderBottom: `1px solid ${colors.border}`,
        padding: "56px 0 48px",
      }}
    >
      <div style={{ ...containerStyle(), textAlign: "center" }}>
        {kicker && (
          <div
            style={{
              fontFamily: fonts.semiCondensed,
              fontWeight: 600,
              fontSize: 14,
              letterSpacing: "0.22em",
              textTransform: "uppercase",
              color: colors.tealDark,
              marginBottom: 12,
            }}
          >
            {kicker}
          </div>
        )}
        <h1 style={headingStyle(44)}>{title}</h1>
        {lead && (
          <p
            style={{
              ...bodyStyle(17.5, colors.textMuted),
              maxWidth: 680,
              margin: "16px auto 0",
            }}
          >
            {lead}
          </p>
        )}
      </div>
    </section>
  );
}
