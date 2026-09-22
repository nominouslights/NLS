"use client";

import { gap } from "@/lib/tablet";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import type { InspectionGateVerdict } from "@/lib/inspectionGate";
import type { InspectionMode } from "@/lib/types";

// APP-LOCAL. The blocking banner, rendered identically on Manifest and on Today.
//
// It composes StatusBanner and TouchButton and introduces no new primitive — it exists only so
// the two screens cannot word the same block differently. Wording drift here is not cosmetic:
// the second paragraph is the admission that the server does not enforce this gate, and a
// screen that quietly loses it looks authoritative in a demo.
//
// WHY A BANNER AND NOT JUST THE TOOLTIP: StatusButton renders `disabledReason` as a `title`
// attribute (TouchButton.tsx:85-131), and a gloved finger on a touchscreen never produces a
// hover. A driver staring at a dead Board button with no reason on screen calls dispatch —
// which is the outcome this app exists to avoid.
//
// kind="over" (vermillion ▲) rather than "soon": this blocks work, it is not a heads-up.
//
// NO CTA WHEN `requires` IS NULL. A failed pre-trip is not fixed by another pre-trip, and
// offering one would invite a driver to re-inspect their way past a failure.

export function BoardingGateBanner({
  gate,
  onStartInspection,
}: {
  gate: InspectionGateVerdict;
  onStartInspection: (mode: InspectionMode) => void;
}) {
  if (gate.open) return null;

  const requires = gate.requires;

  return (
    <StatusBanner kind="over" title={gate.title}>
      {gate.reason}
      {requires ? (
        <div style={{ marginTop: gap.row }}>
          <TouchButton onClick={() => onStartInspection(requires)}>
            {gate.resumable ? "Resume" : "Start"}{" "}
            {requires === "PreTrip" ? "pre-trip" : "post-trip"} inspection
          </TouchButton>
        </div>
      ) : null}
    </StatusBanner>
  );
}
