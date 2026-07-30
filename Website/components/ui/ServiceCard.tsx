import Link from "next/link";
import type { Service } from "@/lib/types";
import { bodyStyle, cardStyle, colors, fonts } from "@/lib/theme";

export default function ServiceCard({ service }: { service: Service }) {
  return (
    <Link
      href={`/services/${service.slug}`}
      className="nl-card-link"
      style={{ ...cardStyle(24), color: "inherit", height: "100%" }}
    >
      <span
        aria-hidden="true"
        style={{
          display: "inline-flex",
          alignItems: "center",
          justifyContent: "center",
          width: 44,
          height: 44,
          borderRadius: 10,
          background: colors.tealTint,
          border: `1px solid ${colors.border}`,
          color: colors.tealDark,
          fontSize: 18,
          marginBottom: 14,
        }}
      >
        {service.glyph}
      </span>
      <h3
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: 23,
          textTransform: "uppercase",
          letterSpacing: "0.01em",
          color: colors.ink,
          margin: "0 0 8px",
        }}
      >
        {service.name}
      </h3>
      <p style={bodyStyle(15, colors.textMuted)}>{service.short}</p>
      <span
        style={{
          display: "inline-flex",
          alignItems: "center",
          gap: 6,
          marginTop: 14,
          fontFamily: fonts.semiCondensed,
          fontWeight: 600,
          fontSize: 14.5,
          color: colors.tealDark,
        }}
      >
        Learn more <span aria-hidden="true">→</span>
      </span>
    </Link>
  );
}
