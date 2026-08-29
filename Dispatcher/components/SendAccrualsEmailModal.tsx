"use client";

import { useEffect, useMemo, useState } from "react";
import { colors, fonts, statusMeta } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { listContacts, type ClientContactRecord } from "@/lib/api/clients";
import {
  dispatchChip,
  listClientEmailDispatches,
  previewClientAccrualsEmail,
  sendClientAccrualsEmail,
  type ClientAccrualsPreviewResult,
  type EmailDispatchRecord,
  type EmailRecipientResult,
  type NotificationServiceType,
} from "@/lib/api/notifications";
import { accrualsEmailPayload, type AccrualsReport } from "@/lib/billing/accruals";
import { getClaims } from "@/lib/auth";
import { periodLabel } from "@/lib/period";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { StatusChip } from "@/components/ui/Chip";
import { SectionLabel } from "@/components/ui/Panel";

// Send-accruals-email modal (Reports screen → Notifications module), modeled
// on SendPickupEmailModal. The frontend composes the WHOLE send request — the
// report travels as pre-formatted strings (accrualsEmailPayload, the same
// derivation the screen and printed sheet render) because Notifications never
// reads Trips, Billing, or Clients data (integration-events-only rule).
// Recipients are the client's contacts flagged "Receives accruals reports",
// plus an optional copy to the signed-in dispatcher (default on) so they can
// refer back to exactly what the client received.
// The POST response is authoritative (200 even on partial/total provider
// failure) — outcomes render inline, no polling. Unlike the pickup modal's
// fresh-GUID-per-attempt, dispatchId here is ONE GUID held for the modal's
// lifetime: a retry after a network failure replays idempotently instead of
// double-sending the same report.

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

/** Per-recipient outcome line — colour + glyph + label, never colour alone.
 *  On accruals dispatches, passengerName carries the CONTACT's display name. */
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

export default function SendAccrualsEmailModal({
  report,
  onClose,
}: {
  report: AccrualsReport;
  onClose: () => void;
}) {
  // ONE idempotency GUID for the modal's lifetime — see the header comment.
  const [dispatchId] = useState(() => crypto.randomUUID());

  // The client's service category anchors the recorded dispatch. A client with
  // no category recorded still needs a valid enum name — Community is the
  // neutral default, mirroring svcForServiceType's fallback in lib/api/clients.
  const serviceType: NotificationServiceType = report.client.serviceType ?? "Community";

  // The wire report — derived ONCE from the same AccrualsReport the screen
  // renders, so the emailed PDF can never disagree with what's on screen.
  const payload = useMemo(() => accrualsEmailPayload(report), [report]);

  // Contacts flagged receivesAccrualsReports with a usable address, deduped by
  // trimmed email (case-insensitive, matching the backend's dedupe). null =
  // still loading; [] after a fetch failure (best-effort — the empty state
  // then points at the roster rather than blocking the modal).
  const [recipients, setRecipients] = useState<{ name: string; email: string }[] | null>(null);
  const [selected, setSelected] = useState<boolean[]>([]);
  // Prior accruals sends for this client (dispatches with no trip reference).
  const [history, setHistory] = useState<EmailDispatchRecord[] | null>(null);
  const [expanded, setExpanded] = useState<string[]>([]);

  useEffect(() => {
    let active = true;
    Promise.all([
      listContacts(report.client.id).catch(() => [] as ClientContactRecord[]),
      listClientEmailDispatches(report.client.id).catch(() => [] as EmailDispatchRecord[]),
    ]).then(([contacts, dispatches]) => {
      if (!active) return;
      const seen = new Set<string>();
      const flagged: { name: string; email: string }[] = [];
      for (const c of contacts) {
        if (!c.receivesAccrualsReports || !c.email) continue;
        const email = c.email.trim();
        if (!email || seen.has(email.toLowerCase())) continue;
        seen.add(email.toLowerCase());
        flagged.push({ name: c.name, email });
      }
      setRecipients(flagged);
      setSelected(flagged.map(() => true)); // everyone flagged is preselected
      // The clientId filter also returns pickup dispatches that carried this
      // client — keep only accruals sends (no trip reference, by contract).
      setHistory(dispatches.filter((d) => d.tripId === null));
    });
    return () => {
      active = false;
    };
  }, [report.client.id]);

  const selectedRecipients = (recipients ?? []).filter((_, i) => selected[i]);

  // "Send me a copy" — the signed-in dispatcher's address from the access
  // token (unverified decode, a UX affordance; see lib/claims.ts). Captured
  // once at mount like dispatchId, and null only if the token is unreadable.
  const [myEmail] = useState(() => getClaims()?.email || null);
  const [copyToSelf, setCopyToSelf] = useState(true);

  // What actually goes out: the selected contacts, plus the self-copy appended
  // LAST — if the dispatcher is also a selected contact the entry is skipped
  // here (and the backend's case-insensitive dedupe backstops it), so the
  // count on the SEND button never overstates the batch.
  const selfAlreadySelected =
    myEmail !== null && selectedRecipients.some((r) => r.email.toLowerCase() === myEmail.toLowerCase());
  const outgoingRecipients =
    copyToSelf && myEmail !== null && !selfAlreadySelected
      ? [...selectedRecipients, { name: "Dispatcher copy", email: myEmail }]
      : selectedRecipients;

  // Preview — on demand, composed from the CURRENT selection with the exact
  // send-time composer server-side (nothing is sent, no dispatch is created).
  const [preview, setPreview] = useState<ClientAccrualsPreviewResult | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);

  async function loadPreview() {
    if (previewLoading) return;
    if (selectedRecipients.length === 0) {
      setPreviewError("Select at least one recipient.");
      return;
    }
    setPreviewLoading(true);
    setPreviewError(null);
    try {
      const result = await previewClientAccrualsEmail({
        clientId: report.client.id,
        clientName: report.client.name,
        serviceType,
        report: payload,
        recipients: outgoingRecipients.map((r) => ({ email: r.email, contactName: r.name })),
      });
      setPreview(result);
    } catch (e) {
      setPreview(null);
      setPreviewError(e instanceof ApiError ? e.message : "Failed to render preview — please try again.");
    } finally {
      setPreviewLoading(false);
    }
  }

  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<EmailDispatchRecord | null>(null);

  async function send() {
    if (sending || lastResult) return;
    if (selectedRecipients.length === 0) {
      setSendError("Select at least one recipient.");
      return;
    }
    setSending(true);
    setSendError(null);
    try {
      const result = await sendClientAccrualsEmail({
        dispatchId, // stable for the modal — a retry replays, never re-sends
        clientId: report.client.id,
        clientName: report.client.name,
        serviceType,
        report: payload,
        recipients: outgoingRecipients.map((r) => ({ email: r.email, contactName: r.name })),
      });
      // Response is authoritative — render outcomes and prepend to history.
      // The send stays done after this: replaying the stable dispatchId would
      // only return this same stored dispatch, so the button locks to SENT.
      setLastResult(result);
      setHistory((h) => [result, ...(h ?? []).filter((d) => d.id !== result.id)]);
    } catch (e) {
      setSendError(e instanceof ApiError ? e.message : "Failed to send — retry replays safely (same dispatch id).");
    } finally {
      setSending(false);
    }
  }

  function toggleRecipient(i: number) {
    if (lastResult) return; // selection is frozen once the send landed
    setSelected((prev) => prev.map((s, j) => (j === i ? !s : s)));
  }

  function toggleExpanded(id: string) {
    setExpanded((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  }

  return (
    <ModalShell
      eyebrow={`Business · ${report.client.name} · ${periodLabel(report.period)}`}
      title="Email Accruals Report"
      onClose={onClose}
      error={sendError}
      maxWidth={760}
      footer={
        <>
          <ActionButton onClick={onClose}>CLOSE</ActionButton>
          <ActionButton
            variant="secondary"
            onClick={loadPreview}
            disabled={previewLoading || selectedRecipients.length === 0}
          >
            {previewLoading ? "RENDERING…" : "PREVIEW EMAIL"}
          </ActionButton>
          <ActionButton
            variant="primary"
            onClick={send}
            disabled={sending || selectedRecipients.length === 0 || lastResult !== null}
          >
            {lastResult
              ? "SENT ✓"
              : sending
                ? "SENDING…"
                : `SEND ACCRUALS EMAIL${outgoingRecipients.length > 0 ? ` (${outgoingRecipients.length})` : ""}`}
          </ActionButton>
        </>
      }
    >
      {/* recipients — the client's flagged contacts */}
      <SectionLabel>Recipients — contacts flagged &ldquo;Receives accruals reports&rdquo;</SectionLabel>
      {recipients === null ? (
        <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Loading contacts…</span>
      ) : recipients.length === 0 ? (
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <StatusChip kind="soon" label="No accruals contact flagged" />
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, lineHeight: 1.5 }}>
            No contact on {report.client.name}&rsquo;s roster is flagged to receive accruals reports (or the
            flagged contact has no email address). On the Clients screen, open this client&rsquo;s contact
            roster, EDIT a contact, and turn on &ldquo;Receives accruals reports&rdquo; — then reopen this
            dialog.
          </span>
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          {recipients.map((r, i) => (
            <div
              key={r.email}
              onClick={() => toggleRecipient(i)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 10,
                padding: "8px 11px",
                borderRadius: 8,
                background: colors.cardBg,
                border: `1px solid ${colors.borderSubtle}`,
                cursor: lastResult ? "default" : "pointer",
              }}
            >
              <input
                type="checkbox"
                checked={selected[i] ?? false}
                disabled={lastResult !== null}
                onChange={() => toggleRecipient(i)}
                onClick={(e) => e.stopPropagation()}
                style={{ accentColor: colors.blue, cursor: lastResult ? "default" : "pointer" }}
              />
              <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
                {r.name}
              </span>
              <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textMuted }}>{r.email}</span>
            </div>
          ))}
        </div>
      )}

      {/* self-copy — the signed-in dispatcher rides along on the client send.
          Dashed border sets it apart from the roster rows above; it is not a
          client contact, and on its own it never enables SEND. */}
      {myEmail !== null && (
        <div
          onClick={() => !lastResult && setCopyToSelf((v) => !v)}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            padding: "8px 11px",
            marginTop: 8,
            borderRadius: 8,
            background: colors.cardBg,
            border: `1px dashed ${colors.borderStrong}`,
            cursor: lastResult ? "default" : "pointer",
          }}
        >
          <input
            type="checkbox"
            checked={copyToSelf}
            disabled={lastResult !== null}
            onChange={() => setCopyToSelf((v) => !v)}
            onClick={(e) => e.stopPropagation()}
            style={{ accentColor: colors.blue, cursor: lastResult ? "default" : "pointer" }}
          />
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
            Send me a copy
          </span>
          <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textMuted }}>{myEmail}</span>
          {selfAlreadySelected && (
            <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
              already a selected contact — sent once
            </span>
          )}
        </div>
      )}

      {/* preview — identical composition to a send, nothing gets dispatched */}
      {(preview || previewLoading || previewError) && (
        <div style={{ marginTop: 16 }}>
          <SectionLabel>Preview — as the contacts will receive it</SectionLabel>
          {previewError ? (
            <StatusChip kind="over" label={previewError} />
          ) : previewLoading || preview === null ? (
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
              {/* covering email body — rendered HTML, fully sandboxed */}
              <iframe
                sandbox=""
                srcDoc={preview.htmlBody}
                title="Accruals email preview"
                style={{
                  width: "100%",
                  height: 160,
                  border: `1px solid ${colors.borderStrong}`,
                  borderRadius: 9,
                  background: "#FFFFFF",
                }}
              />
              {/* attached PDF — our own generated document, so not sandboxed */}
              <div style={{ marginTop: 10 }}>
                <SectionLabel>Attached report PDF</SectionLabel>
                <iframe
                  title="Accruals report PDF"
                  src={`data:application/pdf;base64,${preview.pdfBase64}`}
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
                    href={`data:application/pdf;base64,${preview.pdfBase64}`}
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
              <div
                style={{
                  marginTop: 10,
                  fontFamily: fonts.body,
                  fontSize: 11.5,
                  color: colors.textSecondary,
                  lineHeight: 1.5,
                }}
              >
                Will be emailed to:{" "}
                <span style={{ fontFamily: fonts.mono, color: colors.textDim }}>
                  {preview.recipients.join(", ")}
                </span>
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

      {/* prior accruals sends for this client */}
      <div style={{ marginTop: 16 }}>
        <SectionLabel>Prior accruals sends</SectionLabel>
        {history === null ? (
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Loading send history…</span>
        ) : history.length === 0 ? (
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            No accruals reports have been emailed for this client yet.
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
                      {d.recipients.length} recipient{d.recipients.length === 1 ? "" : "s"}
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
