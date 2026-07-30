import type { Metadata } from "next";
import PageHero from "@/components/sections/PageHero";
import ServicesGrid from "@/components/sections/ServicesGrid";
import CtaBand from "@/components/sections/CtaBand";

export const metadata: Metadata = {
  title: "Services",
  description:
    "Six services on one corridor: crew shuttles, community shuttle, NIHB medical transport, charters, cargo & parcel, and the weekly grocery run.",
};

export default function ServicesPage() {
  return (
    <>
      <PageHero
        kicker="Services"
        title="Six services, one corridor"
        lead="Everything we run is built around the Thompson ↔ Lynn Lake corridor and the five communities on it. Pick the service that fits — or call and we'll figure it out together."
      />
      <ServicesGrid heading={false} />
      <CtaBand />
    </>
  );
}
