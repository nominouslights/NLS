import { colors, fonts } from "@/lib/theme";
import { gap, touch, type } from "@/lib/tablet";
import { policyHref } from "@/lib/legal";

// "Privacy Policy" and "Licence Agreement" — the owner's policy document, which covers this app
// including on-duty location collection. Rendered on the sign-in screen and at the foot of
// My Profile, so a driver can read it before signing in and at any point after.
//
// Each link is a 44px (touch.min) target: tappable, but not a real action, so not 56. Opens in
// a new browsing context because the installed app is display: fullscreen — navigating the app
// window itself away would leave a driver with no visible way back to the duty screen. In an
// installed WebAPK an in-scope _blank opens as a closable custom tab.

const linkStyle = {
  minHeight: touch.min,
  display: "inline-flex",
  alignItems: "center",
  padding: `0 ${gap.row}px`,
  borderRadius: 8,
  fontFamily: fonts.body,
  fontSize: type.label,
  fontWeight: 600,
  color: colors.blue,
  textDecoration: "underline",
  textUnderlineOffset: 3,
} as const;

export default function LegalLinks({ align = "center" }: { align?: "center" | "start" }) {
  return (
    <nav
      aria-label="Legal"
      style={{
        display: "flex",
        justifyContent: align === "center" ? "center" : "flex-start",
        flexWrap: "wrap",
        gap: gap.tight,
      }}
    >
      <a href={policyHref("privacy")} target="_blank" rel="noopener" style={linkStyle}>
        Privacy Policy
      </a>
      <a href={policyHref("eula")} target="_blank" rel="noopener" style={linkStyle}>
        Licence Agreement
      </a>
    </nav>
  );
}
