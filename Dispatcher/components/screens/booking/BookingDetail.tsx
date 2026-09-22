"use client";

import { useCallback, useEffect, useState } from "react";
import { colors, fonts } from "@/lib/theme";
import { ApiError } from "@/lib/api/transport";
import { formatUtcDate } from "@/lib/api/format";
import {
  bookingStatusKind,
  cancelBooking,
  confirmBooking,
  getBooking,
  paymentStatusChip,
  updateBooking,
  type BookingDetailRecord,
} from "@/lib/api/booking";
import { buildBookingPassSheet, markPaidInput, passIssuance, type BookingPassSheet } from "@/lib/booking/passes";
import { printBookingPass, printBookingPasses } from "@/lib/documents/bookingPassPdf";
import { DetailRow, PageHeader, Panel, SectionLabel } from "@/components/ui/Panel";
import { MonoTag, StatusChip } from "@/components/ui/Chip";
import { ActionButton } from "@/components/ui/Button";
import { fmtUtcDateTime } from "@/components/email/RecipientOutcomeRow";
import SendBookingPassesEmailModal from "@/components/SendBookingPassesEmailModal";

// Booking detail — one community booking after creation: who requested it,
// where it stands (booking status AND payment status, two orthogonal chips),
// the travellers under it, and the pass actions (print all / print one / email)
// for each traveller. Rendered inside the Bookings screen as selection state
// (no ScreenId — same pattern as Fleet and Clients), so the calendar's
// corridor/month/day state stays alive underneath.
//
// Reads are same-module transactional (Booking), so reload() is a plain
// getBooking after every mutation — no refetchUntil. 404 (unknown id, or
// another tenant's booking) → error panel with the back button.
//
// Gating — passIssuance (lib/booking/passes.ts) owns the pass rule; the
// booking-lifecycle rules are local:
//   CONFIRM        Unconfirmed only
//   CANCEL BOOKING Unconfirmed + Confirmed (hidden once Cancelled)
//   MARK AS PAID   Unpaid + not Cancelled (the PUT would 409); Paid → "Paid ✓"
//   PRINT / EMAIL  Confirmed only; EMAIL also needs a customer email
// Disabled buttons carry an adjacent dim hint (ActionButton is a span — no
// native tooltip), so the reason is always readable.

function fmtHold(iso: string): string {
  return fmtUtcDateTime(iso);
}

/** A toolbar action with its disabled-reason hint beside it. */
function GatedAction({
  label,
  hint,
  onClick,
  variant = "secondary",
  disabled,
}: {
  label: string;
  hint: string | null;
  onClick: () => void;
  variant?: "primary" | "secondary" | "destructive" | "success" | "amber";
  disabled: boolean;
}) {
  return (
    <span style={{ display: "inline-flex", alignItems: "center", gap: 8 }}>
      <ActionButton variant={variant} disabled={disabled} onClick={onClick}>
        {label}
      </ActionButton>
      {disabled && hint && (
        <span style={{ fontFamily: fonts.body, fontSize: 11, color: colors.textDim, lineHeight: 1.3, maxWidth: 180 }}>
          {hint}
        </span>
      )}
    </span>
  );
}

export default function BookingDetail({ bookingId, onBack }: { bookingId: string; onBack: () => void }) {
  const [detail, setDetail] = useState<BookingDetailRecord | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);

  // 404 = unknown id, or another tenant's booking (the tenant filter makes it
  // read as absent) — a distinct panel from a transient failure, which retries.
  function applyLoadError(e: unknown) {
    if (e instanceof ApiError && e.status === 404) {
      setNotFound(true);
      setLoadError(null);
    } else {
      setLoadError(e instanceof ApiError ? e.message : "Failed to load the booking — please try again.");
    }
  }

  // Plain fetch, keyed on the id; the active guard drops a stale response if
  // the dispatcher opens another booking before this one lands. Per-id state
  // is reset by the caller remounting this component (key={bookingId}); state
  // is set only inside the promise callbacks, never synchronously in the effect.
  useEffect(() => {
    let active = true;
    getBooking(bookingId).then(
      (d) => {
        if (active) setDetail(d);
      },
      (e: unknown) => {
        if (active) applyLoadError(e);
      },
    );
    return () => {
      active = false;
    };
  }, [bookingId]);

  /** After a mutation — same-module read, so one plain refetch is enough. */
  const reload = useCallback(async () => {
    try {
      setDetail(await getBooking(bookingId));
    } catch (e) {
      applyLoadError(e);
    }
  }, [bookingId]);

  // --- mutations (DayPanel.act pattern: one busy flag, one error banner) ---
  const [busy, setBusy] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  async function act(name: string, fn: () => Promise<void>) {
    if (busy) return;
    setBusy(name);
    setActionError(null);
    try {
      await fn();
      await reload();
    } catch (e) {
      setActionError(e instanceof ApiError ? e.message : "The change failed — please try again.");
    } finally {
      setBusy(null);
    }
  }

  const [emailSheet, setEmailSheet] = useState<BookingPassSheet | null>(null);

  const backButton = <ActionButton onClick={onBack}>← BACK TO CALENDAR</ActionButton>;

  if (notFound || loadError) {
    return (
      <div style={{ padding: "20px 26px" }} className="detailfade">
        <div style={{ marginBottom: 14 }}>{backButton}</div>
        <Panel borderColor="rgba(213,94,0,.4)">
          <div style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
            <StatusChip
              kind="over"
              label={notFound ? "Booking not found — it may belong to another tenant or have been removed" : `Booking unavailable — ${loadError}`}
            />
            {!notFound && (
              <ActionButton variant="primary" onClick={() => void reload()}>
                RETRY
              </ActionButton>
            )}
          </div>
        </Panel>
      </div>
    );
  }

  if (!detail) {
    return (
      <div style={{ padding: "20px 26px" }} className="detailfade">
        <div style={{ marginBottom: 14 }}>{backButton}</div>
        <div style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textDim }}>Loading booking…</div>
      </div>
    );
  }

  const b = detail.booking;
  const sheet = buildBookingPassSheet(detail);
  const issuance = passIssuance(detail);
  const pay = paymentStatusChip(b.paymentStatus);

  const cancelled = b.status === "Cancelled";
  const unconfirmed = b.status === "Unconfirmed";
  const anyBusy = busy !== null;

  // Gating table (see header). Print/email reasons come from passIssuance;
  // the email adds the no-address case only once passes may be issued.
  const printDisabled = !issuance.allowed;
  const printHint = issuance.reason;
  const emailHint = !issuance.allowed
    ? issuance.reason
    : !sheet.customer.email
      ? "No email on file for this customer"
      : null;
  const emailDisabled = emailHint !== null;

  return (
    <div style={{ display: "flex", flexDirection: "column", height: "100%" }} className="detailfade" key={b.id}>
      <div style={{ flex: "none", padding: "20px 26px 12px" }}>
        <PageHeader eyebrow={`Community Booking · ${b.reference}`} title={sheet.customer.name} right={backButton} />
        <div style={{ display: "flex", alignItems: "center", gap: 8, flexWrap: "wrap", marginTop: 12 }}>
          <StatusChip kind={bookingStatusKind(b.status)} label={b.status} />
          <StatusChip kind={pay.kind} label={pay.label} glyph={pay.glyph} />
          {b.holdExpired && unconfirmed && <StatusChip kind="soon" label="hold expired" />}
          <span style={{ fontFamily: fonts.body, fontSize: 12.5, color: colors.textMuted, marginLeft: 4 }}>
            {sheet.serviceDateLabel} · {sheet.corridorName}
          </span>
        </div>
      </div>

      <div style={{ flex: 1, minHeight: 0, overflowY: "auto", padding: "16px 26px 24px", borderTop: `1px solid ${colors.border}` }}>
        {actionError && (
          <Panel borderColor="rgba(213,94,0,.4)" style={{ marginBottom: 12 }}>
            <StatusChip kind="over" label={actionError} />
          </Panel>
        )}

        {/* toolbar */}
        <Panel style={{ marginBottom: 14 }}>
          <SectionLabel>Actions</SectionLabel>
          <div style={{ display: "flex", alignItems: "center", gap: 14, flexWrap: "wrap" }}>
            <GatedAction
              label="PRINT ALL PASSES"
              hint={printHint}
              disabled={printDisabled || anyBusy}
              onClick={() => printBookingPasses(sheet)}
            />
            <GatedAction
              label="EMAIL PASSES TO CUSTOMER"
              hint={emailHint}
              disabled={emailDisabled || anyBusy}
              onClick={() => setEmailSheet(sheet)}
            />
            {!cancelled &&
              (b.paymentStatus === "Paid" ? (
                <span style={{ fontFamily: fonts.body, fontSize: 12.5, fontWeight: 600, color: colors.textSecondary }}>
                  Paid ✓
                </span>
              ) : (
                <ActionButton
                  variant="amber"
                  disabled={anyBusy}
                  onClick={() => void act("paid", () => updateBooking(b.id, markPaidInput(detail)))}
                >
                  {busy === "paid" ? "WORKING…" : "MARK AS PAID"}
                </ActionButton>
              ))}
            {unconfirmed && (
              <ActionButton
                variant="success"
                disabled={anyBusy}
                onClick={() => void act("confirm", () => confirmBooking(b.id))}
              >
                {busy === "confirm" ? "WORKING…" : "CONFIRM"}
              </ActionButton>
            )}
            {!cancelled && (
              <ActionButton
                variant="destructive"
                disabled={anyBusy}
                onClick={() => void act("cancel", () => cancelBooking(b.id))}
                style={{ marginLeft: "auto" }}
              >
                {busy === "cancel" ? "WORKING…" : "CANCEL BOOKING"}
              </ActionButton>
            )}
          </div>
        </Panel>

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))", gap: 14, marginBottom: 14 }}>
          {/* customer */}
          <Panel>
            <SectionLabel>Customer</SectionLabel>
            <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
              <DetailRow label="Name" value={sheet.customer.name} />
              <DetailRow
                label="Phone"
                value={sheet.customer.phone ?? "—"}
                valueStyle={sheet.customer.phone ? { fontFamily: fonts.mono } : undefined}
              />
              <DetailRow
                label="Email"
                value={
                  sheet.customer.email ? (
                    <span style={{ fontFamily: fonts.mono }}>{sheet.customer.email}</span>
                  ) : (
                    <StatusChip kind="soon" label="No email on file" />
                  )
                }
              />
              {detail.customer?.notes && <DetailRow label="Customer notes" value={detail.customer.notes} />}
              {!detail.customer && (
                <span style={{ fontFamily: fonts.body, fontSize: 11.5, color: colors.textDim, lineHeight: 1.5 }}>
                  The customer record is no longer available — showing the name recorded on the booking.
                </span>
              )}
            </div>
          </Panel>

          {/* trip */}
          <Panel>
            <SectionLabel>Trip</SectionLabel>
            <div style={{ display: "flex", flexDirection: "column", gap: 9 }}>
              <DetailRow label="Service date" value={sheet.serviceDateLabel} />
              <DetailRow label="Corridor" value={sheet.corridorName} />
              <DetailRow label="Pickup → Drop-off" value={`${sheet.pickupLabel} → ${sheet.dropoffLabel}`} />
              <DetailRow label="Payment method" value={sheet.paymentMethodLabel} />
              <DetailRow
                label="Payment"
                value={<StatusChip kind={pay.kind} label={pay.label} glyph={pay.glyph} />}
              />
              {unconfirmed && (
                <DetailRow
                  label="Hold expires"
                  value={
                    b.holdExpired ? (
                      <StatusChip kind="soon" label={`Expired · ${fmtHold(b.holdExpiresAtUtc)}`} />
                    ) : (
                      <span style={{ fontFamily: fonts.mono }}>{fmtHold(b.holdExpiresAtUtc)}</span>
                    )
                  }
                />
              )}
              {b.notes && <DetailRow label="Booking notes" value={b.notes} />}
              <DetailRow label="Created" value={formatUtcDate(b.createdAtUtc)} valueStyle={{ fontFamily: fonts.mono }} />
              <DetailRow label="Updated" value={formatUtcDate(b.updatedAtUtc)} valueStyle={{ fontFamily: fonts.mono }} />
            </div>
          </Panel>
        </div>

        {/* travellers — one row per pass */}
        <Panel>
          <SectionLabel>
            Travellers · {sheet.passes.length} pass{sheet.passes.length === 1 ? "" : "es"}
          </SectionLabel>
          <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
            {sheet.passes.map((p) => (
              <div
                key={p.index}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 10,
                  flexWrap: "wrap",
                  padding: "10px 12px",
                  borderRadius: 9,
                  border: `1px solid ${colors.borderSubtle}`,
                  background: colors.cardBg,
                  opacity: cancelled ? 0.62 : 1,
                }}
              >
                <MonoTag>{p.seq}</MonoTag>
                <span style={{ fontFamily: fonts.body, fontSize: 13, fontWeight: 600, color: colors.textPrimary }}>
                  {p.travellerName}
                </span>
                {p.phone && (
                  <span style={{ fontFamily: fonts.mono, fontSize: 11, color: colors.textDim }}>{p.phone}</span>
                )}
                {p.isBillingCustomer && <MonoTag color={colors.skyBlue}>BILLING CUSTOMER</MonoTag>}
                <span style={{ marginLeft: "auto" }}>
                  <GatedAction
                    label="PRINT PASS"
                    hint={printHint}
                    disabled={printDisabled || anyBusy}
                    onClick={() => printBookingPass(sheet, p.index)}
                  />
                </span>
              </div>
            ))}
          </div>
        </Panel>
      </div>

      {emailSheet && (
        <SendBookingPassesEmailModal sheet={emailSheet} bookingId={b.id} onClose={() => setEmailSheet(null)} />
      )}
    </div>
  );
}
