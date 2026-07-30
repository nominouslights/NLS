import Button from "@/components/ui/Button";
import { CorridorArt } from "@/components/sections/MapPlaceholder";
import { bodyStyle, colors, containerStyle, fonts, headingStyle } from "@/lib/theme";

export default function Hero() {
  return (
    <section
      style={{
        background: `linear-gradient(180deg, ${colors.sectionAlt} 0%, ${colors.pageBg} 100%)`,
        padding: "72px 0 64px",
        borderBottom: `1px solid ${colors.border}`,
      }}
    >
      <div style={containerStyle()}>
        <div className="nl-hero-split">
          <div>
            <div
              style={{
                fontFamily: fonts.semiCondensed,
                fontWeight: 600,
                fontSize: 14.5,
                letterSpacing: "0.22em",
                textTransform: "uppercase",
                color: colors.tealDark,
                marginBottom: 14,
              }}
            >
              Thompson · Northern Manitoba
            </div>
            <h1 style={headingStyle(52)}>
              The road link for
              <br />
              <span style={{ color: colors.tealDark }}>Northern Manitoba</span>
            </h1>
            <p style={{ ...bodyStyle(18, colors.textMuted), marginTop: 18, maxWidth: 520 }}>
              Shuttle, charter, cargo, and medical transportation connecting Thompson,
              Leaf Rapids, Lynn Lake, South Indian Lake, and Black Sturgeon Falls —
              driven by Class 4 professionals on an NSC-compliant fleet.
            </p>
            <div style={{ display: "flex", flexWrap: "wrap", gap: 14, marginTop: 28 }}>
              <Button href="/booking" size="lg">
                Book a Trip
              </Button>
              <Button href="/services" variant="secondary" size="lg">
                Explore Services
              </Button>
            </div>
          </div>
          <div>
            <CorridorArt />
          </div>
        </div>
      </div>
    </section>
  );
}
