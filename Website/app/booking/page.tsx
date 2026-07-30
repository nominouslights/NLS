import type { Metadata } from "next";
import { Suspense } from "react";
import PageHero from "@/components/sections/PageHero";
import BookingForm from "@/components/forms/BookingForm";
import { bodyStyle, cardStyle, colors, containerStyle, fonts, sectionStyle } from "@/lib/theme";

export const metadata: Metadata = {
  title: "Booking",
  description:
    "Request a seat, a charter quote, or a cargo run with Northern Link Shuttle & Cargo.",
};

const NEXT_STEPS = [
  {
    glyph: "①",
    title: "You send a request",
    body: "Tell us who's travelling (or what's shipping), where, and when.",
  },
  {
    glyph: "②",
    title: "We confirm directly",
    body: "We reply by phone or email with times, pickup point, and fare or quote.",
  },
  {
    glyph: "③",
    title: "You ride the corridor",
    body: "A Class 4 licensed driver and an inspected vehicle, on schedule.",
  },
] as const;

export default function BookingPage() {
  return (
    <>
      <PageHero
        kicker="Booking"
        title="Request a trip or a quote"
        lead="Seats, charters, cargo, NIHB voucher travel, Gift-a-Seat — one form covers it. We confirm every request personally."
      />

      <section style={sectionStyle()}>
        <div style={containerStyle(880)}>
          <Suspense fallback={null}>
            <BookingForm />
          </Suspense>
        </div>
      </section>

      <section style={sectionStyle(true)}>
        <div style={containerStyle()}>
          <div className="nl-grid nl-grid-3">
            {NEXT_STEPS.map((s) => (
              <div key={s.title} style={cardStyle(22)}>
                <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 8 }}>
                  <span aria-hidden="true" style={{ color: colors.tealDark, fontSize: 24 }}>
                    {s.glyph}
                  </span>
                  <h3
                    style={{
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 21,
                      textTransform: "uppercase",
                      color: colors.ink,
                      margin: 0,
                    }}
                  >
                    {s.title}
                  </h3>
                </div>
                <p style={bodyStyle(15, colors.textMuted)}>{s.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>
    </>
  );
}
