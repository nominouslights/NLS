import Wordmark from "@/components/ui/Wordmark";
import Button from "@/components/ui/Button";
import { bodyStyle, colors, containerStyle, headingStyle, sectionStyle } from "@/lib/theme";

export default function NotFound() {
  return (
    <section style={{ ...sectionStyle(true), minHeight: "55vh", display: "flex", alignItems: "center" }}>
      <div style={{ ...containerStyle(640), textAlign: "center" }}>
        <div style={{ marginBottom: 24 }}>
          <Wordmark size={28} />
        </div>
        <h1 style={headingStyle(40)}>Page not found</h1>
        <p style={{ ...bodyStyle(17, colors.textMuted), margin: "14px 0 28px" }}>
          That page isn&apos;t on our route map. Head back to the homepage, or use the
          menu to find services, fleet, booking, and contact details.
        </p>
        <Button href="/" size="lg">
          Back to home
        </Button>
      </div>
    </section>
  );
}
