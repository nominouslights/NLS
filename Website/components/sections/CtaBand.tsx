import Button from "@/components/ui/Button";
import { CONTACT } from "@/lib/data";
import { bodyStyle, colors, containerStyle, headingStyle } from "@/lib/theme";

export default function CtaBand({
  title = "Ready to ride the corridor?",
  lead = "Request a seat, a charter quote, or a cargo run — we'll get back to you with times and a straight answer.",
}: {
  title?: string;
  lead?: string;
}) {
  return (
    <section
      style={{
        background: `linear-gradient(120deg, ${colors.tealDark} 0%, ${colors.teal} 100%)`,
        padding: "56px 0",
      }}
    >
      <div
        style={{
          ...containerStyle(),
          display: "flex",
          flexWrap: "wrap",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 24,
        }}
      >
        <div style={{ maxWidth: 620 }}>
          <h2 style={headingStyle(34, "#FFFFFF")}>{title}</h2>
          <p style={{ ...bodyStyle(16.5, "rgba(255,255,255,.9)"), marginTop: 10 }}>{lead}</p>
        </div>
        <div style={{ display: "flex", flexWrap: "wrap", gap: 14 }}>
          <Button href="/booking" variant="onDark" size="lg">
            Get a Quote
          </Button>
          <Button
            href={`tel:${CONTACT.phone.replace(/[^\d+]/g, "")}`}
            variant="ghost"
            size="lg"
            style={{ color: "#FFFFFF", borderColor: "rgba(255,255,255,.6)" }}
          >
            ☏ {CONTACT.phone}
          </Button>
        </div>
      </div>
    </section>
  );
}
