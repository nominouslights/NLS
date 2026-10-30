import { tripActivityReason, type TripActivityEntry } from "@/lib/api/trips";

// How one trip activity-log entry renders: a label line, plus optional detail,
// reason and "by" lines. Pure, so the null-handling is testable without
// mounting the Trips screen. The server may null `reason`, `detail` and
// `enteredBy` (it does for driver-role callers); every line but the label is
// then simply omitted.

// Friendly labels for the kebab-case audit event types (GET /activity). Unknown
// types fall back to their de-kebabed form.
const EVENT_LABELS: Record<string, string> = {
  "trip-scheduled": "Trip scheduled",
  // The real journal name for REASSIGN; the three below are legacy aliases.
  "trip-assignment-changed": "Assignment changed",
  "trip-assigned": "Driver / vehicle assigned",
  "trip-driver-assigned": "Driver assigned",
  "trip-vehicle-assigned": "Vehicle assigned",
  "trip-started": "Trip started",
  "trip-completed": "Trip completed",
  "trip-cancelled": "Trip cancelled",
  "trip-demand-recorded": "Demand recorded",
  "trip-manifest-linked": "Manifest linked",
  "trip-manifest-created": "Manifest created",
  "trip-manifest-updated": "Manifest updated",
  "trip-route-changed": "Route changed",
  "trip-manifest-route-renamed": "Manifest route renamed",
  "trip-converted-to-deadhead": "Converted to deadhead",
  "trip-converted-to-passenger-trip": "Converted to passenger trip",
  "trip-driver-corrected": "Driver corrected",
};

/** "Who did what" line for one audit entry (source + event). */
export function activityLabel(e: TripActivityEntry): string {
  const who =
    e.source === "App"
      ? "Driver App"
      : e.source === "Dispatcher"
        ? `Dispatcher${e.enteredBy ? ` (${e.enteredBy})` : ""}`
        : null;
  const action = EVENT_LABELS[e.eventType] ?? e.eventType.replace(/-/g, " ");
  return who ? `${who} · ${action}` : action;
}

export interface ActivityEntryView {
  isManifest: boolean;
  label: string;
  /** Server-written one-liner, trimmed; null when absent or blank. */
  detail: string | null;
  /** Actor's reason, trimmed; null when absent or blank. */
  reason: string | null;
  /** Trip events' author, trimmed; null when absent/blank, and always null for
   *  manifest events (their label already names the author). */
  by: string | null;
}

export function activityEntryView(ev: TripActivityEntry): ActivityEntryView {
  const isManifest = ev.aggregateType === "trip-manifest";
  return {
    isManifest,
    label: activityLabel(ev),
    detail: ev.detail?.trim() || null,
    reason: tripActivityReason(ev),
    by: isManifest ? null : ev.enteredBy?.trim() || null,
  };
}
