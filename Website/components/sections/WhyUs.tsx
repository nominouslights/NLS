import SectionHeading from "@/components/ui/SectionHeading";
import StatCounter from "@/components/ui/StatCounter";
import { COMPLIANCE_POINTS, STATS } from "@/lib/data";
import { bodyStyle, cardStyle, colors, containerStyle, fonts, sectionStyle } from "@/lib/theme";

export default function WhyUs() {
  return (
    <section style={sectionStyle(true)}>
      <div style={containerStyle()}>
        <SectionHeading
          kicker="Why Northern Link"
          title="Built for northern roads"
          lead="We're based in Thompson and we drive these highways every week — safety and compliance aren't add-ons, they're the operation."
        />
        <div className="nl-grid nl-grid-4" style={{ marginBottom: 40 }}>
          {STATS.map((s) => (
            <StatCounter key={s.label} stat={s} />
          ))}
        </div>
        <div className="nl-grid nl-grid-4">
          {COMPLIANCE_POINTS.map((p) => (
            <div key={p.title} style={cardStyle(20)}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 8,
                  marginBottom: 8,
                }}
              >
                <span aria-hidden="true" style={{ color: colors.teal, fontWeight: 700, fontSize: 17 }}>
                  ✓
                </span>
                <h3
                  style={{
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 20,
                    textTransform: "uppercase",
                    color: colors.ink,
                    margin: 0,
                  }}
                >
                  {p.title}
                </h3>
              </div>
              <p style={bodyStyle(14.5, colors.textMuted)}>{p.body}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
