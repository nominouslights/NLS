import { request } from "./transport";
import type { StatusKind } from "../theme";
import type { BillingFrequency } from "./clients";

// ---------------------------------------------------------------------------
// Billing API client — contract owned by Backend/ (Billing module,
// BillingEndpoints.cs). Shapes mirror the backend's InvoiceResponse /
// InvoiceSummaryResponse / BillableTripResponse and the request records
// exactly (JSON camelCase, enums as PascalCase strings, DateOnly as
// "yyyy-MM-dd"). Do not invent fields — extend only when the backend
// contract changes.
//
// This section PREPARES invoices for manual entry into QuickBooks Online — it
// is NOT an internal invoice system-of-record. QuickBooks is never called via
// API: the user copies/prints the numbers, keys them into QBO, then records the
// resulting QBO invoice number + entered date back here (mark-entered). There
// is no send / paid / overdue / AR-aging concept anymore; netTermsDays is
// purely informational.
// ---------------------------------------------------------------------------

export type InvoiceStatus = "Draft" | "EnteredInQbo" | "Void";
/** A trip leg's direction — Outbound (depart) pairs with Inbound (return) into
 *  a round trip. Same enum the Trips module exposes as TripDirection. */
export type TripLegDirection = "Inbound" | "Outbound";

/** Mirrors InvoiceLineResponse — amountCad is server-computed (qty × unit price). */
export interface InvoiceLineRecord {
  lineId: string;
  description: string;
  tripIds: string[];
  tripNumber: string | null;
  serviceDate: string | null; // DateOnly, "2026-07-01"
  quantity: number;
  unitPriceCad: number;
  amountCad: number;
}

/** Mirrors InvoiceSummaryResponse (list rows — no lines). qboEnteredDate is set
 *  once the invoice has been keyed into QuickBooks (EnteredInQbo). */
export interface InvoiceSummaryRecord {
  id: string;
  invoiceNumber: string;
  clientId: string;
  clientName: string;
  poNumber: string | null;
  budgetCode: string | null;
  netTermsDays: number; // informational only — no overdue logic
  periodStart: string; // DateOnly
  periodEnd: string;
  status: InvoiceStatus;
  issuedAtUtc: string;
  subtotalCad: number;
  gstCad: number;
  totalCad: number;
  lineCount: number;
  qboInvoiceId: string | null;
  qboEnteredDate: string | null; // DateOnly, set when EnteredInQbo
}

/** Mirrors InvoiceResponse (detail — lines included, contract snapshots). */
export interface InvoiceDetailRecord {
  id: string;
  invoiceNumber: string;
  clientId: string;
  clientName: string;
  contractId: string | null;
  poNumber: string | null;
  budgetCode: string | null;
  netTermsDays: number; // informational only — no overdue logic
  gstApplicable: boolean;
  gstRate: number; // fraction, e.g. 0.05
  periodStart: string;
  periodEnd: string;
  status: InvoiceStatus;
  issuedAtUtc: string;
  subtotalCad: number;
  gstCad: number;
  totalCad: number;
  qboInvoiceId: string | null;
  qboEnteredDate: string | null; // DateOnly, set when EnteredInQbo
  lines: InvoiceLineRecord[];
}

/** Mirrors BillableTripResponse — invoiceId null = still uninvoiced. */
export interface BillableTripRecord {
  id: string;
  tripNumber: string;
  clientId: string | null;
  clientName: string | null;
  serviceType: string;
  routeName: string;
  origin: string;
  destination: string;
  distanceKm: number;
  serviceDate: string; // DateOnly
  roundTripKey: string | null;
  /** Leg direction — Outbound/Inbound legs pair (via roundTripKey) into a round
   *  trip. Null for legacy/unclassified legs (backend adds this in parallel). */
  direction: TripLegDirection | null;
  poNumber: string | null;
  completedAtUtc: string;
  invoiceId: string | null;
}

/** PUT /api/billing/invoices/{id}/lines line (InvoiceLineRequest). lineId null
 *  = new line; amounts are server-computed — never sent. */
export interface InvoiceLineInput {
  lineId: string | null;
  description: string;
  tripIds: string[] | null;
  tripNumber: string | null;
  serviceDate: string | null;
  quantity: number;
  unitPriceCad: number;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

export function listInvoices(params?: {
  status?: InvoiceStatus;
  clientId?: string;
}): Promise<InvoiceSummaryRecord[]> {
  const q = new URLSearchParams();
  if (params?.status) q.set("status", params.status);
  if (params?.clientId) q.set("clientId", params.clientId);
  const qs = q.toString();
  return request<InvoiceSummaryRecord[]>(`/api/billing/invoices${qs ? `?${qs}` : ""}`);
}

export function getInvoice(id: string): Promise<InvoiceDetailRecord> {
  return request<InvoiceDetailRecord>(`/api/billing/invoices/${id}`);
}

/** POST → 201 { id }. Draft pulls uninvoiced completed round trips at the
 *  contract rate. Errors surfaced inline by the generate dialog:
 *  Billing.Invoice.NoActiveContract / Billing.Invoice.NotRoundTripBilled. */
export async function generateDraftInvoice(
  clientId: string,
  periodStart: string,
  periodEnd: string,
): Promise<string> {
  const res = await request<{ id: string }>("/api/billing/invoices/generate-draft", {
    method: "POST",
    body: JSON.stringify({ clientId, periodStart, periodEnd }),
  });
  return res.id;
}

/** PUT → 204. Draft only (409 Billing.Invoice.NotDraft otherwise); a trip
 *  already claimed by another invoice → 409 Billing.Invoice.TripAlreadyInvoiced. */
export function replaceInvoiceLines(id: string, lines: InvoiceLineInput[]): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/lines`, {
    method: "PUT",
    body: JSON.stringify({ lines }),
  });
}

/** POST → 204. Records that this draft was keyed into QuickBooks: captures the
 *  QBO invoice number + entered date, Draft → EnteredInQbo. */
export function markInvoiceEntered(
  id: string,
  qboInvoiceNumber: string,
  enteredDate: string,
): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/mark-entered`, {
    method: "POST",
    body: JSON.stringify({ qboInvoiceNumber, enteredDate }),
  });
}

/** POST → 204. Corrects the recorded QBO reference (EnteredInQbo only). */
export function updateQboReference(
  id: string,
  qboInvoiceNumber: string,
  enteredDate: string,
): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/qbo-reference`, {
    method: "POST",
    body: JSON.stringify({ qboInvoiceNumber, enteredDate }),
  });
}

/** POST → 204. EnteredInQbo → Draft, clearing the QBO reference (e.g. the QBO
 *  entry was a mistake and needs re-keying). */
export function reopenInvoice(id: string): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/reopen`, { method: "POST" });
}

export function voidInvoice(id: string): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/void`, { method: "POST" });
}

export function listBillableTrips(params?: {
  clientId?: string;
  uninvoiced?: boolean;
  from?: string;
  to?: string;
}): Promise<BillableTripRecord[]> {
  const q = new URLSearchParams();
  if (params?.clientId) q.set("clientId", params.clientId);
  if (params?.uninvoiced) q.set("uninvoiced", "true");
  if (params?.from) q.set("from", params.from);
  if (params?.to) q.set("to", params.to);
  const qs = q.toString();
  return request<BillableTripRecord[]>(`/api/billing/billable-trips${qs ? `?${qs}` : ""}`);
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible. Shared helper lives in
// lib/api/drivers.ts (same backend pattern).
export { refetchUntil } from "./drivers";

// ---------------------------------------------------------------------------
// Display derivations — status colour NEVER stands alone (StatusChip pairs
// the colour with a glyph and text label).
// ---------------------------------------------------------------------------

/** Invoice status chip — keyed only on status. Draft (gold, still to key into
 *  QBO), EnteredInQbo (teal, keyed), Void (gray). */
export function invoiceChip(inv: { status: InvoiceStatus }): { kind: StatusKind; label: string } {
  switch (inv.status) {
    case "Draft":
      return { kind: "soon", label: "Draft" };
    case "EnteredInQbo":
      return { kind: "ontime", label: "Entered in QBO" };
    case "Void":
    default:
      return { kind: "off", label: "Void" };
  }
}

/** Newest first — issued timestamp, then invoice number. */
export function sortInvoices(rows: InvoiceSummaryRecord[]): InvoiceSummaryRecord[] {
  return [...rows].sort((a, b) => {
    if (a.issuedAtUtc !== b.issuedAtUtc) return a.issuedAtUtc < b.issuedAtUtc ? 1 : -1;
    return b.invoiceNumber.localeCompare(a.invoiceNumber);
  });
}

// Invoice amounts carry cents (unlike the whole-dollar formatCad in lib/api.ts).
const cadCents = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export function formatInvoiceCad(value: number): string {
  return cadCents.format(value);
}

/** "2026-06-01 → 2026-06-30" (billing period line). */
export function periodLabel(inv: { periodStart: string; periodEnd: string }): string {
  return `${inv.periodStart} → ${inv.periodEnd}`;
}

/** Direction glyph + short label for a trip leg — rendered as glyph + text
 *  (never colour-only). Null direction → null (render nothing). Outbound "→",
 *  Inbound "←". */
export function directionMeta(
  direction: TripLegDirection | null,
): { glyph: string; label: string } | null {
  if (direction === "Outbound") return { glyph: "→", label: "Outbound" };
  if (direction === "Inbound") return { glyph: "←", label: "Inbound" };
  return null;
}

// ---------------------------------------------------------------------------
// Clipboard export — a clean, paste-friendly block for keying an invoice into
// QuickBooks Online by hand. Header lines, then one tab-delimited row per line
// (paste straight into a QBO line grid), then totals. Money via formatInvoiceCad.
// ---------------------------------------------------------------------------

function lineRef(l: InvoiceLineRecord): string {
  return [l.tripNumber, l.serviceDate].filter(Boolean).join(" · ");
}

export function invoiceClipboardText(inv: InvoiceDetailRecord): string {
  const out: string[] = [];
  out.push(`Client: ${inv.clientName}`);
  out.push(`Billing period: ${periodLabel(inv)}`);
  out.push(`PO #: ${inv.poNumber ?? "—"}`);
  out.push(`Budget code: ${inv.budgetCode ?? "—"}`);
  out.push(`Net terms: Net ${inv.netTermsDays}`);
  out.push("");
  out.push(["Description", "Trip # / Service date", "Qty", "Unit (CAD)", "Amount (CAD)"].join("\t"));
  for (const l of inv.lines) {
    out.push(
      [
        l.description,
        lineRef(l),
        String(l.quantity),
        formatInvoiceCad(l.unitPriceCad),
        formatInvoiceCad(l.amountCad),
      ].join("\t"),
    );
  }
  out.push("");
  out.push(`Subtotal: ${formatInvoiceCad(inv.subtotalCad)}`);
  if (inv.gstApplicable) {
    out.push(`GST (${Math.round(inv.gstRate * 1000) / 10}%): ${formatInvoiceCad(inv.gstCad)}`);
  }
  out.push(`Total (CAD): ${formatInvoiceCad(inv.totalCad)}`);
  return out.join("\n");
}

// ---------------------------------------------------------------------------
// Billing-period defaults for the generate-draft dialog — previous calendar
// month by default, honouring the client contract's billingFrequency when set.
// ---------------------------------------------------------------------------

function isoOf(d: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

export function previousCalendarMonth(): { start: string; end: string } {
  const now = new Date();
  return {
    start: isoOf(new Date(now.getFullYear(), now.getMonth() - 1, 1)),
    end: isoOf(new Date(now.getFullYear(), now.getMonth(), 0)),
  };
}

/** Weekly → previous Mon–Sun week; BiWeekly → the previous two full weeks;
 *  Monthly (or no contract) → previous calendar month. */
export function defaultBillingPeriod(
  frequency: BillingFrequency | null | undefined,
): { start: string; end: string } {
  if (frequency === "Weekly" || frequency === "BiWeekly") {
    const now = new Date();
    const monday = new Date(now);
    monday.setDate(now.getDate() - ((now.getDay() + 6) % 7)); // this week's Monday
    const start = new Date(monday);
    start.setDate(start.getDate() - (frequency === "Weekly" ? 7 : 14));
    const end = new Date(monday);
    end.setDate(end.getDate() - 1); // last Sunday
    return { start: isoOf(start), end: isoOf(end) };
  }
  return previousCalendarMonth();
}
