import Link from "next/link";
import Wordmark from "@/components/ui/Wordmark";
import { NAV_ITEMS } from "@/lib/nav";
import { COMMUNITIES, CONTACT } from "@/lib/data";
import { bodyStyle, colors, fonts } from "@/lib/theme";

const colTitle = {
  fontFamily: fonts.condensed,
  fontWeight: 700,
  fontSize: 18,
  letterSpacing: "0.06em",
  textTransform: "uppercase" as const,
  color: "#FFFFFF",
  margin: "0 0 14px",
};

const footLink = {
  fontFamily: fonts.body,
  fontSize: 15,
  color: colors.footerText,
  display: "block",
  padding: "4px 0",
};

export default function Footer() {
  return (
    <footer style={{ background: colors.footerBg, padding: "56px 0 0", marginTop: 0 }}>
      <div style={{ maxWidth: 1120, margin: "0 auto", padding: "0 24px" }}>
        <div className="nl-grid nl-grid-4">
          <div>
            <Wordmark onDark />
            <p style={{ ...bodyStyle(14.5, colors.footerText), marginTop: 14, maxWidth: 280 }}>
              Shuttle, charter, cargo, and medical transportation connecting Northern
              Manitoba communities, with our hub and depot in Thompson.
            </p>
          </div>
          <div>
            <h3 style={colTitle}>Quick Links</h3>
            {NAV_ITEMS.map((i) => (
              <Link key={i.href} href={i.href} style={footLink}>
                {i.label}
              </Link>
            ))}
          </div>
          <div>
            <h3 style={colTitle}>Communities</h3>
            {COMMUNITIES.map((c) => (
              <span key={c} style={footLink}>
                {c}
              </span>
            ))}
          </div>
          <div>
            <h3 style={colTitle}>Contact</h3>
            <a href={`tel:${CONTACT.phone.replace(/[^\d+]/g, "")}`} style={footLink}>
              ☏ {CONTACT.phone}
            </a>
            <a href={`mailto:${CONTACT.email}`} style={footLink}>
              ✉ {CONTACT.email}
            </a>
            <span style={footLink}>◎ {CONTACT.address}</span>
          </div>
        </div>
        <div
          style={{
            borderTop: "1px solid rgba(255,255,255,.12)",
            marginTop: 44,
            padding: "18px 0",
            display: "flex",
            flexWrap: "wrap",
            gap: 12,
            justifyContent: "space-between",
          }}
        >
          <span style={bodyStyle(13.5, colors.footerTextDim)}>
            © {new Date().getFullYear()} Northern Link Shuttle &amp; Cargo · Thompson, Manitoba
          </span>
          <span style={bodyStyle(13.5, colors.footerTextDim)}>
            Class 4 licensed drivers · NSC-compliant fleet
          </span>
        </div>
      </div>
    </footer>
  );
}
