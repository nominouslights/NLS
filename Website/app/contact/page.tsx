import type { Metadata } from "next";
import PageHero from "@/components/sections/PageHero";
import ContactInfo from "@/components/sections/ContactInfo";
import ContactForm from "@/components/forms/ContactForm";
import MapPlaceholder from "@/components/sections/MapPlaceholder";
import { containerStyle, sectionStyle } from "@/lib/theme";

export const metadata: Metadata = {
  title: "Contact",
  description:
    "Reach Northern Link Shuttle & Cargo in Thompson, Manitoba — phone, email, or the contact form.",
};

export default function ContactPage() {
  return (
    <>
      <PageHero
        kicker="Contact"
        title="Talk to a person"
        lead="Bookings, quotes, cargo, medical travel coordination — reach us however suits you."
      />
      <section style={sectionStyle()}>
        <div style={containerStyle()}>
          <div className="nl-contact-split">
            <ContactInfo />
            <ContactForm />
          </div>
        </div>
      </section>
      <section style={{ ...sectionStyle(true), paddingTop: 0 }}>
        <div style={containerStyle(880)}>
          <MapPlaceholder />
        </div>
      </section>
    </>
  );
}
