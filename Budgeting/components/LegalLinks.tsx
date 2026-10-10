import { colors, fonts } from "@/lib/theme";
import { LEGAL_LINKS, legalHref } from "@/lib/legal";

// "Privacy Policy · Licence Agreement" — a quiet line of text links to the static policy page.
// Shown on the sign-in screen (it must be reachable before anyone signs in) and at the foot of
// Settings for signed-in users. Each opens in a new tab so a half-filled form is never lost.

export default function LegalLinks({ align = "center" }: { align?: "center" | "left" }) {
  return (
    <nav
      aria-label="Legal"
      style={{
        display: "flex",
        justifyContent: align === "center" ? "center" : "flex-start",
        flexWrap: "wrap",
        gap: 8,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textDim,
        lineHeight: 1.6,
      }}
    >
      {LEGAL_LINKS.map(({ section, label }, i) => (
        <span key={section} style={{ display: "inline-flex", gap: 8 }}>
          {i > 0 && <span aria-hidden="true">·</span>}
          <a
            href={legalHref(section)}
            target="_blank"
            rel="noopener"
            style={{ color: colors.textMuted, textDecoration: "underline" }}
          >
            {label}
          </a>
        </span>
      ))}
    </nav>
  );
}
