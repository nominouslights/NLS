import type { Metadata } from "next";
import PageHero from "@/components/sections/PageHero";
import CtaBand from "@/components/sections/CtaBand";
import StatCounter from "@/components/ui/StatCounter";
import PhotoSlot from "@/components/ui/PhotoSlot";
import SectionHeading from "@/components/ui/SectionHeading";
import { COMPLIANCE_POINTS, STATS } from "@/lib/data";
import { bodyStyle, cardStyle, colors, containerStyle, fonts, sectionStyle } from "@/lib/theme";

export const metadata: Metadata = {
  title: "About",
  description:
    "Northern Link Shuttle & Cargo is based in Thompson, Manitoba, connecting northern communities with shuttle, charter, cargo, and medical transportation.",
};

export default function AboutPage() {
  return (
    <>
      <PageHero
        kicker="About us"
        title="Rooted in Thompson, built for the North"
        lead="Northern Link Shuttle & Cargo exists because northern communities deserve dependable transportation — not whenever it's convenient, but every week, all year."
      />

      <section style={sectionStyle()}>
        <div style={containerStyle()}>
          <div className="nl-hero-split">
            <div>
              <h2
                style={{
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 32,
                  textTransform: "uppercase",
                  color: colors.ink,
                  margin: "0 0 16px",
                }}
              >
                Our story
              </h2>
              <p style={{ ...bodyStyle(16.5), marginBottom: 14 }}>
                From our hub and depot in Thompson, Manitoba, we run scheduled and
                contracted transportation along the Thompson ↔ Lynn Lake corridor —
                serving Thompson, Leaf Rapids, Lynn Lake, South Indian Lake, and Black
                Sturgeon Falls.
              </p>
              <p style={{ ...bodyStyle(16.5), marginBottom: 14 }}>
                The work spans six service lines: corporate crew shuttles (including our
                crew contract with Alamos Gold&apos;s Lynn Lake operations), the community
                shuttle with its Gift-a-Seat program, voucher-based NIHB medical
                transport, private charters, cargo and parcel service, and a weekly
                grocery run.
              </p>
              <p style={bodyStyle(16.5)}>
                Different services, one idea: if it moves people or goods between these
                communities, it should be safe, on time, and run by people who live here.
              </p>
            </div>
            <PhotoSlot label="Our team and coach at the Thompson depot" height={320} />
          </div>
        </div>
      </section>

      <section style={sectionStyle(true)}>
        <div style={containerStyle()}>
          <SectionHeading
            kicker="By the numbers"
            title="What we run"
          />
          <div className="nl-grid nl-grid-4">
            {STATS.map((s) => (
              <StatCounter key={s.label} stat={s} />
            ))}
          </div>
        </div>
      </section>

      <section style={sectionStyle()}>
        <div style={containerStyle()}>
          <SectionHeading
            kicker="Safety & compliance"
            title="The standards behind every run"
            lead="Commercial passenger transport in the North is a responsibility. These aren't marketing lines — they're the regime every vehicle and driver operates under."
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
        title="Work with a northern carrier"
        lead="Crew contracts, community service, cargo — talk to us about what your community or operation needs."
      />
    </>
  );
}
