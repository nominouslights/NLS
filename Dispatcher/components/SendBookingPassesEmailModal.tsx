"use client";

import { useEffect, useMemo, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import {
  dispatchChip,
  listBookingEmailDispatches,
  previewBookingPassesEmail,
  sendBookingPassesEmail,
  type BookingPassesPreviewResult,
  type EmailDispatchRecord,
} from "@/lib/api/notifications";
import { passesEmailPayload, type BookingPassSheet } from "@/lib/booking/passes";
import { getClaims } from "@/lib/auth";
import { ModalShell } from "@/components/ui/ModalShell";
import { ActionButton } from "@/components/ui/Button";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { SectionLabel } from "@/components/ui/Panel";
import { fmtUtcDateTime, RecipientOutcomeRow } from "@/components/email/RecipientOutcomeRow";

// Send-booking-passes modal (BookingDetail → Notifications module), modeled on
// SendAccrualsEmailModal. The frontend composes the WHOLE send request — the
// pass sheet travels as pre-formatted strings (passesEmailPayload, the same
// derivation the detail screen and the NL-BP-01 print render) because
// Notifications never reads Booking data (integration-events-only rule). The
// email is HTML-only: one bordered pass block per traveller, no PDF.
// Recipient = the booking's customer (pre-checked), plus an optional copy to
// the signed-in dispatcher (default on). The POST response is authoritative
// (200 even on partial/total provider failure) — outcomes render inline.
// dispatchId is ONE GUID held for the modal's lifetime: a retry after a
// network failure replays idempotently instead of double-sending the passes.
// Gating (Confirmed only, customer has an email) is the caller's job — the
// screen never opens this modal otherwise.

export default function SendBookingPassesEmailModal({
  sheet,
  bookingId,
  onClose,
}: {
  sheet: BookingPassSheet;
  bookingId: string;
  onClose: () => void;
}) {
  // ONE idempotency GUID for the modal's lifetime — see the header comment.
  const [dispatchId] = useState(() => crypto.randomUUID());

  // The wire sheet — derived ONCE from the same BookingPassSheet the screen
  // renders, so the emailed passes can never disagree with what's on screen.
  const payload = useMemo(() => passesEmailPayload(sheet), [sheet]);

  // The customer is the recipient; the screen only opens this modal when the
  // customer has an email, but the guard stays for the type.
  const customer =
    sheet.customer.email && sheet.customer.email.trim()
      ? { name: sheet.customer.name, email: sheet.customer.email.trim() }
      : null;
  const [customerSelected, setCustomerSelected] = useState(true);

  // Prior pass sends for this booking (newest first).
  const [history, setHistory] = useState<EmailDispatchRecord[] | null>(null);
  const [expanded, setExpanded] = useState<string[]>([]);

  useEffect(() => {
    let active = true;
    listBookingEmailDispatches(bookingId)
      .catch(() => [] as EmailDispatchRecord[])
      .then((dispatches) => {
        if (!active) return;
        setHistory(dispatches);
      });
    return () => {
      active = false;
    };
  }, [bookingId]);

  // "Send me a copy" — the signed-in dispatcher's address from the access
  // token (unverified decode, a UX affordance; see lib/claims.ts). Captured
  // once at mount like dispatchId, and null only if the token is unreadable.
  const [myEmail] = useState(() => getClaims()?.email || null);
  const [copyToSelf, setCopyToSelf] = useState(true);

  const selectedRecipients = customer && customerSelected ? [customer] : [];

  // What actually goes out: the customer (if selected), plus the self-copy
  // appended LAST — skipped here when it equals the customer's address (the
  // backend's case-insensitive dedupe backstops it), so the count on SEND
  // never overstates the batch.
  const selfAlreadySelected =
    myEmail !== null && selectedRecipients.some((r) => r.email.toLowerCase() === myEmail.toLowerCase());
  const outgoingRecipients =
    copyToSelf && myEmail !== null && !selfAlreadySelected
      ? [...selectedRecipients, { name: "Dispatcher copy", email: myEmail }]
      : selectedRecipients;

  // Preview — on demand, composed from the CURRENT selection with the exact
  // send-time composer server-side (nothing is sent, no dispatch is created).
  const [preview, setPreview] = useState<BookingPassesPreviewResult | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);

  async function loadPreview() {
    if (previewLoading) return;
    if (selectedRecipients.length === 0) {
      setPreviewError("Select the customer as a recipient.");
      return;
    }
    setPreviewLoading(true);
    setPreviewError(null);
    try {
      const result = await previewBookingPassesEmail({
        bookingId,
        bookingReference: sheet.reference,
        sheet: payload,
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
      setSendError("Select the customer as a recipient.");
      return;
    }
    setSending(true);
    setSendError(null);
    try {
      const result = await sendBookingPassesEmail({
        dispatchId, // stable for the modal — a retry replays, never re-sends
        bookingId,
        bookingReference: sheet.reference,
        sheet: payload,
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

  function toggleExpanded(id: string) {
    setExpanded((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
  }

  const frozen = lastResult !== null;
  const passCount = sheet.passes.length;

  return (
    <ModalShell
      eyebrow={`Community Booking · ${sheet.reference} · ${sheet.serviceDateLabel}`}
      title="Email Booking Passes"
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
            disabled={sending || selectedRecipients.length === 0 || frozen}
          >
            {frozen
              ? "SENT ✓"
              : sending
                ? "SENDING…"
                : `SEND PASSES${outgoingRecipients.length > 0 ? ` (${outgoingRecipients.length})` : ""}`}
          </ActionButton>
        </>
      }
    >
      {/* what goes out — one pass per traveller, in the customer's email */}
      <SectionLabel>
        Passes in this email · {passCount} traveller{passCount === 1 ? "" : "s"}
      </SectionLabel>
      <div style={{ display: "flex", flexWrap: "wrap", gap: 6, marginBottom: 14 }}>
        {sheet.passes.map((p) => (
          <span
            key={p.index}
            style={{
              display: "inline-flex",
              alignItems: "center",
              gap: 6,
              padding: "4px 9px",
              borderRadius: 7,
              border: `1px solid ${colors.borderSubtle}`,
              background: colors.cardBg,
              fontFamily: fonts.body,
              fontSize: 12,
              color: colors.textSecondary,
            }}
          >
            <MonoTag>{p.seq}</MonoTag>
            {p.travellerName}
          </span>
        ))}
      </div>

      {/* recipient — the booking's customer */}
      <SectionLabel>Recipient — the booking&rsquo;s customer</SectionLabel>
      {customer === null ? (
        <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
          <StatusChip kind="soon" label="No email on file for this customer" />
          <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, lineHeight: 1.5 }}>
            Add an email address to {sheet.customer.name}&rsquo;s customer record, then reopen this dialog —
            or print the passes instead.
          </span>
        </div>
      ) : (
        <div
          onClick={() => !frozen && setCustomerSelected((v) => !v)}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            padding: "8px 11px",
            borderRadius: 8,
            background: colors.cardBg,
            border: `1px solid ${colors.borderSubtle}`,
            cursor: frozen ? "default" : "pointer",
          }}
        >
          <input
            type="checkbox"
            checked={customerSelected}
            disabled={frozen}
            onChange={() => setCustomerSelected((v) => !v)}
            onClick={(e) => e.stopPropagation()}
            style={{ accentColor: colors.blue, cursor: frozen ? "default" : "pointer" }}
          />
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
            {customer.name}
          </span>
          <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textMuted }}>{customer.email}</span>
        </div>
      )}

      {/* self-copy — the signed-in dispatcher rides along on the customer
          send. Dashed border sets it apart; on its own it never enables SEND. */}
      {myEmail !== null && (
        <div
          onClick={() => !frozen && setCopyToSelf((v) => !v)}
          style={{
            display: "flex",
            alignItems: "center",
            gap: 10,
            padding: "8px 11px",
            marginTop: 8,
            borderRadius: 8,
            background: colors.cardBg,
            border: `1px dashed ${colors.borderStrong}`,
            cursor: frozen ? "default" : "pointer",
          }}
        >
          <input
            type="checkbox"
            checked={copyToSelf}
            disabled={frozen}
            onChange={() => setCopyToSelf((v) => !v)}
            onClick={(e) => e.stopPropagation()}
            style={{ accentColor: colors.blue, cursor: frozen ? "default" : "pointer" }}
          />
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textPrimary }}>
            Send me a copy
          </span>
          <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textMuted }}>{myEmail}</span>
          {selfAlreadySelected && (
            <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim }}>
              same address as the customer — sent once
            </span>
          )}
        </div>
      )}

      {/* preview — identical composition to a send, nothing gets dispatched */}
      {(preview || previewLoading || previewError) && (
        <div style={{ marginTop: 16 }}>
          <SectionLabel>Preview — as the customer will receive it</SectionLabel>
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
              {/* HTML-only email — rendered in a fully sandboxed frame */}
              <iframe
                sandbox=""
                srcDoc={preview.htmlBody}
                title="Booking passes email preview"
                style={{
                  width: "100%",
                  height: 460,
                  border: `1px solid ${colors.borderStrong}`,
                  borderRadius: 9,
                  background: "#FFFFFF",
                }}
              />
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

      {/* prior pass sends for this booking */}
      <div style={{ marginTop: 16 }}>
        <SectionLabel>Prior sends</SectionLabel>
        {history === null ? (
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>Loading send history…</span>
        ) : history.length === 0 ? (
          <span style={{ fontFamily: fonts.body, fontSize: 12, color: colors.textDim }}>
            No passes have been emailed for this booking yet.
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
