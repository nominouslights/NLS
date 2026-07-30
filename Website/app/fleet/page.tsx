import type { Metadata } from "next";
import PageHero from "@/components/sections/PageHero";
import FleetShowcase from "@/components/sections/FleetShowcase";
import CtaBand from "@/components/sections/CtaBand";
import SectionHeading from "@/components/ui/SectionHeading";
import { COMPLIANCE_POINTS } from "@/lib/data";
import { bodyStyle, cardStyle, colors, containerStyle, fonts, sectionStyle } from "@/lib/theme";

export const metadata: Metadata = {
  title: "Fleet",
  description:
    "24-seat International coaches and 7-seat Ford Transit T-150 vans — an NSC-compliant fleet with daily inspections and Class 4 licensed drivers.",
};

export default function FleetPage() {
  return (
    <>
      <PageHero
        kicker="Our fleet"
        title="The right vehicle for the run"
        lead="Two vehicle classes cover everything we do: 24-seat International coaches for crews and community routes, and 7-seat Ford Transit T-150 vans for smaller trips, medical travel, and parcels."
      />
      <FleetShowcase heading={false} />

      <section style={sectionStyle(true)}>
        <div style={containerStyle()}>
          <SectionHeading
            kicker="Safety strip"
            title="Inspected daily, driven professionally"
            lead="Every vehicle in the fleet operates under the National Safety Code — the same standards that govern commercial carriers across Canada."
          />
          <div className="nl-grid nl-grid-4">
            {COMPLIANCE_POINTS.map((p) => (
              <div key={p.title} style={cardStyle(20)}>
                <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
                  <span aria-hidden="true" style={{ color: colors.teal, fontWeight: 700, fontSize: 17 }}>
                    ✓
                  </span>
                  <h3
                    style={{
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 20,
                      textTransform: "uppercase",
                      color: colors.ink,
                      margin: 0,
                    }}
                  >
                    {p.title}
                  </h3>
                </div>
                <p style={bodyStyle(14.5, colors.textMuted)}>{p.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      <CtaBand
        title="Need a coach or a van?"
        lead="Charter the whole vehicle or book a seat on a scheduled run — either way, it starts with a request."
      />
    </>
  );
}
