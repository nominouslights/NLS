import type { Testimonial } from "@/lib/types";
import { bodyStyle, cardStyle, chipStyle, colors, fonts } from "@/lib/theme";

export default function TestimonialCard({ t }: { t: Testimonial }) {
  return (
    <figure style={{ ...cardStyle(24), margin: 0, height: "100%" }}>
      <span style={chipStyle(colors.goldTint, colors.gold, "#7A6000")}>◔ Sample placeholder</span>
      <blockquote
        style={{
          ...bodyStyle(15.5, colors.text),
          fontStyle: "italic",
          margin: "14px 0 16px",
        }}
      >
        “{t.quote}”
      </blockquote>
      <figcaption>
        <div
          style={{
            fontFamily: fonts.semiCondensed,
            fontWeight: 600,
            fontSize: 15,
            color: colors.ink,
          }}
        >
          {t.name}
        </div>
        <div style={{ ...bodyStyle(13.5, colors.textMuted) }}>{t.role}</div>
      </figcaption>
    </figure>
  );
}
