"use client";

// CHANGE ROUTE — moves a Scheduled trip (and its paired leg, reversed) onto
// another catalogue route. Pick a route → GET .../change-route/preview shows
// every leg's before/after plus blockers, warnings and notices → POST
// .../change-route with acknowledgeWarnings = the "I understand" box.
// Pickup-email history is checked here (frontend-only warning): a sent email
// carries times and stops the change makes stale.

import { useEffect, useRef, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  changeTripRoute,
  corridorLabel,
  listRoutes,
  previewTripRouteChange,
  type RouteRecord,
  type TripRecord,
  type TripRouteChangeFinding,
  type TripRouteChangeLeg,
  type TripRouteChangePreview,
} from "@/lib/api/trips";
import { listTripEmailDispatches } from "@/lib/api/notifications";
import {
  latestPickupEmail,
  partnerLegLine,
  pickupEmailLabel,
  routeChangeNeedsAcknowledgement,
  routeChangeOptions,
  routeChangeSubmitEnabled,
  routeChangeSummaryChips,
  windowEndLine,
  type PickupEmailWarning,
} from "@/lib/routeChange";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { SelectField } from "@/components/ui/Field";
import { StatusBadge, StatusChip } from "@/components/ui/Chip";
import type { StatusKind } from "@/lib/theme";

export default function ChangeRouteModal({
  trip,
  onClose,
  onChanged,
}: {
  trip: TripRecord;
  onClose: () => void;
  /** Runs after the POST succeeds (refresh + follow-up); the modal closes after it. */
  onChanged: (routeId: string, routeName: string | null) => Promise<void>;
}) {
  const [routes, setRoutes] = useState<RouteRecord[] | null>(null);
  const [routesError, setRoutesError] = useState<string | null>(null);
  const [routeId, setRouteId] = useState("");
  const [preview, setPreview] = useState<TripRouteChangePreview | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);
  // Pickup-email history per trip id (this trip on mount, the partner once known).
  const [emails, setEmails] = useState<Record<string, PickupEmailWarning | null>>({});
  const [acknowledged, setAcknowledged] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const previewSeq = useRef(0);

  useEffect(() => {
    let active = true;
    listRoutes().then(
      (rows) => {
        if (active) setRoutes(rows);
      },
      (e) => {
        if (active) setRoutesError(e instanceof ApiError ? e.message : "Routes could not be loaded.");
      },
    );
    return () => {
      active = false;
    };
  }, []);

  // Best-effort: a failed history read just means no email warning.
  const emailTripIds = [trip.id, ...(preview?.partner ? [preview.partner.tripId] : [])];
  const emailKey = emailTripIds.join(",");
  useEffect(() => {
    let active = true;
    for (const id of emailKey.split(",")) {
      if (id in emails) continue;
      const num = id === trip.id ? trip.tripNumber : (preview?.partner?.tripNumber ?? "");
      listTripEmailDispatches(id).then(
        (rows) => {
          if (active) setEmails((prev) => ({ ...prev, [id]: latestPickupEmail(rows, num) }));
        },
        () => {
          if (active) setEmails((prev) => ({ ...prev, [id]: null }));
        },
      );
    }
    return () => {
      active = false;
    };
    // `emails` is read only to skip ids already fetched; re-running on it would refetch nothing.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [emailKey]);

  async function pickRoute(id: string) {
    setRouteId(id);
    setAcknowledged(false);
    setError(null);
    setPreview(null);
    setPreviewError(null);
    if (!id) return;
    const seq = ++previewSeq.current;
    setPreviewLoading(true);
    try {
      const p = await previewTripRouteChange(trip.id, id);
      if (seq === previewSeq.current) setPreview(p);
    } catch (e) {
      if (seq === previewSeq.current) {
        setPreviewError(e instanceof ApiError ? e.message : "The change could not be previewed — please try again.");
      }
    } finally {
      if (seq === previewSeq.current) setPreviewLoading(false);
    }
  }

  // Email warnings only for legs that actually move.
  const movingIds = new Set((preview?.legs ?? []).filter((l) => l.willChange).map((l) => l.tripId));
  const emailWarnings = preview
    ? emailTripIds
        .filter((id) => movingIds.has(id))
        .map((id) => emails[id])
        .filter((w): w is PickupEmailWarning => !!w)
    : [];
  const localWarnings = emailWarnings.length;
  const needsAck = routeChangeNeedsAcknowledgement(preview, localWarnings);
  const canSubmit = routeChangeSubmitEnabled({ preview, loading: previewLoading, busy, acknowledged, localWarnings });

  async function submit() {
    if (!canSubmit || !preview) return;
    setBusy(true);
    setError(null);
    try {
      await changeTripRoute(trip.id, { routeId, acknowledgeWarnings: acknowledged });
      await onChanged(routeId, preview.newRouteName);
      onClose();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : "The route could not be changed — please try again.");
      setBusy(false);
    }
  }

  const options = routeChangeOptions(routes ?? [], trip.routeId);
  const partnerLine = preview ? partnerLegLine(preview) : null;

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · ${corridorLabel(trip)}`}
      title="Change Route"
      onClose={onClose}
      error={error}
      maxWidth={640}
      footer={
        <>
          <ActionButton onClick={onClose}>KEEP ROUTE</ActionButton>
          <ActionButton variant="primary" onClick={submit} disabled={!canSubmit}>
            {busy ? "CHANGING…" : "CHANGE ROUTE"}
          </ActionButton>
        </>
      }
    >
      <div style={{ display: "flex", flexDirection: "column", gap: 14 }}>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, lineHeight: 1.55 }}>
          Currently on <strong style={{ color: colors.textPrimary }}>{trip.routeName || "a free-form corridor"}</strong>.
          The corridor, stops, distance and window end are re-copied from the new route; a paired leg moves with it,
          reversed for its direction.
        </div>

        {routesError ? (
          <StatusChip kind="over" label={`Routes unavailable — ${routesError}`} />
        ) : (
          <SelectField
            label="New route"
            value={routeId}
            onChange={(v) => void pickRoute(v)}
            disabled={routes === null || busy}
            options={[
              { value: "", label: routes === null ? "Loading routes…" : options.length ? "— pick a route —" : "No other active routes" },
              ...options,
            ]}
          />
        )}

        {previewLoading && (
          <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Checking what changes…</div>
        )}
        {previewError && <StatusChip kind="over" label={previewError} />}

        {preview && !previewLoading && (
          <>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {preview.legs.map((leg) => (
                <LegCard key={leg.tripId} leg={leg} />
              ))}
              {partnerLine && (
                <div
                  data-testid="partner-line"
                  style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textSecondary }}
                >
                  {partnerLine}
                </div>
              )}
            </div>

            <div data-testid="route-change-summary" style={{ display: "flex", flexWrap: "wrap", gap: 7 }}>
              {routeChangeSummaryChips(preview, localWarnings).map((c) => (
                <StatusChip key={c.label} kind={c.kind} label={c.label} />
              ))}
            </div>

            <FindingList kind="over" findings={preview.blockers} />
            <FindingList kind="soon" findings={preview.warnings} />
            {emailWarnings.length > 0 && (
              <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                {emailWarnings.map((w) => (
                  <div key={w.tripNumber} data-testid="email-warning">
                    <StatusChip kind="soon" label={pickupEmailLabel(w, emailWarnings.length > 1 || w.tripNumber !== trip.tripNumber)} />
                  </div>
                ))}
              </div>
            )}
            <FindingList kind="info" findings={preview.notices} />

            {needsAck && preview.blockers.length === 0 && (
              <label style={{ display: "flex", alignItems: "flex-start", gap: 11, cursor: "pointer" }}>
                <input
                  type="checkbox"
                  checked={acknowledged}
                  disabled={busy}
                  onChange={(e) => setAcknowledged(e.target.checked)}
                  style={{ accentColor: colors.blue, cursor: "pointer", marginTop: 2 }}
                />
                <span style={{ fontFamily: fonts.body, fontSize: 13, color: colors.textPrimary, lineHeight: 1.5 }}>
                  I understand — change the route anyway
                </span>
              </label>
            )}
          </>
        )}
      </div>
    </ModalShell>
  );
}

function LegCard({ leg }: { leg: TripRouteChangeLeg }) {
  const title = `${leg.tripNumber}${leg.direction ? ` · ${leg.direction}` : ""}${leg.isRequestedTrip ? "" : " · paired leg"}`;
  const current = `${leg.currentOrigin} → ${leg.currentDestination}`;
  const next =
    leg.newOrigin !== null && leg.newDestination !== null ? `${leg.newOrigin} → ${leg.newDestination}` : "—";
  return (
    <div
      data-testid="route-change-leg"
      style={{
        padding: "11px 14px",
        background: colors.cardBg,
        border: `1px solid ${colors.border}`,
        borderRadius: 10,
        display: "flex",
        flexDirection: "column",
        gap: 4,
      }}
    >
      <div style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{title}</div>
      <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary }}>
        <span>
          {current} <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{leg.currentDistanceKm} km</span>
        </span>
        {leg.willChange ? (
          <>
            <span style={{ color: colors.textDim }}>{"  ⇒  "}</span>
            <strong style={{ color: colors.textPrimary }}>{next}</strong>{" "}
            {leg.newDistanceKm !== null && (
              <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{leg.newDistanceKm} km</span>
            )}
          </>
        ) : (
          <span style={{ color: colors.textDim }}> · no change</span>
        )}
      </div>
      <div style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{windowEndLine(leg)}</div>
    </div>
  );
}

/** One row per finding: colour + glyph (StatusBadge) + the server's message. */
function FindingList({ kind, findings }: { kind: StatusKind; findings: TripRouteChangeFinding[] }) {
  if (findings.length === 0) return null;
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      {findings.map((f, i) => (
        <div key={`${f.code}-${f.tripId ?? ""}-${i}`} data-testid={`finding-${kind}`} style={{ display: "flex", gap: 9, alignItems: "flex-start" }}>
          <StatusBadge kind={kind} size={15} />
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textSecondary, lineHeight: 1.5 }}>{f.message}</span>
        </div>
      ))}
    </div>
  );
}
