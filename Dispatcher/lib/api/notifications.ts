import { request } from "./transport";
import type { StatusKind } from "../theme";
import type { ClientServiceType } from "./clients";

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

/** Same enum as the Clients module's ServiceType (declared per-module backend-side). */
export type NotificationServiceType = ClientServiceType;

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
 *  passengerName field carries the CONTACT's display name. */
export interface EmailDispatchRecord {
  id: string;
  tripId: string | null;
  tripNumber: string | null;
  manifestId: string | null;
  templateId: string | null;
  templateName: string | null;
  serviceType: NotificationServiceType;
  clientId: string | null;
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
    status: string;
    subtotalCad: string;
    gstCad: string;
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
