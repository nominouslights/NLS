"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, radius, touch, type, wizard } from "@/lib/tablet";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { odometerError } from "@/lib/inspectionGate";

// APP-LOCAL. The wizard's first step: the odometer reading.
//
// Asked first because every other value in the report hangs off it — odometer-in on a pre-trip,
// odometer-out on a post-trip (VehicleInspection.OdometerKm's own doc comment).
//
// The rejection rule is NOT invented here: it mirrors Vehicle.RecordOdometer's monotonic guard,
// which PropagateInspectionOdometerCommandHandler feeds an inspection's reading into. See
// odometerError() in lib/inspectionGate.ts and its test. Better to say so on this step than to
// let a driver certify 22 answers against a number the server will bounce.

export function OdometerStep({
  unit,
  lastReadingKm,
  value,
  onChange,
}: {
  unit: string;
  lastReadingKm: number;
  value: number | null;
  onChange: (next: number | null) => void;
}) {
  const error = value === null ? null : odometerError(value, lastReadingKm);

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: gap.section, minHeight: 0 }}>
      <div
        style={{
          fontFamily: fonts.condensed,
          fontWeight: 700,
          fontSize: wizard.question,
          lineHeight: 1.1,
          color: colors.headingBright,
        }}
      >
        What does the odometer read?
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: gap.row }}>
        <input
          value={value === null ? "" : String(value)}
          onChange={(e) => {
            const raw = e.target.value.replace(/[^0-9]/g, "");
            onChange(raw === "" ? null : Number(raw));
          }}
          inputMode="numeric"
          autoFocus
          aria-label={`Odometer reading for ${unit}, in kilometres`}
          placeholder="0"
          style={{
            width: 340,
            minHeight: touch.primary + 24,
            padding: "0 20px",
            borderRadius: radius.control,
            border: `1px solid ${error ? colors.borderStrong : colors.border}`,
            background: colors.inputBg,
            color: colors.headingBright,
            fontFamily: fonts.mono,
            fontVariantNumeric: "tabular-nums",
            fontSize: 38,
          }}
        />
        <span
          style={{
            fontFamily: fonts.semiCondensed,
            fontSize: type.value,
            letterSpacing: ".1em",
            textTransform: "uppercase",
            color: colors.textDim,
          }}
        >
          km
        </span>
      </div>

      <div
        style={{
          fontFamily: fonts.body,
          fontSize: type.label,
          color: colors.textDim,
          lineHeight: 1.55,
        }}
      >
        Last recorded reading for {unit}:{" "}
        <span style={{ fontFamily: fonts.mono, color: colors.textSecondary }}>
          {lastReadingKm.toLocaleString("en-CA")} km
        </span>
        . A reading equal to it is fine — a vehicle that has not moved since the post-trip is
        the normal case.
      </div>

      {error ? (
        <StatusBanner kind="over" title="That reading cannot be right.">
          {error}
        </StatusBanner>
      ) : null}
    </div>
  );
}
