"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  corridorLabel,
  hhmm,
  shortDateLabel,
  type ManifestPassenger,
  type TripManifest,
  type TripRecord,
} from "@/lib/api/trips";
import { SERVICE_TYPE_LABELS, listContacts, type ClientContactRecord } from "@/lib/api/clients";
import { listStops, stopAddressLine, type StopRecord } from "@/lib/api/stops";
import {
  dispatchChip,
  isEmailContact,
  listEmailTemplates,
  listTripEmailDispatches,
  previewEmailTemplate,
  previewTripPickupReport,
  sendTripPickupEmail,
  type EmailDispatchRecord,
  type EmailPreviewResult,
  type EmailRecipientResult,
  type EmailTemplateRecord,
  type PickupReportPreviewResult,
} from "@/lib/api/notifications";
import { ModalShell } from "@/components/ui/ModalShell";
import { SelectField } from "@/components/ui/Field";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { SectionLabel } from "@/components/ui/Panel";

// Send-pickup-email modal (Trips detail → Notifications module). The frontend
// composes the whole send request — trip context and per-recipient merge
// values travel as opaque snapshots because Notifications never reads Trips or
// Clients data (integration-events-only rule). The POST response is
// authoritative (200 even on partial/total provider failure) — outcomes render
// inline, no polling. dispatchId is a fresh client GUID per attempt, so a
// replayed request can never double-send.

function fmtUtcDateTime(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return iso;
  return d.toLocaleString("en-CA", {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: true,
  });
}

/** Per-recipient outcome line — colour + glyph + label, never colour alone. */
function RecipientOutcomeRow({ r }: { r: EmailRecipientResult }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
      <StatusChip kind={r.status === "Sent" ? "ontime" : "over"} label={r.status === "Sent" ? "Sent" : "Failed"} />
      <span style={{ fontFamily: fonts.body, fontSize: 12, fontWeight: 500, color: colors.textSecondary }}>
        {r.passengerName}
      </span>
      <span style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>{r.email}</span>
      {r.errorMessage && (
        <span style={{ fontFamily: fonts.body, fontSize: 11, color: statusMeta("over").t, flexBasis: "100%" }}>
          {r.errorCode ? `${r.errorCode} · ` : ""}
          {r.errorMessage}
        </span>
      )}
    </div>
  );
}

export default function SendPickupEmailModal({
  trip,
  manifest,
  onClose,
}: {
  trip: TripRecord;
  manifest: TripManifest;
  onClose: () => void;
}) {
  const passengers = manifest.passengers;
  const eligible = passengers.map((p) => isEmailContact(p.email));

  const [templates, setTemplates] = useState<EmailTemplateRecord[] | null>(null);
  const [history, setHistory] = useState<EmailDispatchRecord[] | null>(null);
  // Stops catalog keyed by id — resolves each passenger's per-recipient stop
  // addresses ({{PickupAddress}}/{{DropoffStopAddress}}). Best-effort: if the
  // list fails it stays empty and addresses render blank, never a raw token.
  const [stopMap, setStopMap] = useState<Map<string, StopRecord>>(() => new Map());
  // Client contacts flagged receivesEmailReports — the backend emails them a
  // report, but ONLY for ContractCrew trips, so this stays empty otherwise.
  // Resolved on mount (best-effort) and passed as reportRecipients on send.
  const [reportRecipients, setReportRecipients] = useState<{ name: string; email: string }[]>([]);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [templateId, setTemplateId] = useState("");
  // Eligible recipients are preselected; ineligible rows can never be checked.
  const [selected, setSelected] = useState<boolean[]>(() => passengers.map((p) => isEmailContact(p.email)));
  const [preview, setPreview] = useState<EmailPreviewResult | null>(null);
  const [previewError, setPreviewError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<string[]>([]);
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<EmailDispatchRecord | null>(null);
  // Report preview (crew-only) — what the flagged report contact would receive.
  // Populated on demand by the PREVIEW REPORT button; sends nothing.
  const [reportPreview, setReportPreview] = useState<PickupReportPreviewResult | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [reportPreviewError, setReportPreviewError] = useState<string | null>(null);

  // Active templates + this trip's dispatch history, loaded together on mount.
  // The dropdown offers only COMPATIBLE templates (same service type, and either
  // service-wide or pinned to this trip's client) — an incompatible choice now
  // 400s server-side, so it is never offered here.
  useEffect(() => {
    let active = true;
    // Report recipients only apply to ContractCrew trips with a client; for
    // anything else there is nothing to fetch. Best-effort, like stops.
    const contactsFetch: Promise<ClientContactRecord[]> =
      trip.serviceType === "ContractCrew" && trip.clientId
        ? listContacts(trip.clientId).catch(() => [] as ClientContactRecord[])
        : Promise.resolve([] as ClientContactRecord[]);
    // Stops are best-effort: a failure resolves to [] so it can never block the
    // templates/history load — addresses just render empty in that case.
    Promise.all([
      listEmailTemplates(),
      listTripEmailDispatches(trip.id),
      listStops().catch(() => [] as StopRecord[]),
      contactsFetch,
    ]).then(
      ([tpls, hist, stops, contacts]) => {
        if (!active) return;
        const compatible = tpls.filter(
          (t) => t.isActive && t.serviceType === trip.serviceType && (!t.clientId || t.clientId === trip.clientId),
        );
        setTemplates(compatible);
        setHistory(hist);
        setStopMap(new Map(stops.map((s) => [s.id, s])));
        // Contacts flagged for reports, with a real email — deduped by email.
        const seen = new Set<string>();
        const reports: { name: string; email: string }[] = [];
        for (const c of contacts) {
          if (!c.receivesEmailReports || !c.email) continue;
          const email = c.email.trim();
          if (!email || seen.has(email)) continue;
          seen.add(email);
          reports.push({ name: c.name, email });
        }
        setReportRecipients(reports);
        setLoadError(null);
        // Default pick only — the dispatcher can override in the dropdown.
        // Rule: client-pinned beats service-wide; within each group the most
        // recently updated template wins (updatedAtUtc desc).
        const newestFirst = [...compatible].sort((a, b) => b.updatedAtUtc.localeCompare(a.updatedAtUtc));
        setTemplateId((newestFirst.find((t) => t.clientId) ?? newestFirst[0])?.id ?? "");
      },
      (e) => {
        if (active) setLoadError(e instanceof ApiError ? e.message : "Failed to load templates and send history.");
      },
    );
    return () => {
      active = false;
    };
  }, [trip.id, trip.clientId, trip.serviceType]);

  const template = templates?.find((t) => t.id === templateId) ?? null;
  const firstSelectedIdx = selected.findIndex((s, i) => s && eligible[i]);
  const selectedCount = selected.reduce((n, s, i) => n + (s && eligible[i] ? 1 : 0), 0);

  // Resolve a stop id to its one-line street address. Empty string when the id
  // is null or unknown (still-loading / missing catalog entry) — a missing
  // address must render empty, never a raw {{token}}.
  const addressFor = useCallback(
    (stopId: string | null | undefined): string => {
      if (!stopId) return "";
      const s = stopMap.get(stopId);
      return s ? stopAddressLine(s) : "";
    },
    [stopMap],
  );

  /** Real merge values for one passenger — mirrors the backend MergeFields set. */
  const mergeValues = useCallback(
    (p: ManifestPassenger): Record<string, string> => ({
      PassengerName: p.name,
      TripDate: shortDateLabel(trip.serviceDate),
      PickupTime: hhmm(trip.windowStart),
      DropoffTime: trip.windowEnd ? hhmm(trip.windowEnd) : "",
      Route: trip.routeName || corridorLabel(trip),
      PickupStop: p.pickupStopName ?? trip.origin,
      PickupAddress: addressFor(p.pickupStopId),
      DropoffStop: p.dropoffStopName ?? trip.destination,
      DropoffStopAddress: addressFor(p.dropoffStopId),
      TripNumber: trip.tripNumber,
      ClientName: trip.clientName ?? "",
    }),
    [trip, addressFor],
  );

  // Live preview for the FIRST selected recipient — recomputed when the
  // template or that recipient changes.
  useEffect(() => {
    if (!template || firstSelectedIdx < 0) {
      setPreview(null);
      setPreviewError(null);
      return;
    }
    let active = true;
    previewEmailTemplate({
      subject: template.subject,
      htmlBody: template.htmlBody,
      values: mergeValues(passengers[firstSelectedIdx]),
    }).then(
      (res) => {
        if (active) {
          setPreview(res);
          setPreviewError(null);
        }
      },
      (e) => {
        if (active) {
          setPreview(null);
          setPreviewError(e instanceof ApiError ? e.message : "Preview failed.");
        }
      },
    );
    return () => {
      active = false;
    };
  }, [template, firstSelectedIdx, passengers, mergeValues]);

  function toggleRecipient(i: number) {
    if (!eligible[i]) return;
    setSelected((prev) => prev.map((s, j) => (j === i ? !s : s)));
  }

  function toggleExpanded(id: string) {
    setExpanded((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  }

  async function send() {
    if (sending) return;
    if (!template) {
      setSendError("Pick a template first.");
      return;
    }
    const recipients = passengers.filter((p, i) => selected[i] && eligible[i]);
    if (recipients.length === 0) {
      setSendError("Select at least one recipient with an email address.");
      return;
    }
    setSending(true);
    setSendError(null);
    try {
      const result = await sendTripPickupEmail({
        dispatchId: crypto.randomUUID(), // fresh GUID per attempt (idempotency key)
        templateId: template.id,
        tripId: trip.id,
        tripNumber: trip.tripNumber,
        manifestId: manifest.id,
        serviceType: trip.serviceType,
        tripDate: shortDateLabel(trip.serviceDate),
        pickupTime: hhmm(trip.windowStart),
        dropoffTime: trip.windowEnd ? hhmm(trip.windowEnd) : "",
        route: trip.routeName || corridorLabel(trip),
        clientId: trip.clientId ?? null, // validated server-side against the template's client pin
        clientName: trip.clientName,
        recipients: recipients.map((p) => ({
          email: (p.email ?? "").trim(),
          passengerName: p.name,
          pickupStop: p.pickupStopName ?? trip.origin,
          dropoffStop: p.dropoffStopName ?? trip.destination,
          pickupAddress: addressFor(p.pickupStopId),
          dropoffStopAddress: addressFor(p.dropoffStopId),
        })),
        // Pre-resolved report-recipient emails (crew-only; empty otherwise).
        // The backend only acts on these for ContractCrew trips.
        reportRecipients: reportRecipients.map((r) => r.email),
      });
      // Response is authoritative — render outcomes and prepend to history.
      setLastResult(result);
      setHistory((h) => [result, ...(h ?? []).filter((d) => d.id !== result.id)]);
      setExpanded((prev) => (prev.includes(result.id) ? prev : [...prev, result.id]));
    } catch (e) {
      setSendError(e instanceof ApiError ? e.message : "Failed to send — please try again.");
    } finally {
      setSending(false);
    }
  }

  // Build a preview of the crew-trip report — identical composition to send()
  // but WITHOUT dispatchId (nothing is sent). Reflects the CURRENT modal state.
  async function previewReport() {
    if (previewLoading) return;
    if (!template) {
      setReportPreviewError("Pick a template first.");
      return;
    }
    const recipients = passengers.filter((p, i) => selected[i] && eligible[i]);
    if (recipients.length === 0) {
      setReportPreviewError("Select at least one recipient with an email address.");
      return;
    }
    setPreviewLoading(true);
    setReportPreviewError(null);
    try {
      const result = await previewTripPickupReport({
        templateId: template.id,
        tripId: trip.id,
        tripNumber: trip.tripNumber,
        manifestId: manifest.id,
        serviceType: trip.serviceType,
        tripDate: shortDateLabel(trip.serviceDate),
        pickupTime: hhmm(trip.windowStart),
        dropoffTime: trip.windowEnd ? hhmm(trip.windowEnd) : "",
        route: trip.routeName || corridorLabel(trip),
        clientId: trip.clientId ?? null,
        clientName: trip.clientName,
        recipients: recipients.map((p) => ({
          email: (p.email ?? "").trim(),
          passengerName: p.name,
          pickupStop: p.pickupStopName ?? trip.origin,
          dropoffStop: p.dropoffStopName ?? trip.destination,
          pickupAddress: addressFor(p.pickupStopId),
          dropoffStopAddress: addressFor(p.dropoffStopId),
        })),
        reportRecipients: reportRecipients.map((r) => r.email),
      });
      setReportPreview(result);
    } catch (e) {
      setReportPreview(null);
      setReportPreviewError(e instanceof ApiError ? e.message : "Failed to render preview — please try again.");
    } finally {
      setPreviewLoading(false);
    }
  }

  const templateOptions = [
    { value: "", label: "— Select a template —" },
    ...(templates ?? []).map((t) => ({
      value: t.id,
      label: `${t.name}${t.clientName ? ` · ${t.clientName}` : ""} · ${SERVICE_TYPE_LABELS[t.serviceType]}`,
    })),
  ];

  return (
    <ModalShell
      eyebrow={`Operations · ${trip.tripNumber} · ${corridorLabel(trip)}`}
      title="Send Pickup Email"
      onClose={onClose}
      error={sendError ?? loadError}
      maxWidth={760}
      footer={
        <>
          <ActionButton onClick={onClose}>CLOSE</ActionButton>
          {trip.serviceType === "ContractCrew" && (
            <ActionButton
              variant="secondary"
              onClick={previewReport}
              disabled={previewLoading || !template || selectedCount === 0}
            >
              {previewLoading ? "RENDERING…" : "PREVIEW REPORT"}
            </ActionButton>
          )}
          <ActionButton
            variant="primary"
            onClick={send}
            disabled={sending || !template || selectedCount === 0}
          >
            {sending
              ? "SENDING…"
              : `SEND PICKUP EMAIL${selectedCount > 0 ? ` (${selectedCount})` : ""}`}
          </ActionButton>
        </>
      }
    >
      {/* template */}
      <div style={{ marginBottom: 6 }}>
        <SelectField
          label="Email template"
          value={templateId}
          onChange={setTemplateId}
          options={templateOptions}
          disabled={templates === null}
        />
      </div>
      {templates !== null && !template && (
        <div style={{ display: "flex", flexDirection: "column", gap: 6, marginBottom: 8 }}>
          <StatusChip kind="soon" label="No matching template" />
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, lineHeight: 1.5 }}>
            No active template matches this trip&rsquo;s client or service type. Pick one above, or create a
            template on the Communications screen.
          </span>
        </div>
      )}

      {/* recipients */}
      <div style={{ marginTop: 14 }}>
        <SectionLabel>Recipients — from the trip manifest</SectionLabel>
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {passengers.map((p, i) => {
            const ok = eligible[i];
            const reason = ok
              ? null
              : !p.email?.trim()
                ? "No email on manifest"
                : "Not a valid email address";
            return (
              <div
                key={`${p.name}-${i}`}
                onClick={() => toggleRecipient(i)}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 10,
                  padding: "8px 11px",
                  borderRadius: 8,
                  background: colors.cardBg,
                  border: `1px solid ${colors.borderSubtle}`,
                  opacity: ok ? 1 : 0.55,
                  cursor: ok ? "pointer" : "not-allowed",
                }}
              >
                <input
                  type="checkbox"
                  checked={ok && selected[i]}
                  disabled={!ok}
                  onChange={() => toggleRecipient(i)}
                  onClick={(e) => e.stopPropagation()}
                  style={{ accentColor: colors.blue, cursor: ok ? "pointer" : "not-allowed" }}
                />
                <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                  {p.name}
                </span>
                {ok ? (
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textMuted }}>
                    {(p.email ?? "").trim()}
                  </span>
                ) : (
                  <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, fontStyle: "italic" }}>
                    {reason}
                  </span>
                )}
              </div>
            );
          })}
        </div>

        {reportRecipients.length > 0 && (
          <div
            style={{
              marginTop: 8,
              padding: "8px 11px",
              borderRadius: 8,
              background: colors.cardBgActive,
              border: `1px solid ${colors.borderSubtle}`,
              display: "flex",
              gap: 8,
              alignItems: "flex-start",
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: colors.textSecondary,
              lineHeight: 1.5,
            }}
          >
            <span aria-hidden style={{ flex: "none" }}>📄</span>
            <span>
              A report will be emailed to:{" "}
              {reportRecipients.map((r, i) => (
                <span key={r.email}>
                  {i > 0 ? ", " : ""}
                  {r.name} <span style={{ fontFamily: fonts.mono, color: colors.textDim }}>&lt;{r.email}&gt;</span>
                </span>
              ))}
            </span>
          </div>
        )}
      </div>

      {/* live preview for the first selected recipient */}
      {template && firstSelectedIdx >= 0 && (
        <div style={{ marginTop: 16 }}>
          <SectionLabel>Preview — as {passengers[firstSelectedIdx].name} will receive it</SectionLabel>
          {previewError ? (
            <StatusChip kind="over" label={previewError} />
          ) : preview === null ? (
            <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Rendering preview…</span>
          ) : (
            <>
              <div
                style={{
                  fontFamily: fonts.body,
                  fontSize: 13,
                  fontWeight: 600,
                  color: colors.textPrimary,
                  marginBottom: 8,
                }}
              >
                {preview.subject}
              </div>
              {/* sandbox="" = full sandbox: no scripts, no navigation, no same-origin */}
              <iframe
                sandbox=""
                srcDoc={preview.htmlBody}
                title="Pickup email preview"
                style={{
                  width: "100%",
                  height: 240,
                  border: `1px solid ${colors.borderStrong}`,
                  borderRadius: 9,
                  background: "#FFFFFF",
                }}
              />
            </>
          )}
        </div>
      )}

      {/* crew-trip report preview — content only, nothing has been sent */}
      {trip.serviceType === "ContractCrew" && (reportPreview || previewLoading || reportPreviewError) && (
        <div style={{ marginTop: 16 }}>
          <SectionLabel>Report preview — what the flagged contact will receive</SectionLabel>
          <div
            style={{
              fontFamily: fonts.body,
              fontSize: 11.5,
              color: colors.textDim,
              lineHeight: 1.5,
              marginBottom: 8,
            }}
          >
            This is a content preview only — nothing has been sent. Counts and per-recipient statuses show as
            pending (&ldquo;0 sent&rdquo;, tagged &ldquo;(Preview)&rdquo;) because no email has gone out yet.
          </div>
          {reportPreviewError ? (
            <StatusChip kind="over" label={reportPreviewError} />
          ) : previewLoading || reportPreview === null ? (
            <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Rendering preview…</span>
          ) : (
            <>
              <div
                style={{
                  fontFamily: fonts.body,
                  fontSize: 13,
                  fontWeight: 600,
                  color: colors.textPrimary,
                  marginBottom: 8,
                }}
              >
                {reportPreview.subject}
              </div>
              {/* summary body — untrusted rendered HTML, fully sandboxed */}
              <iframe
                sandbox=""
                srcDoc={reportPreview.htmlBody}
                title="Report summary preview"
                style={{
                  width: "100%",
                  height: 240,
                  border: `1px solid ${colors.borderStrong}`,
                  borderRadius: 9,
                  background: "#FFFFFF",
                }}
              />
              {/* attached PDF — our own generated document, so not sandboxed */}
              <div style={{ marginTop: 10 }}>
                <SectionLabel>Attached report PDF</SectionLabel>
                <iframe
                  title="Report PDF"
                  src={`data:application/pdf;base64,${reportPreview.pdfBase64}`}
                  style={{
                    width: "100%",
                    height: 420,
                    border: `1px solid ${colors.borderStrong}`,
                    borderRadius: 9,
                    background: "#FFFFFF",
                  }}
                />
                <div style={{ marginTop: 6 }}>
                  <a
                    href={`data:application/pdf;base64,${reportPreview.pdfBase64}`}
                    target="_blank"
                    rel="noreferrer"
                    style={{
                      fontFamily: fonts.condensed,
                      fontWeight: 700,
                      fontSize: 12,
                      letterSpacing: ".03em",
                      color: colors.blue,
                      textDecoration: "none",
                    }}
                  >
                    Open PDF in new tab ↗
                  </a>
                </div>
              </div>
              {/* report recipients line */}
              <div
                style={{
                  marginTop: 10,
                  fontFamily: fonts.body,
                  fontSize: 11.5,
                  color: colors.textSecondary,
                  lineHeight: 1.5,
                }}
              >
                {reportPreview.reportRecipients.length > 0 ? (
                  <>
                    Will be emailed to:{" "}
                    <span style={{ fontFamily: fonts.mono, color: colors.textDim }}>
                      {reportPreview.reportRecipients.join(", ")}
                    </span>
                  </>
                ) : (
                  "No report contact flagged for this client — the report won't be sent (this is a content preview only)."
                )}
              </div>
            </>
          )}
        </div>
      )}

      {/* outcome of the send just performed */}
      {lastResult && (
        <div style={{ marginTop: 16 }}>
          <SectionLabel>Send result</SectionLabel>
          <div style={{ display: "flex", alignItems: "center", gap: 9, marginBottom: 8 }}>
            <StatusChip kind={dispatchChip(lastResult.status).kind} label={dispatchChip(lastResult.status).label} />
            <span style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim }}>
              {fmtUtcDateTime(lastResult.sentAtUtc)}
            </span>
          </div>
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {lastResult.recipients.map((r, i) => (
              <RecipientOutcomeRow key={`${r.email}-${i}`} r={r} />
            ))}
          </div>
        </div>
      )}

      {/* prior sends for this trip */}
      <div style={{ marginTop: 16 }}>
        <SectionLabel>Prior sends</SectionLabel>
        {history === null ? (
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            {loadError ? "Send history unavailable." : "Loading send history…"}
          </span>
        ) : history.length === 0 ? (
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            No pickup emails have been sent for this trip yet.
          </span>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            {history.map((d) => {
              const chip = dispatchChip(d.status);
              const open = expanded.includes(d.id);
              return (
                <div
                  key={d.id}
                  style={{
                    padding: "8px 11px",
                    borderRadius: 8,
                    background: colors.cardBg,
                    border: `1px solid ${colors.borderSubtle}`,
                  }}
                >
                  <div
                    onClick={() => toggleExpanded(d.id)}
                    style={{ display: "flex", alignItems: "center", gap: 9, cursor: "pointer" }}
                  >
                    <span style={{ fontFamily: fonts.mono, fontSize: 10.5, color: colors.textDim, flex: "none" }}>
                      {fmtUtcDateTime(d.sentAtUtc)}
                    </span>
                    <span
                      style={{
                        fontFamily: fonts.body,
                        fontSize: 12,
                        fontWeight: 500,
                        color: colors.textSecondary,
                        flex: 1,
                        minWidth: 0,
                        whiteSpace: "nowrap",
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                      }}
                    >
                      {d.templateName}
                    </span>
                    <StatusChip kind={chip.kind} label={chip.label} />
                    <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
                      {open ? "▾" : "▸"}
                    </span>
                  </div>
                  {open && (
                    <div
                      style={{
                        marginTop: 8,
                        paddingTop: 8,
                        borderTop: `1px solid ${colors.borderSubtle}`,
                        display: "flex",
                        flexDirection: "column",
                        gap: 6,
                      }}
                    >
                      {d.recipients.map((r, i) => (
                        <RecipientOutcomeRow key={`${r.email}-${i}`} r={r} />
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </ModalShell>
  );
}
