// Pure display/enablement helpers for the CHANGE ROUTE dialog
// (components/ChangeRouteModal.tsx). Kept out of the component so the rules —
// which chips show, when the submit is live — are unit-tested on their own.
//
// Status never stands alone: every chip here is rendered by StatusChip, which
// pairs the protected colour with a glyph AND the text label below.

import type { StatusKind } from "./theme";
import type { EmailDispatchRecord } from "./api/notifications";
import { hhmm, type RouteRecord, type TripRouteChangeLeg, type TripRouteChangePreview } from "./api/trips";
import { formatUtcDate } from "./api/format";

export interface RouteChangeChip {
  kind: StatusKind;
  label: string;
}

/** A pickup email already sent for a leg that is about to move — frontend-only
 *  warning (the server does not know about notification history). */
export interface PickupEmailWarning {
  tripNumber: string;
  sentAtUtc: string;
}

function plural(n: number, noun: string): string {
  return n === 1 ? `1 ${noun}` : `${n} ${noun}s`;
}

/** Route picker options: active catalogue routes only, the trip's current route
 *  excluded, labelled "Name · origin → destination · 320 km". */
export function routeChangeOptions(
  routes: RouteRecord[],
  currentRouteId: string | null,
): { value: string; label: string }[] {
  return routes
    .filter((r) => r.active && r.id !== currentRouteId)
    .sort((a, b) => a.name.localeCompare(b.name))
    .map((r) => ({ value: r.id, label: `${r.name} · ${r.origin} → ${r.destination} · ${r.distanceKm} km` }));
}

/** The newest pickup email that actually reached someone (Sent or
 *  PartiallyFailed) among a trip's dispatches, or null. Trip-anchored dispatches
 *  are pickup sends — accruals and booking-pass sends carry no tripId. */
export function latestPickupEmail(dispatches: EmailDispatchRecord[], tripNumber: string): PickupEmailWarning | null {
  let latest: EmailDispatchRecord | null = null;
  for (const d of dispatches) {
    if (d.tripId === null || d.status === "Failed") continue;
    if (latest === null || d.sentAtUtc > latest.sentAtUtc) latest = d;
  }
  return latest ? { tripNumber: latest.tripNumber ?? tripNumber, sentAtUtc: latest.sentAtUtc } : null;
}

/** Chip text for a pickup-email warning. */
export function pickupEmailLabel(w: PickupEmailWarning, includeTrip: boolean): string {
  return `Pickup email sent ${formatUtcDate(w.sentAtUtc)}${includeTrip ? ` for ${w.tripNumber}` : ""} — times and stops will be out of date`;
}

/** Summary chips for a preview: blockers (over), warnings (soon, server +
 *  frontend-only email warnings), notices (info); "No conflicts" (ontime) when
 *  there is nothing at all. Counts are in the text. */
export function routeChangeSummaryChips(preview: TripRouteChangePreview, localWarnings: number): RouteChangeChip[] {
  const chips: RouteChangeChip[] = [];
  const warnings = preview.warnings.length + localWarnings;
  if (preview.blockers.length > 0) chips.push({ kind: "over", label: plural(preview.blockers.length, "blocker") });
  if (warnings > 0) chips.push({ kind: "soon", label: plural(warnings, "warning") });
  if (preview.notices.length > 0) chips.push({ kind: "info", label: plural(preview.notices.length, "notice") });
  if (chips.length === 0) chips.push({ kind: "ontime", label: "No conflicts" });
  return chips;
}

/** True when the dispatcher must tick "I understand" before submitting. */
export function routeChangeNeedsAcknowledgement(preview: TripRouteChangePreview | null, localWarnings: number): boolean {
  if (!preview) return false;
  return preview.requiresAcknowledgement || preview.warnings.length > 0 || localWarnings > 0;
}

/** Submit enablement. CHANGE ROUTE is live only with a loaded preview, no
 *  blockers, and the acknowledgement ticked when one is needed. */
export function routeChangeSubmitEnabled(args: {
  preview: TripRouteChangePreview | null;
  loading: boolean;
  busy: boolean;
  acknowledged: boolean;
  localWarnings: number;
}): boolean {
  const { preview, loading, busy, acknowledged, localWarnings } = args;
  if (!preview || loading || busy) return false;
  if (preview.blockers.length > 0 || !preview.canChange) return false;
  if (routeChangeNeedsAcknowledgement(preview, localWarnings) && !acknowledged) return false;
  return true;
}

/** "Return leg TR-0042 changes too (reversed)" / "… is already on this route",
 *  or null for an unpaired trip. */
export function partnerLegLine(preview: TripRouteChangePreview): string | null {
  const partner = preview.partner;
  if (!partner) return null;
  const leg = preview.legs.find((l) => l.tripId === partner.tripId);
  const which = partner.direction === "Outbound" ? "Outbound leg" : "Return leg";
  if (leg && !leg.willChange) return `${which} ${partner.tripNumber} is already on this route`;
  return `${which} ${partner.tripNumber} changes too (reversed)`;
}

/** "Ends 9:55 AM → 10:20 AM", "Ends 9:55 AM (unchanged)", or "Open-ended window". */
export function windowEndLine(leg: TripRouteChangeLeg): string {
  if (leg.currentWindowEnd === null && leg.newWindowEnd === null) return "Open-ended window";
  if (!leg.willChange || leg.currentWindowEnd === leg.newWindowEnd) return `Ends ${hhmm(leg.currentWindowEnd)} (unchanged)`;
  return `Ends ${hhmm(leg.currentWindowEnd)} → ${hhmm(leg.newWindowEnd)}`;
}
