"use client";

import { colors, fonts } from "@/lib/theme";
import { gap, radius, touch, type, wizard } from "@/lib/tablet";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { INSPECTION_LOCATION_MAX, locationError, odometerError } from "@/lib/inspectionGate";

// APP-LOCAL. The wizard's first step: the report's header — the odometer reading and where the
// inspection is being done.
//
// The odometer is asked first because every other value in the report hangs off it —
// odometer-in on a pre-trip, odometer-out on a post-trip (VehicleInspection.OdometerKm's own
// doc comment). The location sits on the same step because it is the same KIND of value: a
// fact about the report, not a check of the vehicle. Man. Reg. 95/2008 s.12(1) requires the
// report to name "the municipality or description of the highway location where the
// inspection was performed", so it is required here even though the wire field is nullable.
//
// The rejection rules are NOT invented here: odometerError() mirrors Vehicle.RecordOdometer's
// monotonic guard, and locationError() mirrors VehicleInspection.Create's Normalize + 200-char
// LocationTooLong check. See lib/inspectionGate.ts and its test. Better to say so on this step
// than to let a driver certify a whole walk-around against a header the server will bounce.
//
// HEIGHT BUDGET (WizardFrame has no scroll — see lib/tablet.ts `wizard`): question 44 + input
// 80 + last-reading line ~50 + location label 28 + input 56 + hint ~22 + five gaps ≈ 380, plus
// at most ONE banner ~80 when a value is wrong — inside the ~524 a step has. The location input is a
// single line at touch.primary, not a textarea: a town or highway fits on one, and two rows
// would spend the slack a banner needs.

export function OdometerStep({
  unit,
  lastReadingKm,
  value,
  onChange,
  location,
  onLocationChange,
}: {
  unit: string;
  lastReadingKm: number;
  value: number | null;
  onChange: (next: number | null) => void;
  /** Raw, as typed. Trimmed at submit. */
  location: string;
  onLocationChange: (next: string) => void;
}) {
  const error = value === null ? null : odometerError(value, lastReadingKm);
  // Blank is not shown as an error banner — the step has only just opened, and the Continue
  // button's own disabled reason already says what is owed. Too long IS shown, because a driver
  // who pasted or kept typing cannot otherwise see why Continue is greyed.
  const trimmedLength = location.trim().length;
  const locError = trimmedLength > INSPECTION_LOCATION_MAX ? locationError(location) : null;

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

      <div style={{ display: "flex", flexDirection: "column", gap: gap.tight }}>
        <label
          htmlFor="inspection-location"
          style={{
            fontFamily: fonts.semiCondensed,
            fontWeight: 600,
            fontSize: type.value,
            color: colors.headingBright,
          }}
        >
          Location (town or highway)
        </label>
        {/* No maxLength: a browser cap silently truncates a paste, which would put a different
            location on the report than the one the driver entered. The limit is shown and
            enforced instead. */}
        <input
          id="inspection-location"
          value={location}
          onChange={(e) => onLocationChange(e.target.value)}
          autoComplete="off"
          enterKeyHint="done"
          placeholder="e.g. Lynn Lake, or PTH 391 at km 42"
          aria-invalid={locError !== null}
          style={{
            width: "100%",
            maxWidth: 720,
            minHeight: touch.primary,
            padding: "0 16px",
            borderRadius: radius.control,
            border: `1px solid ${locError ? colors.borderStrong : colors.border}`,
            background: colors.inputBg,
            color: colors.textPrimary,
            fontFamily: fonts.body,
            fontSize: type.value,
          }}
        />
        <div
          style={{
            fontFamily: fonts.body,
            fontSize: type.label,
            color: colors.textDim,
          }}
        >
          {/* Said in text, not only in Continue's disabled reason: that is a `title` tooltip,
              and a touchscreen has no hover. */}
          {trimmedLength === 0
            ? "Required — the report must say where the inspection was done. "
            : "Required on the report. "}
          {trimmedLength} / {INSPECTION_LOCATION_MAX} characters.
        </div>
      </div>

      {/* One banner at most: two would overrun the no-scroll height budget. With both wrong,
          the odometer banner shows and the "201 / 200" count line still carries this one. */}
      {locError && !error ? (
        <StatusBanner kind="over" title="That location is too long.">
          {locError}
        </StatusBanner>
      ) : null}
    </div>
  );
}
