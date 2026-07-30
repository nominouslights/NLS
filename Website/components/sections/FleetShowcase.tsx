import SectionHeading from "@/components/ui/SectionHeading";
import FleetCard from "@/components/ui/FleetCard";
import { FLEET } from "@/lib/data";
import { containerStyle, sectionStyle } from "@/lib/theme";

export default function FleetShowcase({
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
            kicker="Our fleet"
            title="The right vehicle for the run"
            lead="24-seat International coaches for crews and community routes, 7-seat Ford Transit T-150 vans for smaller trips — all inspected daily."
          />
        )}
        <div className="nl-grid nl-grid-2">
          {FLEET.map((v) => (
            <FleetCard key={v.id} vehicle={v} />
          ))}
        </div>
      </div>
    </section>
  );
}
