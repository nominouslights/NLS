import { CONTACT } from "@/lib/data";
import { bodyStyle, cardStyle, colors, fonts } from "@/lib/theme";

const ITEMS = [
  {
    glyph: "☏",
    title: "Phone",
    body: CONTACT.phone,
    href: `tel:${CONTACT.phone.replace(/[^\d+]/g, "")}`,
  },
  {
    glyph: "✉",
    title: "Email",
    body: CONTACT.email,
    href: `mailto:${CONTACT.email}`,
  },
  {
    glyph: "◎",
    title: "Depot",
    body: CONTACT.address,
    href: undefined,
  },
] as const;

export default function ContactInfo() {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      {ITEMS.map((item) => (
        <div key={item.title} style={{ ...cardStyle(20), display: "flex", gap: 16, alignItems: "center" }}>
          <span
            aria-hidden="true"
            style={{
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              width: 46,
              height: 46,
              flex: "none",
              borderRadius: 10,
              background: colors.tealTint,
              border: `1px solid ${colors.border}`,
              color: colors.tealDark,
              fontSize: 20,
            }}
          >
            {item.glyph}
          </span>
          <div>
            <div
              style={{
                fontFamily: fonts.condensed,
                fontWeight: 700,
                fontSize: 19,
                textTransform: "uppercase",
                color: colors.ink,
              }}
            >
              {item.title}
            </div>
            {item.href ? (
              <a href={item.href} style={{ ...bodyStyle(16, colors.tealDark), fontWeight: 600 }}>
                {item.body}
              </a>
            ) : (
              <p style={bodyStyle(16)}>{item.body}</p>
            )}
          </div>
        </div>
      ))}
      <p style={bodyStyle(14, colors.textMuted)}>{CONTACT.hours}</p>
    </div>
  );
}
