import type { CSSProperties } from "react";
import { colors, fonts } from "@/lib/theme";
import { LEGAL_LINKS, policiesHref } from "@/lib/legal";

// "Privacy Policy · Licence Agreement" footer line. Used on the sign-in screen (reachable
// signed out) and the Home launcher (persistent for signed-in users). New tab, so leaving the
// console never drops an in-progress session or form.

export default function LegalLinks({ style }: { style?: CSSProperties }) {
  const link: CSSProperties = {
    color: colors.textMuted,
    textDecoration: "underline",
    textUnderlineOffset: 3,
  };
  return (
    <nav
      aria-label="Legal"
      style={{
        display: "flex",
        justifyContent: "center",
        flexWrap: "wrap",
        gap: 8,
        fontFamily: fonts.body,
        fontSize: 11.5,
        color: colors.textDim,
        ...style,
      }}
    >
      {LEGAL_LINKS.map((l, i) => (
        <span key={l.anchor} style={{ display: "inline-flex", gap: 8 }}>
          {i > 0 && <span aria-hidden>·</span>}
          <a href={policiesHref(l.anchor)} target="_blank" rel="noopener" style={link}>
            {l.label}
          </a>
        </span>
      ))}
    </nav>
  );
}
