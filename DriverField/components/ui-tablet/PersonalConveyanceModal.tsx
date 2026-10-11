"use client";

import { useState } from "react";
import { colors, fonts, statusMeta, type StatusKind } from "@/lib/theme";
import { gap, radius, type } from "@/lib/tablet";
import { ModalShell } from "@/components/ui/ModalShell";
import { NumberField } from "@/components/ui/Field";
import { AttestationTile } from "@/components/ui-tablet/AttestationTile";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { kmLabel, pcDayVerdict } from "@/lib/hos/display";
import { HOS_LIMITS } from "@/lib/hos/engine";

// APP-LOCAL. The personal-conveyance sheet, in two modes.
//
//   start  the driver is about to move the bus for personal reasons: the odometer reading now
//          and FOUR attestations, every one of which must be on before Confirm lights. The
//          server requires three flags on the opening event (DutyDay.Validate —
//          PcAttestationRequired: no passengers or cargo, no trailer, no work); the tablet asks
//          them as four plain-language tiles, the owner's wording, and the fourth ("Bus is
//          empty") folds into the first flag on the wire. A sheet that let a driver confirm
//          with one tile off would be a false attestation in a compliance record.
//   end    the driver is leaving personal conveyance (any next duty tap): the odometer reading
//          now, which the closing event must carry (PcOdometerEndRequired) and which may not
//          be lower than the start (PcOdometerDecreasing) — refused here, on screen, before the
//          server would park it.
//
// Both modes show where today's kilometres stand against the 75.0 km allowance through the one
// band set (lib/hos/display.ts pcDayVerdict: gold ◐ from 60 km, vermillion ▲ from 75 km), and
// say plainly that the kilometres are readings the driver typed, not a live feed — this tablet
// has no GPS and reads no odometer.
//
// The odometer input is the COPIED NumberField at its desktop size, deliberately: a 40px field
// is under the 44px touch floor, which is exactly the signal CLAUDE.md asks to leave visible
// rather than hide behind a scale(). A ui-tablet NumberField is owed; until then the tiles and
// buttons around it are at size.

export const PC_ATTESTATIONS = [
  "No passengers, parcels or trailer",
  "Not performing any work",
  "Not a Leaf Rapids ↔ Thompson or shop move",
  "Bus is empty",
] as const;

export type PcModalMode = { mode: "start"; suggestedKm: number | null } | { mode: "end"; startKm: number | null };

function parseKm(typed: string): number | null {
  const trimmed = typed.trim();
  if (trimmed.length === 0) return null;
  const n = Number(trimmed);
  if (!Number.isFinite(n) || n < 0) return null;
  return Math.round(n * 10) / 10;
}

export function PersonalConveyanceModal({
  mode,
  todayPcDecikm,
  onClose,
  onConfirm,
}: {
  mode: PcModalMode;
  /** Today's declared personal-conveyance kilometres so far (closed segments), tenths of a km. */
  todayPcDecikm: number;
  onClose: () => void;
  /** The reading the driver confirmed, km to one decimal. */
  onConfirm: (km: number) => void;
}) {
  const ending = mode.mode === "end";
  const initial = ending ? "" : mode.suggestedKm === null ? "" : String(mode.suggestedKm);
  const [typed, setTyped] = useState(initial);
  const [attested, setAttested] = useState<boolean[]>(PC_ATTESTATIONS.map(() => false));

  const km = parseKm(typed);
  const startKm = ending ? mode.startKm : null;
  const decreasing = ending && km !== null && startKm !== null && km < startKm;
  const segmentDecikm = ending && km !== null && startKm !== null && !decreasing ? Math.round((km - startKm) * 10) : 0;
  const projected = pcDayVerdict(todayPcDecikm + segmentDecikm);
  const today = pcDayVerdict(todayPcDecikm);
  const allAttested = attested.every(Boolean);

  const blocked: string | null = ending
    ? km === null
      ? "Enter the odometer reading now."
      : decreasing
        ? `The end reading is lower than the ${kmLabel(Math.round(startKm! * 10))} that started the segment.`
        : null
    : km === null
      ? "Enter the odometer reading now."
      : !allAttested
        ? "Every attestation must be on."
        : null;

  return (
    <ModalShell
      eyebrow={ending ? "Personal conveyance · end of segment" : "Personal conveyance · s.1"}
      title={ending ? "Odometer at the end of personal conveyance" : "Start personal conveyance"}
      onClose={onClose}
      maxWidth={760}
      footer={
        <>
          <TouchButton variant="secondary" onClick={onClose}>
            Cancel
          </TouchButton>
          <TouchButton onClick={() => km !== null && onConfirm(km)} disabled={blocked !== null} disabledReason={blocked ?? undefined}>
            {ending ? "Record end of personal conveyance" : "Confirm personal conveyance"}
          </TouchButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: gap.section }}>
        <div style={{ maxWidth: 320 }}>
          <NumberField
            label={ending ? "Odometer now (km)" : "Odometer at start (km)"}
            value={typed}
            onChange={setTyped}
            min={ending && startKm !== null ? startKm : 0}
            step={0.1}
            hint={ending && startKm !== null ? `— the segment started at ${kmLabel(Math.round(startKm * 10))}` : undefined}
          />
        </div>

        {!ending ? (
          <div role="group" aria-label="Personal conveyance attestations" style={{ display: "flex", flexWrap: "wrap", gap: gap.row }}>
            {PC_ATTESTATIONS.map((label, i) => (
              <AttestationTile
                key={label}
                label={label}
                on={attested[i]}
                onToggle={() => setAttested((prev) => prev.map((v, j) => (j === i ? !v : v)))}
              />
            ))}
          </div>
        ) : null}

        <VerdictLine kind={projected.kind} glyph={statusMeta(projected.kind).g}>
          <strong style={{ fontWeight: 600 }}>PC today: {kmLabel(ending ? todayPcDecikm + segmentDecikm : today.usedDecikm)}</strong>
          {" · "}
          {projected.label}
          {ending && segmentDecikm > 0 ? ` (this segment ${kmLabel(segmentDecikm)})` : ""}
        </VerdictLine>

        {blocked !== null ? (
          <VerdictLine kind={decreasing ? "over" : "off"} glyph={statusMeta(decreasing ? "over" : "off").g}>
            {blocked}
          </VerdictLine>
        ) : null}

        <div style={{ fontFamily: fonts.body, fontSize: type.label, color: colors.textSecondary, lineHeight: 1.5 }}>
          Kilometres are the odometer readings you enter here, not a live feed. The allowance is{" "}
          {kmLabel(HOS_LIMITS.pcDailyLimitDecikm)} a day; past it the rest of the drive is on-duty time, and the server
          records that switch itself. Personal conveyance is off-duty time for your rest, and it stays on the bus&rsquo;s
          ELD as the record.
        </div>
      </div>
    </ModalShell>
  );
}

function VerdictLine({ kind, glyph, children }: { kind: StatusKind; glyph: string; children: React.ReactNode }) {
  const m = statusMeta(kind);
  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: 10,
        padding: "12px 16px",
        borderRadius: radius.control,
        background: m.bg,
        border: `1px solid ${m.bd}`,
        fontFamily: fonts.body,
        fontSize: type.label,
        color: m.t,
      }}
    >
      <span aria-hidden style={{ fontWeight: 800 }}>
        {glyph}
      </span>
      <span>{children}</span>
    </div>
  );
}
