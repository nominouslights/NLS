import type { Metadata } from "next";
import { notFound } from "next/navigation";
import PageHero from "@/components/sections/PageHero";
import CtaBand from "@/components/sections/CtaBand";
import Button from "@/components/ui/Button";
import PhotoSlot from "@/components/ui/PhotoSlot";
import ServiceCard from "@/components/ui/ServiceCard";
import SectionHeading from "@/components/ui/SectionHeading";
import { SERVICES } from "@/lib/data";
import { bodyStyle, cardStyle, colors, containerStyle, fonts, sectionStyle } from "@/lib/theme";

interface Props {
  params: Promise<{ slug: string }>;
}

export function generateStaticParams() {
  return SERVICES.map((s) => ({ slug: s.slug }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const { slug } = await params;
  const service = SERVICES.find((s) => s.slug === slug);
  if (!service) return { title: "Service not found" };
  return { title: service.name, description: service.short };
}

export default async function ServiceDetailPage({ params }: Props) {
  const { slug } = await params;
  const service = SERVICES.find((s) => s.slug === slug);
  if (!service) notFound();

  const related = SERVICES.filter((s) => s.slug !== service.slug).slice(0, 3);

  return (
    <>
      <PageHero kicker="Our services" title={service.name} lead={service.short} />

      <section style={sectionStyle()}>
        <div style={containerStyle()}>
          <div className="nl-hero-split" style={{ alignItems: "start" }}>
            <div>
              <h2
                style={{
                  fontFamily: fonts.condensed,
                  fontWeight: 700,
                  fontSize: 30,
                  textTransform: "uppercase",
                  color: colors.ink,
                  margin: "0 0 16px",
                }}
              >
                Overview
              </h2>
              {service.overview.map((p) => (
                <p key={p.slice(0, 32)} style={{ ...bodyStyle(16.5), marginBottom: 14 }}>
                  {p}
                </p>
              ))}
              <div style={{ ...cardStyle(20), marginTop: 8, background: colors.sectionAlt }}>
                <h3
                  style={{
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 19,
                    textTransform: "uppercase",
                    color: colors.ink,
                    margin: "0 0 6px",
                  }}
                >
                  Who it&apos;s for
                </h3>
                <p style={bodyStyle(15.5)}>{service.audience}</p>
              </div>
              <div style={{ marginTop: 24 }}>
                <Button href={`/booking?service=${service.slug}`} size="lg">
                  Book this service
                </Button>
              </div>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 20 }}>
              <PhotoSlot label={`${service.name} in action`} height={220} />
              <div style={cardStyle(24)}>
                <h3
                  style={{
                    fontFamily: fonts.condensed,
                    fontWeight: 700,
                    fontSize: 21,
                    textTransform: "uppercase",
                    color: colors.ink,
                    margin: "0 0 12px",
                  }}
                >
                  At a glance
                </h3>
                <ul style={{ margin: 0, padding: 0, listStyle: "none" }}>
                  {service.features.map((f) => (
                    <li
                      key={f}
                      style={{
                        ...bodyStyle(15),
                        display: "flex",
                        gap: 10,
                        alignItems: "baseline",
                        padding: "5px 0",
                      }}
                    >
                      <span aria-hidden="true" style={{ color: colors.teal, fontWeight: 700 }}>
                        ✓
                      </span>
                      {f}
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section style={sectionStyle(true)}>
        <div style={containerStyle()}>
          <SectionHeading kicker="More from Northern Link" title="Related services" />
          <div className="nl-grid nl-grid-3">
            {related.map((s) => (
              <ServiceCard key={s.slug} service={s} />
            ))}
          </div>
        </div>
      </section>

      <CtaBand
        title={`Ready to book ${service.name.toLowerCase()}?`}
        lead="Send a booking request or call us — we'll confirm times and details directly."
      />
    </>
  );
}
