// ---------------------------------------------------------------------------
// The boarding gate: may this driver start moving crew on this trip?
//
// NSC Standard 11 says you may not operate before inspecting, so the owner chose a HARD BLOCK:
// Board and No-show are disabled until a pre-trip is certified for the trip's vehicle today.
//
// THE SERVER MUST OWN THIS. No backend endpoint requires a certified pre-trip before a manifest
// write — there is no such precondition anywhere in Backend/. This is a UX gate on one device,
// it says so on screen in two places, and it is never the enforcement point. Same class of
// client-side mirror as eligibility() in lib/data.ts, and the same honesty obligation.
//
// DELIBERATELY NOT A SIXTH eligibility() RULE, for four reasons:
//
//   1. Different question. §5.4 asks "may this driver CLAIM this Open trip?"; this asks "may
//      they START MOVING CREW on a trip they already hold?" A driver with no pre-trip done at
//      21:00 is perfectly eligible to claim tomorrow's 06:30 run — a sixth rule would wrongly
//      block that.
//   2. It would destroy the demonstration of the other five. No pre-trip is certified today in
//      the mock layer, so a sixth rule would grey out EVERY Open trip and the Open tab would
//      stop demonstrating rules 1–5.
//   3. lib/eligibility.test.ts is valuable because it is hard to edit ("a rule silently
//      disappearing is the failure mode that matters"). Adding a sixth teaches the next author
//      that the list is negotiable. That file is unchanged, byte for byte, on purpose.
//   4. Different server homes — eligibility belongs to a trip-claim command; this precondition
//      belongs with the manifest write.
//
// DELIBERATELY DOES NOT READ lib/sync/queue.ts's pending(). queue.ts states the rule: "screens
// read their data from lib/data.ts, never from the queue." Reading the write path to answer a
// read question would break the invariant that keeps the offline batch additive. The gate reads
// lib/inspectionStore.ts (what this device certified, persisted) and lib/data.ts instead.
// ---------------------------------------------------------------------------

import { assignedVehicleId, dvirSubmissions, today, vehicles } from "./data";
import { certifiedToday, hasDraft } from "./inspectionStore";
import { statusMeta, type StatusKind } from "./theme";
import type { CheckState, DefectSeverity, InspectionMode } from "./types";

/** Mirrors InspectionResult — Backend/src/Fleet/Domain/Inspections/InspectionResult.cs. */
export type InspectionResultName = "Pass" | "PassWithDefects" | "Fail";

/** Mirrors InspectionDefectSeverity's MEMBER NAMES — no spaces. Never the display strings. */
export type DefectSeverityWire = "Minor" | "Major" | "OutOfService";

/**
 * Mirrors ChecklistItemState — Backend/src/Fleet/Domain/Inspections/ChecklistItemState.cs.
 *
 * NL-PTI-01's three boxes, as the wire spells them. Before this enum existed there was only
 * `ChecklistItemInput.Passed`, a bool, and this app OMITTED every N/A row from the submission
 * rather than send `passed: true` for a row that does not apply — a false attestation in a
 * compliance record. The tri-state landed, so N/A is carried now and nothing is dropped.
 */
export type ChecklistItemStateWire = "Ok" | "Defect" | "NotApplicable";

/**
 * Mirrors InspectionSource — Backend/src/Fleet/Domain/Inspections/InspectionSource.cs.
 *
 * NEVER OMITTED from a submission. EnterInspectionCommand takes Source as a required value and
 * the office path supplies `Dispatcher`; a driver submission that leaves it to a default is
 * attributed to the wrong place in the compliance record. Same failure the HOS source pin in
 * lib/wire.test.ts exists for. Pinned there too.
 */
export const INSPECTION_SOURCE_WIRE = "DriverApp" as const;

/**
 * Mirrors VehicleInspection.DeriveResult —
 * Backend/src/Fleet/Domain/Inspections/VehicleInspection.cs:421-432.
 *
 * No defects → Pass; only Minor defects → PassWithDefects; ANY Major or OutOfService → Fail.
 * Note the rule is stricter than intuition: a single Major fails the inspection outright, it
 * does not merely downgrade it. Pinned on both sides of that boundary in
 * lib/inspectionGate.test.ts.
 *
 * Runs client-side for the banner, the local certification and this gate ONLY. `result` is NOT
 * in the submit payload — the server derives it.
 */
export function deriveResult(severities: DefectSeverity[]): InspectionResultName {
  if (severities.length === 0) return "Pass";
  return severities.some((s) => s === "Major" || s === "Out of Service")
    ? "Fail"
    : "PassWithDefects";
}

/**
 * Display string → wire value. `"Out of Service"` → `"OutOfService"`.
 *
 * The default System.Text.Json enum converter would reject the spaced display string, so this
 * is the only legal way across the boundary. Pinned in lib/wire.test.ts.
 */
export function severityToWire(severity: DefectSeverity): DefectSeverityWire {
  switch (severity) {
    case "Minor":
      return "Minor";
    case "Major":
      return "Major";
    case "Out of Service":
      return "OutOfService";
  }
}

/**
 * The screen's tri-state → the wire's. The screen's values are lowercase because they key a
 * draft; the wire's are C# member names. Pinned in lib/wire.test.ts.
 */
export function checkStateToWire(state: CheckState): ChecklistItemStateWire {
  switch (state) {
    case "pass":
      return "Ok";
    case "defect":
      return "Defect";
    case "na":
      return "NotApplicable";
  }
}

/**
 * Mirrors VehicleInspection.NormalizeChecklist —
 * Backend/src/Fleet/Domain/Inspections/VehicleInspection.cs:510, which re-derives
 * `Passed = state != ChecklistItemState.Defect` for any row that supplies a state.
 *
 * `ChecklistItemInput.Passed` is still a non-nullable `bool`, so it is sent alongside `state`;
 * deriving it the same way the aggregate does is what stops a caller sending the two out of
 * step. NOTE THE DIRECTION: an N/A row is `passed: true`, which is only safe BECAUSE `state`
 * travels with it — the bool alone would be the false attestation this app used to avoid by
 * omitting the row entirely.
 */
export function checkStatePassed(state: CheckState): boolean {
  return checkStateToWire(state) !== "Defect";
}

/**
 * Mirrors Vehicle.RecordOdometer's monotonic guard —
 * Backend/src/Fleet/Domain/Vehicles/Vehicle.cs:198-213, which fails with
 * VehicleErrors.OdometerRollback ("An odometer reading cannot be lower than the current
 * reading"). PropagateInspectionOdometerCommandHandler feeds an inspection's reading straight
 * into it, so a rolled-back reading on a DVIR is rejected server-side — better to say so on the
 * step than to let a driver certify 22 answers against a number that will bounce.
 *
 * EQUAL IS ALLOWED (the guard is `<`, not `<=`): a vehicle that has not moved since the last
 * reading is the normal case for a pre-trip right after a post-trip. Returns a
 * driver-readable reason, or null when the value is acceptable.
 */
export function odometerError(entered: number | null, lastReadingKm: number): string | null {
  if (entered === null) return "Enter the odometer reading before continuing.";
  if (!Number.isFinite(entered) || !Number.isInteger(entered)) {
    return "Enter the odometer reading in whole kilometres.";
  }
  if (entered < 0) return "An odometer reading cannot be negative.";
  if (entered < lastReadingKm) {
    return (
      `An odometer reading cannot be lower than the last recorded ` +
      `${lastReadingKm.toLocaleString("en-CA")} km. Check the reading, and report a fault if ` +
      `the odometer is wrong.`
    );
  }
  return null;
}

/**
 * The status colour for a severity. Major and Out of Service BOTH map to `over` (vermillion) —
 * deliberately, because lib/theme.ts is a protected copy and inventing a fifth StatusKind or a
 * new hex is not an option. They are told apart by their glyph instead; see severityGlyph.
 */
export function severityKind(severity: DefectSeverity): StatusKind {
  return severity === "Minor" ? "soon" : "over";
}

/**
 * The glyph for a severity, and the reason `StatusChip`/`TabletChip`/`AnswerButton` all carry a
 * `glyph` override.
 *
 * Major and Out of Service share a colour, so if they also shared the kind's default `▲` the
 * text label would be the only thing separating two different states — a breach of the
 * colour + glyph + label rule, and invisible in grayscale at a glance. Out of Service supplies
 * its own `✕`; everything else takes the kind's default, derived rather than duplicated so it
 * cannot drift from theme.ts.
 */
export function severityGlyph(severity: DefectSeverity): string {
  return severity === "Out of Service" ? "✕" : statusMeta(severityKind(severity)).g;
}

export interface InspectionGateVerdict {
  open: boolean;
  /** Banner title. Non-empty in both directions. */
  title: string;
  /** Driver-readable body, in BOTH directions — EligibilityRule.reason's convention. */
  reason: string;
  /** Short enough for a disabled button's `title` attribute. */
  shortReason: string;
  /** The mode a CTA should start, or null when re-inspecting is not the remedy. */
  requires: InspectionMode | null;
  /** A draft exists, so the CTA reads "Resume" rather than "Start". */
  resumable: boolean;
}

/** What we know about the most recent pre-trip for a vehicle today, whatever its source. */
interface PreTripRecord {
  result: InspectionResultName;
  /** HH:MM, for the driver. */
  at: string;
  outOfService: boolean;
  /** True when this device certified it, false when it came from the mock history. */
  local: boolean;
}

/**
 * Two local sources, in this order:
 *
 *   1. What THIS DEVICE certified (lib/inspectionStore.ts). Persisted, so a driver who reloads
 *      after certifying is not re-blocked.
 *   2. The mock submission history (lib/data.ts), matched on `mode` and `vehicleId` — never on
 *      `type` (display prose) or `unit` (free text).
 */
function preTripToday(vehicleId: string, asOf: string): PreTripRecord | null {
  const local = certifiedToday("PreTrip", vehicleId, asOf);
  if (local) {
    return {
      result: local.result,
      at: clockOf(local.certifiedAt),
      outOfService: local.outOfService,
      local: true,
    };
  }

  const row = dvirSubmissions.find(
    (s) => s.mode === "PreTrip" && s.vehicleId === vehicleId && s.performedAt.slice(0, 10) === asOf,
  );
  if (!row) return null;

  return {
    result: resultFromProse(row.result),
    at: clockOf(row.performedAt),
    // The mock rows carry no severity breakdown, so this cannot be claimed either way.
    outOfService: false,
    local: false,
  };
}

/** The mock layer stores `result` as prose. Nothing branches on prose except here. */
function resultFromProse(result: string): InspectionResultName {
  const normalized = result.toLowerCase();
  if (normalized.startsWith("fail")) return "Fail";
  if (normalized.includes("defect")) return "PassWithDefects";
  return "Pass";
}

function clockOf(iso: string): string {
  const at = iso.indexOf("T");
  return at === -1 ? iso : iso.slice(at + 1, at + 6);
}

function unitOf(vehicleId: string): string {
  return vehicles.find((v) => v.id === vehicleId)?.unit ?? vehicleId;
}

/**
 * May passengers be marked boarded or no-show on a trip using this vehicle?
 *
 * Argument order and defaults mirror eligibility(trip, asOf = today).
 */
export function boardingGate(
  vehicleId: string = assignedVehicleId,
  asOf: string = today,
): InspectionGateVerdict {
  const unit = unitOf(vehicleId);
  const record = preTripToday(vehicleId, asOf);

  if (!record) {
    return {
      open: false,
      title: "Pre-trip inspection required before boarding.",
      reason:
        `No pre-trip inspection is certified for ${unit} on ${asOf}. Board and No-show are ` +
        "disabled until one is. This is a check on this device only — the server does not " +
        "enforce it, so nothing here prevents boarding from another device or from the " +
        "Dispatch Console.",
      shortReason: `Pre-trip inspection not certified for ${unit} today.`,
      requires: "PreTrip",
      resumable: hasDraft("PreTrip", vehicleId, asOf),
    };
  }

  if (record.result === "Fail") {
    // No CTA: another pre-trip is not the remedy, and offering one would invite a driver to
    // re-inspect their way past a failure.
    return {
      open: false,
      title: `${unit} failed its pre-trip inspection.`,
      reason:
        `${record.outOfService ? "An out-of-service defect" : "A failing defect"} was ` +
        `recorded at ${record.at}. Boarding stays disabled until maintenance clears it — call ` +
        `dispatch. ${record.local ? "Recorded on this device only; the server does not enforce this." : "The server does not enforce this."}`,
      shortReason: `${unit} failed its pre-trip inspection today.`,
      requires: null,
      resumable: false,
    };
  }

  return {
    open: true,
    title: `Pre-trip certified for ${unit}.`,
    reason:
      `${unit} passed its pre-trip inspection at ${record.at}` +
      `${record.result === "PassWithDefects" ? " with minor defects recorded" : ""}. ` +
      "Boarding is allowed on this device; the server does not check this either way.",
    shortReason: `Pre-trip certified for ${unit} today.`,
    requires: null,
    resumable: false,
  };
}

/**
 * What the one rail entry should say, and whether it is gating work.
 *
 * DELIBERATELY NOT MODELLED: trip lifecycle ("a post-trip is due once the trip completes").
 * This app does not own trip status and `activeTrip.status` is a mock string, so "no post-trip
 * certified today" is enough and invents no rule the backend has not stated.
 */
export function inspectionDue(
  vehicleId: string = assignedVehicleId,
  asOf: string = today,
): {
  mode: InspectionMode;
  label: string;
  badge: string | null;
  badgeKind: StatusKind;
  gating: boolean;
} {
  if (!preTripToday(vehicleId, asOf)) {
    const open = hasDraft("PreTrip", vehicleId, asOf);
    return {
      mode: "PreTrip",
      label: "Pre-Trip",
      badge: open ? "OPEN" : "DUE",
      // Vermillion for a gating pre-trip; gold once one is actually in progress.
      badgeKind: open ? "soon" : "over",
      gating: true,
    };
  }

  const postTrip =
    certifiedToday("PostTrip", vehicleId, asOf) !== null ||
    dvirSubmissions.some(
      (s) =>
        s.mode === "PostTrip" && s.vehicleId === vehicleId && s.performedAt.slice(0, 10) === asOf,
    );

  if (!postTrip) {
    // Gold, not vermillion: a post-trip owed at end of shift does not block work, and
    // vermillion here would over-signal and dull the badge that does.
    return {
      mode: "PostTrip",
      label: "Post-Trip",
      badge: hasDraft("PostTrip", vehicleId, asOf) ? "OPEN" : "DUE",
      badgeKind: "soon",
      gating: false,
    };
  }

  return { mode: "PostTrip", label: "Inspection", badge: null, badgeKind: "off", gating: false };
}
