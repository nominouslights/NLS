import SectionHeading from "@/components/ui/SectionHeading";
import TestimonialCard from "@/components/ui/TestimonialCard";
import { TESTIMONIALS } from "@/lib/data";
import { bodyStyle, colors, containerStyle, sectionStyle } from "@/lib/theme";

export default function Testimonials() {
  return (
    <section style={sectionStyle(true)}>
      <div style={containerStyle()}>
        <SectionHeading
          kicker="Riders & partners"
          title="What people say"
          lead="Real rider and client stories are being collected — the cards below are placeholders showing where they'll live."
        />
        <div className="nl-grid nl-grid-3">
          {TESTIMONIALS.map((t) => (
            <TestimonialCard key={t.name} t={t} />
          ))}
        </div>
        <p style={{ ...bodyStyle(14, colors.textMuted), textAlign: "center", marginTop: 24, fontStyle: "italic" }}>
          Sample quotes shown — real rider and client stories coming soon.
        </p>
      </div>
    </section>
  );
}
