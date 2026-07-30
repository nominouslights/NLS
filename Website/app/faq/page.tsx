import type { Metadata } from "next";
import PageHero from "@/components/sections/PageHero";
import FaqAccordion from "@/components/sections/FaqAccordion";
import CtaBand from "@/components/sections/CtaBand";
import { containerStyle, sectionStyle } from "@/lib/theme";

export const metadata: Metadata = {
  title: "FAQ",
  description:
    "Answers about booking, routes, cargo, and NIHB medical travel with Northern Link Shuttle & Cargo.",
};

export default function FaqPage() {
  return (
    <>
      <PageHero
        kicker="FAQ"
        title="Questions, answered"
        lead="Booking, routes, cargo, and medical travel — if it's not covered here, call or email and a person will answer."
      />
      <section style={sectionStyle()}>
        <div style={containerStyle(880)}>
          <FaqAccordion />
        </div>
      </section>
      <CtaBand
        title="Still have a question?"
        lead="Skip the form if you like — phone and email go straight to us."
      />
    </>
  );
}
