import SectionHeading from "@/components/ui/SectionHeading";
import ServiceCard from "@/components/ui/ServiceCard";
import { SERVICES } from "@/lib/data";
import { containerStyle, sectionStyle } from "@/lib/theme";

export default function ServicesGrid({
  heading = true,
  alt = false,
}: {
  heading?: boolean;
  alt?: boolean;
}) {
  return (
    <section style={sectionStyle(alt)}>
      <div style={containerStyle()}>
        {heading && (
          <SectionHeading
            kicker="What we do"
            title="Six services, one corridor"
            lead="From daily crew shuttles to a weekly grocery run — every service is built around keeping Northern Manitoba's communities moving."
          />
        )}
        <div className="nl-grid nl-grid-3">
          {SERVICES.map((s) => (
            <ServiceCard key={s.slug} service={s} />
          ))}
        </div>
      </div>
    </section>
  );
}
