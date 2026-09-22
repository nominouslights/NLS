import { request } from "./transport";
import type { ServiceType, StatusKind } from "../theme";
import { SERVICE_TYPE_LABELS, svcForServiceType, type ClientServiceType } from "./clients";

// ---------------------------------------------------------------------------
// Notifications API client — contract owned by Backend/ (Notifications module,
// NotificationsEndpoints.cs). Shapes mirror the backend's EmailTemplateResponse
// / EmailDispatchResponse and the request records exactly (JSON camelCase,
// enums as PascalCase strings). Do not invent fields — extend only when the
// backend contract changes.
// Send requests are composed FRONTEND-SIDE: Notifications never reads Trips,
// Billing, or Clients data (integration-events-only rule), so the dispatcher's
// screen supplies trip context + per-recipient merge values as opaque
// snapshots — and the accruals send posts the whole report as pre-formatted
// strings (lib/billing/accruals.ts → accrualsEmailPayload).
// ---------------------------------------------------------------------------

/** The Clients module's ServiceType plus Notifications-only entries (declared
 *  per-module backend-side). CommunityBookingAtRisk is a notification purpose,
 *  not a trip service: the "trip at risk — seats needed" email sent when a
 *  community booking day reverts (an override template for the built-in body).
 *  CommunityBookingPasses is likewise a purpose, not a service: the traveller
 *  passes emailed to a community booking's customer (a built-in composer, no
 *  template — never offered as a template target). */
export type NotificationServiceType =
  | ClientServiceType
  | "CommunityBookingAtRisk"
  | "CommunityBookingPasses";

export type EmailDispatchStatus = "Sent" | "PartiallyFailed" | "Failed";
export type EmailRecipientStatus = "Sent" | "Failed";

/** Mirrors EmailTemplateResponse. clientId/clientName set = client-specific
 *  template; serviceType is always present (the fallback grouping). */
export interface EmailTemplateRecord {
  id: string;
  name: string;
  serviceType: NotificationServiceType;
  clientId: string | null;
  clientName: string | null;
  subject: string;
  htmlBody: string;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST /api/notifications/templates and PUT /{id} body (EmailTemplateRequest).
 *  clientName is required iff clientId is set (name snapshot at pick time). */
export interface EmailTemplateInput {
  name: string;
  serviceType: NotificationServiceType;
  clientId?: string | null;
  clientName?: string | null;
  subject: string;
  htmlBody: string;
}

/** GET /api/notifications/templates query filters. */
export interface EmailTemplateListParams {
  serviceType?: NotificationServiceType;
  clientId?: string;
  includeInactive?: boolean;
}

/** POST /api/notifications/templates/preview body — works for unsaved edits;
 *  null/omitted values → the server substitutes sample data. */
export interface EmailPreviewInput {
  subject: string;
  htmlBody: string;
  values?: Record<string, string> | null;
}

/** Preview response: the exact server-side renderer's output. */
export interface EmailPreviewResult {
  subject: string;
  htmlBody: string;
  textBody: string;
}

/** One recipient in the send request (RecipientRequest). Stops are the merge
 *  values for {{PickupStop}}/{{DropoffStop}} — already resolved frontend-side. */
export interface SendRecipientInput {
  email: string;
  passengerName: string;
  pickupStop?: string | null;
  dropoffStop?: string | null;
  pickupAddress?: string | null;
  dropoffStopAddress?: string | null;
  /** This passenger's OWN {{PickupTime}}/{{DropoffTime}} — resolved from the route
   *  timetable and their boarding stop, so a mid-corridor passenger is told when the
   *  vehicle reaches them rather than when it left the origin. Omit (undefined) to fall
   *  back to the request's trip-level times; an empty string would not, so prefer
   *  undefined over "". */
  pickupTime?: string;
  dropoffTime?: string;
}

/** POST /api/notifications/emails/trip-pickup body (SendTripPickupEmailRequest).
 *  dispatchId is a CLIENT-generated GUID: replaying the same id returns the
 *  stored dispatch without re-sending (idempotency). 1–16 recipients.
 *  clientId (null = client-less trip) is validated server-side against the
 *  template's client pin. */
export interface SendTripPickupEmailInput {
  dispatchId: string;
  templateId: string;
  tripId: string;
  tripNumber: string;
  manifestId: string | null;
  serviceType: NotificationServiceType;
  tripDate: string;
  pickupTime: string;
  dropoffTime: string;
  route: string;
  clientId: string | null;
  clientName: string | null;
  recipients: SendRecipientInput[];
  /** Pre-resolved contact emails (contacts flagged receivesEmailReports).
   *  Backend emails them a report — but only for ContractCrew trips. Empty
   *  otherwise. */
  reportRecipients: string[];
}

/** POST /api/notifications/emails/trip-pickup/report-preview body — mirrors the
 *  send request MINUS dispatchId. Composed frontend-side from the same modal
 *  state as a send, so the preview reflects exactly what a send would produce. */
export type PickupReportPreviewInput = Omit<SendTripPickupEmailInput, "dispatchId">;

/** Response of the report preview. At preview time NOTHING has been sent, so the
 *  summary body reads "0 pickup emails sent…" and each recipient is tagged
 *  "(Preview)" — this is a content preview of what the report WILL contain, not
 *  sent history. Render htmlBody/textBody as a preview only; pdfBase64 is our
 *  generated PDF (base64, no data: prefix). */
export interface PickupReportPreviewResult {
  subject: string;
  htmlBody: string;
  textBody: string;
  pdfBase64: string;
  recipientCount: number;
  reportRecipients: string[];
}

/** Per-recipient outcome embedded on the dispatch (RecipientResult). */
export interface EmailRecipientResult {
  email: string;
  passengerName: string;
  status: EmailRecipientStatus;
  errorCode: string | null;
  errorMessage: string | null;
  postmarkMessageId: string | null;
}

/** Mirrors EmailDispatchResponse — one send action with per-recipient outcomes.
 *  Returned with HTTP 200 even on partial/total provider failure: the outcomes
 *  are data the dispatcher must see. Trip pickup dispatches carry trip/template
 *  references; client accruals dispatches carry NONE of the four (all null) and
 *  are anchored by clientId instead — and on those, each recipient's
 *  passengerName field carries the CONTACT's display name. Booking-pass
 *  dispatches are anchored by bookingId/bookingReference alone (trip, template
 *  and client all null), with passengerName again the contact's name. */
export interface EmailDispatchRecord {
  id: string;
  tripId: string | null;
  tripNumber: string | null;
  manifestId: string | null;
  templateId: string | null;
  templateName: string | null;
  serviceType: NotificationServiceType;
  clientId: string | null;
  bookingId: string | null;
  bookingReference: string | null;
  status: EmailDispatchStatus;
  sentAtUtc: string;
  recipients: EmailRecipientResult[];
}

// ---------------------------------------------------------------------------
// Client accruals email — the report travels as pre-formatted strings (labels,
// dates, amounts with any " est." markings baked in): the backend renders them
// verbatim into the covering email + PDF, doing zero domain lookups of its own.
// Compose the report object with accrualsEmailPayload (lib/billing/accruals.ts)
// so it can never disagree with the Reports screen or the printed sheet.
// ---------------------------------------------------------------------------

/** One selected contact: address + the display name recorded in history. */
export interface AccrualsRecipientInput {
  email: string;
  contactName: string;
}

/** Wire shape of the report (ClientAccrualsReportRequest) — strings only. */
export interface AccrualsEmailReport {
  clientName: string;
  periodLabel: string;
  preparedDate: string;
  /** Degradation banners (manual billing, failed fetches, unpaired legs). */
  notes: string[];
  summary: {
    bucketLabel: string;
    roundTrips: string;
    actualCad: string;
    estimatedCad: string;
  }[];
  buckets: {
    label: string;
    rows: {
      date: string;
      tripNumbers: string;
      route: string;
      poNumber: string;
      reference: string;
      amountCad: string;
    }[];
  }[];
  reconciliation: {
    date: string;
    tripNumbers: string;
    route: string;
    status: string;
    reason: string;
    amountCad: string;
  }[];
  invoices: {
    invoiceNumber: string;
    qboInvoiceId: string;
    status: string;
    periodLabel: string;
    totalCad: string;
  }[];
}

/** POST /api/notifications/emails/client-accruals body
 *  (SendClientAccrualsEmailRequest). dispatchId is a CLIENT-generated GUID:
 *  replaying the same id returns the stored dispatch without re-sending
 *  (idempotency). serviceType is the client's service category (enum name).
 *  Recipients: validated, deduped case-insensitively, 1–16 distinct. */
export interface SendClientAccrualsEmailInput {
  dispatchId: string;
  clientId: string;
  clientName: string;
  serviceType: NotificationServiceType;
  report: AccrualsEmailReport;
  recipients: AccrualsRecipientInput[];
}

/** POST /api/notifications/emails/client-accruals/preview body — the send
 *  request MINUS dispatchId (a preview records nothing, so no idempotency
 *  key). Same composer as the send, so the preview is byte-for-byte what a
 *  contact would receive. */
export type ClientAccrualsPreviewInput = Omit<SendClientAccrualsEmailInput, "dispatchId">;

/** Preview response. pdfBase64 is the attached report PDF (base64, no data:
 *  prefix); recipients echoes the deduped addresses a send would use. */
export interface ClientAccrualsPreviewResult {
  subject: string;
  htmlBody: string;
  textBody: string;
  pdfBase64: string;
  recipientCount: number;
  recipients: string[];
}

// ---------------------------------------------------------------------------
// Booking passes email — the pass sheet travels as pre-formatted strings
// (date label, seat "1 of 3", payment labels): the backend composes one
// bordered pass block per traveller into an HTML-only email (no PDF), doing
// zero Booking lookups of its own. Compose the sheet with passesEmailPayload
// (lib/booking/passes.ts) so it can never disagree with the detail screen or
// the printed NL-BP-01 sheet.
// ---------------------------------------------------------------------------

/** One traveller's pass on the wire (BookingPassTravellerRequest). */
export interface BookingPassTravellerPayload {
  name: string;
  phone: string | null;
  /** "1 of 3" — position in the booking, pre-formatted. */
  seat: string;
}

/** Wire shape of the pass sheet (BookingPassSheetRequest) — strings only. */
export interface BookingPassSheetPayload {
  reference: string;
  /** "Tuesday, September 15, 2026" — the long service-date label. */
  serviceDate: string;
  corridorName: string;
  pickup: string;
  dropoff: string;
  customerName: string;
  paymentMethod: string;
  paymentStatus: string;
  notes: string | null;
  travellers: BookingPassTravellerPayload[];
}

/** One recipient: address + the display name recorded in history. */
export interface PassRecipientInput {
  email: string;
  contactName: string;
}

/** POST /api/notifications/emails/booking-passes body
 *  (SendBookingPassesEmailRequest). dispatchId is a CLIENT-generated GUID:
 *  replaying the same id returns the stored dispatch without re-sending.
 *  Recipients: validated, deduped case-insensitively, 1–16 distinct. */
export interface SendBookingPassesEmailInput {
  dispatchId: string;
  bookingId: string;
  bookingReference: string;
  sheet: BookingPassSheetPayload;
  recipients: PassRecipientInput[];
}

/** POST /api/notifications/emails/booking-passes/preview body — the send
 *  request MINUS dispatchId (a preview records nothing). Same composer as the
 *  send, so the preview is byte-for-byte what the customer would receive. */
export type BookingPassesPreviewInput = Omit<SendBookingPassesEmailInput, "dispatchId">;

/** Preview response — HTML-only (no PDF pane); recipients echoes the deduped
 *  addresses a send would use. */
export interface BookingPassesPreviewResult {
  subject: string;
  htmlBody: string;
  textBody: string;
  recipientCount: number;
  recipients: string[];
}

// ---------------------------------------------------------------------------
// Template endpoints
// ---------------------------------------------------------------------------

export function listEmailTemplates(params?: EmailTemplateListParams): Promise<EmailTemplateRecord[]> {
  const q = new URLSearchParams();
  if (params?.serviceType) q.set("serviceType", params.serviceType);
  if (params?.clientId) q.set("clientId", params.clientId);
  if (params?.includeInactive) q.set("includeInactive", "true");
  const qs = q.toString();
  return request<EmailTemplateRecord[]>(`/api/notifications/templates${qs ? `?${qs}` : ""}`);
}

export function getEmailTemplate(id: string): Promise<EmailTemplateRecord> {
  return request<EmailTemplateRecord>(`/api/notifications/templates/${id}`);
}

/** POST → 201 { id }. Unknown merge token → 400 Notifications.Template.UnknownMergeField. */
export async function createEmailTemplate(input: EmailTemplateInput): Promise<string> {
  const res = await request<{ id: string }>("/api/notifications/templates", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateEmailTemplate(id: string, input: EmailTemplateInput): Promise<void> {
  return request<void>(`/api/notifications/templates/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/** POST /activate | /deactivate → 204. Templates are deactivated, never deleted. */
export function setEmailTemplateActive(id: string, active: boolean): Promise<void> {
  return request<void>(`/api/notifications/templates/${id}/${active ? "activate" : "deactivate"}`, {
    method: "POST",
  });
}

/** Renders subject/htmlBody through the exact server-side merge renderer.
 *  Render the returned htmlBody ONLY inside a fully sandboxed iframe
 *  (`<iframe sandbox="" srcDoc={…}>`) — template HTML is untrusted. */
export function previewEmailTemplate(input: EmailPreviewInput): Promise<EmailPreviewResult> {
  return request<EmailPreviewResult>("/api/notifications/templates/preview", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

// ---------------------------------------------------------------------------
// Send + history endpoints
// ---------------------------------------------------------------------------

/** POST → 200 EmailDispatchResponse (also on partial/total failure — the
 *  response is authoritative, no polling needed). 404 unknown template;
 *  400 inactive template / bad recipients / template–trip mismatch
 *  (Notifications.Template.ServiceTypeMismatch, .ClientMismatch). */
export function sendTripPickupEmail(input: SendTripPickupEmailInput): Promise<EmailDispatchRecord> {
  return request<EmailDispatchRecord>("/api/notifications/emails/trip-pickup", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST → 200 { subject, htmlBody, textBody, pdfBase64, recipientCount,
 *  reportRecipients }. Renders exactly what the crew-trip report email would
 *  contain WITHOUT sending anything (no dispatch is created). Same auth as the
 *  send; same 4xx template/recipient validation. */
export function previewTripPickupReport(input: PickupReportPreviewInput): Promise<PickupReportPreviewResult> {
  return request<PickupReportPreviewResult>("/api/notifications/emails/trip-pickup/report-preview", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST → 200 EmailDispatchResponse (also on partial/total provider failure —
 *  read the per-recipient outcomes). Replaying the same dispatchId returns the
 *  stored dispatch without re-sending. */
export function sendClientAccrualsEmail(
  input: SendClientAccrualsEmailInput,
): Promise<EmailDispatchRecord> {
  return request<EmailDispatchRecord>("/api/notifications/emails/client-accruals", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST → 200 { subject, htmlBody, textBody, pdfBase64, recipientCount,
 *  recipients }. Renders exactly what the accruals email + PDF would contain
 *  WITHOUT sending anything (no dispatch is created). Same recipient
 *  validation as the send. */
export function previewClientAccrualsEmail(
  input: ClientAccrualsPreviewInput,
): Promise<ClientAccrualsPreviewResult> {
  return request<ClientAccrualsPreviewResult>("/api/notifications/emails/client-accruals/preview", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST → 200 EmailDispatchResponse (also on partial/total provider failure —
 *  read the per-recipient outcomes). Replaying the same dispatchId returns the
 *  stored dispatch without re-sending. 400 Notifications.Dispatch.
 *  {BookingRequired|NoRecipients|InvalidRecipientEmail|TooManyRecipients|NoTravellers}. */
export function sendBookingPassesEmail(input: SendBookingPassesEmailInput): Promise<EmailDispatchRecord> {
  return request<EmailDispatchRecord>("/api/notifications/emails/booking-passes", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** POST → 200 { subject, htmlBody, textBody, recipientCount, recipients }.
 *  Renders exactly what the passes email would contain WITHOUT sending
 *  anything (no dispatch is created). Same recipient validation as the send. */
export function previewBookingPassesEmail(input: BookingPassesPreviewInput): Promise<BookingPassesPreviewResult> {
  return request<BookingPassesPreviewResult>("/api/notifications/emails/booking-passes/preview", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

/** GET /api/notifications/emails?bookingId={id} → the booking's pass sends,
 *  newest first. One of tripId/clientId/bookingId is required by the endpoint
 *  (precedence tripId > clientId > bookingId). */
export function listBookingEmailDispatches(bookingId: string): Promise<EmailDispatchRecord[]> {
  const q = new URLSearchParams({ bookingId });
  return request<EmailDispatchRecord[]>(`/api/notifications/emails?${q.toString()}`);
}

/** GET /api/notifications/emails?tripId={id} → dispatches, newest first. */
export function listTripEmailDispatches(tripId: string): Promise<EmailDispatchRecord[]> {
  const q = new URLSearchParams({ tripId });
  return request<EmailDispatchRecord[]>(`/api/notifications/emails?${q.toString()}`);
}

/** GET /api/notifications/emails?clientId={id} → the client's dispatches,
 *  newest first — accruals sends AND any pickup sends carrying the client.
 *  One of tripId/clientId is required by the endpoint. */
export function listClientEmailDispatches(clientId: string): Promise<EmailDispatchRecord[]> {
  const q = new URLSearchParams({ clientId });
  return request<EmailDispatchRecord[]>(`/api/notifications/emails?${q.toString()}`);
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible. Shared helper lives in
// lib/api/shared.ts (same backend pattern as every other module).
export { refetchUntil } from "./shared";

// ---------------------------------------------------------------------------
// Display derivations & helpers — status colour NEVER stands alone (StatusChip
// pairs the colour with a glyph and text label).
// ---------------------------------------------------------------------------

/** Display labels for every notification service type — the Clients labels
 *  plus the Notifications-only entries. Use these (never the raw enum string)
 *  wherever a template's service type is shown. */
export const NOTIFICATION_SERVICE_TYPE_LABELS: Record<NotificationServiceType, string> = {
  ...SERVICE_TYPE_LABELS,
  CommunityBookingAtRisk: "Community — booking at risk",
  CommunityBookingPasses: "Community — booking passes",
};

/** The Notifications-only purposes: sends with a built-in composer and no
 *  template. Screens that list templates or offer template targets use this
 *  to label them explicitly (never the raw enum) and to exclude them. */
export type NotificationOnlyServiceType = Exclude<NotificationServiceType, ClientServiceType>;

export const NOTIFICATION_ONLY_SERVICE_TYPES: readonly NotificationOnlyServiceType[] = [
  "CommunityBookingAtRisk",
  "CommunityBookingPasses",
];

export function isNotificationOnlyServiceType(
  serviceType: NotificationServiceType,
): serviceType is NotificationOnlyServiceType {
  return (NOTIFICATION_ONLY_SERVICE_TYPES as readonly string[]).includes(serviceType);
}

/** Notification service type → the console's theme service key (svcMeta).
 *  Notifications-only entries render under the service they belong to
 *  (CommunityBookingAtRisk / CommunityBookingPasses → community). */
export function svcForNotificationServiceType(serviceType: NotificationServiceType): ServiceType {
  if (isNotificationOnlyServiceType(serviceType)) return "community";
  return svcForServiceType(serviceType);
}

/** Canonical merge-field set — mirrors the backend's MergeFields.cs exactly
 *  (case-sensitive PascalCase; a typo in a saved template is rejected with
 *  400 UnknownMergeField). Descriptions say where the value comes from. */
export const MERGE_FIELDS: { token: string; description: string }[] = [
  { token: "{{PassengerName}}", description: "Passenger's name from the manifest" },
  { token: "{{TripDate}}", description: "Trip service date" },
  { token: "{{PickupTime}}", description: "Departure window start" },
  { token: "{{DropoffTime}}", description: "Dropoff / arrival time (trip window end)" },
  { token: "{{Route}}", description: "Route / corridor name" },
  { token: "{{PickupStop}}", description: "Passenger's pickup stop (falls back to trip origin)" },
  { token: "{{PickupAddress}}", description: "The passenger's pickup stop street address" },
  { token: "{{DropoffStop}}", description: "Passenger's dropoff stop (falls back to trip destination)" },
  { token: "{{DropoffStopAddress}}", description: "The passenger's dropoff (final) stop street address" },
  { token: "{{TripNumber}}", description: "Trip number" },
  { token: "{{ClientName}}", description: "Client name (empty when the trip has no client)" },
  { token: "{{SeatsNeeded}}", description: "Seats still needed to save an at-risk community booking day" },
];

/** The manifest contact field is free-text email-or-phone. This RFC-lite gate
 *  deliberately under-selects — excluded rows always show a reason so the
 *  dispatcher can fix the manifest. */
const EMAIL_CONTACT_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function isEmailContact(contact: string | null | undefined): boolean {
  if (!contact) return false;
  return EMAIL_CONTACT_RE.test(contact.trim());
}

/** Dispatch status chip (kind + label travel together): Sent → teal,
 *  PartiallyFailed → gold, Failed → vermillion. */
export function dispatchChip(status: EmailDispatchStatus): { kind: StatusKind; label: string } {
  switch (status) {
    case "Sent":
      return { kind: "ontime", label: "Sent" };
    case "PartiallyFailed":
      return { kind: "soon", label: "Partially failed" };
    case "Failed":
    default:
      return { kind: "over", label: "Failed" };
  }
}
