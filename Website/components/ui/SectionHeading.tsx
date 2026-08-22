import { bodyStyle, colors, fonts, headingStyle } from "@/lib/theme";

export default function SectionHeading({
  kicker,
  title,
  lead,
  align = "center",
  onDark = false,
}: {
  kicker?: string;
  title: string;
  lead?: string;
  align?: "left" | "center";
  onDark?: boolean;
}) {
  return (
    <div
      style={{
        textAlign: align,
        maxWidth: 720,
        margin: align === "center" ? "0 auto 48px" : "0 0 40px",
      }}
    >
      {kicker && (
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontWeight: 600,
            fontSize: 14,
            letterSpacing: "0.22em",
            textTransform: "uppercase",
            color: colors.teal,
            marginBottom: 10,
          }}
        >
          {kicker}
        </div>
      )}
      <h2 style={headingStyle(36, onDark ? "#FFFFFF" : colors.ink)}>{title}</h2>
      {lead && (
        <p style={{ ...bodyStyle(17, onDark ? colors.footerText : colors.textMuted), marginTop: 14 }}>
          {lead}
        </p>
      )}
    </div>
  );
}
