"use client";

import { useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { gap, radius, touch, type } from "@/lib/tablet";
import { TouchButton } from "@/components/ui-tablet/TouchButton";
import { StatusBanner } from "@/components/ui-tablet/StatusBanner";
import { Screen, MockTag, CardRow, FieldLine, TabletChip, EmptyNote } from "./shared";
import { enqueue } from "@/lib/sync/queue";
import { assignedTrips, eligibility, formatClock, formatDurationH, openTrips } from "@/lib/data";
import type { Trip } from "@/lib/types";

// Assigned | Open, the two trip modes from architecture §5.4. Assigned trips were handed to
// this driver by dispatch; Open trips are claimable by any eligible driver.
//
// TWO THINGS ON THIS SCREEN ARE DELIBERATE DEVIATIONS, both recorded in DriverField/CLAUDE.md:
//
//  1. §5.4 says an ineligible driver should never SEE an Open trip — the filter is supposed to
//     happen server-side so a driver without Alamos clearance never even knows the trip exists.
//     This scaffold greys ineligible trips and names the failing rule instead, because a screen
//     that hides rows cannot demonstrate the engine it exists to prove. The real implementation
//     filters server-side.
//
//  2. CLAIM here changes local state and nothing else. §5.4 requires claiming to be an atomic,
//     server-validated operation with eligibility re-checked at the moment of the claim — two
//     drivers must never be able to claim the same trip. The backend has no claim endpoint and
//     no eligibility engine at all today, only a dispatcher-shaped POST /api/trips/{id}/assign
//     with no concurrency guard. So the banner below says so out loud, because this screen will
//     otherwise look authoritative in a demo.

type Tab = "assigned" | "open";

export default function Trips({ onOpenManifest }: { onOpenManifest: (tripId: string) => void }) {
  const [tab, setTab] = useState<Tab>("assigned");
  const [claimed, setClaimed] = useState<string[]>([]);

  const rows = tab === "assigned" ? assignedTrips : openTrips;

  async function claim(trip: Trip) {
    await enqueue("trip.claim", { tripId: trip.id });
    setClaimed((c) => [...c, trip.id]);
  }

  return (
    <Screen eyebrow="Shift" title="Trips" right={<MockTag />}>
      <div style={{ display: "flex", gap: 10, marginBottom: gap.section }}>
        <Tab label={`Assigned (${assignedTrips.length})`} active={tab === "assigned"} onClick={() => setTab("assigned")} />
        <Tab label={`Open (${openTrips.length})`} active={tab === "open"} onClick={() => setTab("open")} />
      </div>

      {tab === "open" && (
        <StatusBanner kind="soon" title="Claiming is not wired up.">
          Eligibility below is checked on this device only. The server has no claim endpoint and
          no eligibility engine yet, so nothing here prevents two drivers claiming the same trip.
        </StatusBanner>
      )}

      {rows.length === 0 ? (
        <EmptyNote>No trips in this list.</EmptyNote>
      ) : (
        rows.map((trip) => {
          const verdict = tab === "open" ? eligibility(trip) : null;
          const isClaimed = claimed.includes(trip.id);
          const blocked = verdict !== null && !verdict.eligible;

          return (
            <div key={trip.id} style={{ marginBottom: 10 }}>
              <CardRow muted={blocked} onClick={tab === "assigned" ? () => onOpenManifest(trip.id) : undefined}>
                <div style={{ flex: "none", width: 110 }}>
                  <span
                    style={{
                      fontFamily: fonts.mono,
                      fontSize: type.value,
                      fontVariantNumeric: "tabular-nums",
                      color: colors.headingBright,
                    }}
                  >
                    {formatClock(trip.startsAt)}
                  </span>
                  <div style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textDim }}>
                    {formatDurationH(trip.estimatedHours)}
                  </div>
                </div>

                <div style={{ flex: 1, minWidth: 0 }}>
                  <FieldLine
                    label={`${trip.tripNumber} · ${trip.serviceType}`}
                    value={`${trip.origin} → ${trip.destination} · ${trip.clientName}`}
                  />
                </div>

                <div style={{ flex: "none", display: "flex", alignItems: "center", gap: 12 }}>
                  <TabletChip kind={trip.tk} label={isClaimed ? "Claimed" : trip.status} />
                  {tab === "assigned" ? (
                    <TouchButton variant="secondary" onClick={() => onOpenManifest(trip.id)}>
                      Manifest
                    </TouchButton>
                  ) : (
                    <TouchButton
                      onClick={() => void claim(trip)}
                      disabled={blocked || isClaimed}
                      disabledReason={
                        isClaimed
                          ? "Already claimed on this device"
                          : verdict?.rules.find((r) => !r.pass)?.reason
                      }
                    >
                      {isClaimed ? "Claimed" : "Claim"}
                    </TouchButton>
                  )}
                </div>
              </CardRow>

              {verdict && <EligibilityRules verdict={verdict} />}
            </div>
          );
        })
      )}
    </Screen>
  );
}

function Tab({ label, active, onClick }: { label: string; active: boolean; onClick: () => void }) {
  return (
    <button
      onClick={onClick}
      aria-pressed={active}
      style={{
        minHeight: touch.primary,
        padding: "0 24px",
        borderRadius: radius.control,
        border: `1px solid ${active ? colors.borderActive : colors.borderStrong}`,
        background: active ? colors.cardBgActive : colors.cardBg,
        color: active ? colors.headingBright : colors.textMuted,
        fontFamily: fonts.semiCondensed,
        fontSize: 17,
        fontWeight: 600,
        letterSpacing: ".08em",
        textTransform: "uppercase",
        cursor: "pointer",
      }}
    >
      {label}
    </button>
  );
}

/**
 * The five §5.4 rules, each with its verdict and reason. Shown for every Open trip, passing or
 * not — a driver who can see why they are eligible can also see when something has quietly
 * lapsed, which is the point of encoding the rules rather than filtering silently.
 */
function EligibilityRules({ verdict }: { verdict: ReturnType<typeof eligibility> }) {
  return (
    <div
      style={{
        display: "flex",
        flexWrap: "wrap",
        gap: 8,
        padding: "10px 18px 2px",
      }}
    >
      {verdict.rules.map((rule) => {
        const m = statusMeta(rule.pass ? "ontime" : "over");
        return (
          <span
            key={rule.rule}
            title={rule.reason}
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: 7,
              padding: "6px 12px",
              borderRadius: 7,
              background: m.bg,
              border: `1px solid ${m.bd}`,
              color: m.t,
              fontFamily: fonts.body,
              fontSize: 14,
            }}
          >
            <span aria-hidden style={{ fontWeight: 800 }}>
              {m.g}
            </span>
            <strong style={{ fontWeight: 600 }}>{rule.rule}:</strong> {rule.reason}
          </span>
        );
      })}
    </div>
  );
}
