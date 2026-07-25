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
// QuickBooks Online is a READ-ONLY book of record: qboInvoiceId/qboSyncStatus
// merely RECORD reconciliation state (set via /qbo-status) — there is no QBO
// API write path anywhere in the stack.
// ---------------------------------------------------------------------------

export type InvoiceStatus = "Draft" | "Sent" | "Paid" | "Void";
export type QboSyncStatus = "NotSynced" | "Matched" | "UnmatchedPayment";

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

/** Mirrors InvoiceSummaryResponse (list rows — no lines). DueDate/IsOverdue
 *  are derived read-time by the backend (Sent + netTerms); Overdue is never a
 *  stored status. */
export interface InvoiceSummaryRecord {
  id: string;
  invoiceNumber: string;
  clientId: string;
  clientName: string;
  poNumber: string | null;
  budgetCode: string | null;
  netTermsDays: number;
  periodStart: string; // DateOnly
  periodEnd: string;
  status: InvoiceStatus;
  issuedAtUtc: string;
  sentAtUtc: string | null;
  paidAtUtc: string | null;
  dueDate: string | null; // DateOnly
  isOverdue: boolean;
  subtotalCad: number;
  gstCad: number;
  totalCad: number;
  lineCount: number;
  qboInvoiceId: string | null;
  qboSyncStatus: QboSyncStatus;
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
  netTermsDays: number;
  gstApplicable: boolean;
  gstRate: number; // fraction, e.g. 0.05
  periodStart: string;
  periodEnd: string;
  status: InvoiceStatus;
  issuedAtUtc: string;
  sentAtUtc: string | null;
  paidAtUtc: string | null;
  dueDate: string | null;
  isOverdue: boolean;
  subtotalCad: number;
  gstCad: number;
  totalCad: number;
  qboInvoiceId: string | null;
  qboSyncStatus: QboSyncStatus;
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

export function sendInvoice(id: string): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/send`, { method: "POST" });
}

export function markInvoicePaid(id: string): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/mark-paid`, { method: "POST" });
}

export function voidInvoice(id: string): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/void`, { method: "POST" });
}

/** Records the QBO reconciliation state (read-only book of record — this is
 *  bookkeeping about QBO, never a write TO QBO). */
export function setInvoiceQboStatus(
  id: string,
  qboInvoiceId: string | null,
  syncStatus: QboSyncStatus,
): Promise<void> {
  return request<void>(`/api/billing/invoices/${id}/qbo-status`, {
    method: "POST",
    body: JSON.stringify({ qboInvoiceId, syncStatus }),
  });
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
// the colour with a glyph and text label). Overdue is a frontend/read-side
// derivation of Sent (never a stored status), rendered as the vermillion chip
// replacing Sent when isOverdue.
// ---------------------------------------------------------------------------

const DAY_MS = 86_400_000;

function daysSince(iso: string): number {
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / DAY_MS));
}

/** Whole days past a DateOnly due date (negative = not yet due). */
export function daysPastDue(dueDate: string | null): number | null {
  if (!dueDate) return null;
  return Math.floor((Date.now() - new Date(`${dueDate}T00:00:00`).getTime()) / DAY_MS);
}

/** Invoice status chip — Sent + isOverdue renders as the vermillion Overdue
 *  chip (replacing Sent). */
export function invoiceChip(inv: {
  status: InvoiceStatus;
  isOverdue: boolean;
}): { kind: StatusKind; label: string } {
  switch (inv.status) {
    case "Draft":
      return { kind: "soon", label: "Draft" };
    case "Sent":
      return inv.isOverdue ? { kind: "over", label: "Overdue" } : { kind: "info", label: "Sent" };
    case "Paid":
      return { kind: "ontime", label: "Paid" };
    case "Void":
    default:
      return { kind: "off", label: "Void" };
  }
}

export const QBO_LABELS: Record<QboSyncStatus, string> = {
  NotSynced: "Not synced",
  Matched: "Matched",
  UnmatchedPayment: "Unmatched payment",
};

export function qboKindFor(status: QboSyncStatus): StatusKind {
  switch (status) {
    case "Matched":
      return "ontime";
    case "UnmatchedPayment":
      return "over";
    case "NotSynced":
    default:
      return "off";
  }
}

/** List "age" column: days since sent for Sent rows, "Paid" once paid. */
export function invoiceAgeLabel(inv: {
  status: InvoiceStatus;
  sentAtUtc: string | null;
}): string {
  if (inv.status === "Paid") return "Paid";
  if (inv.status === "Sent" && inv.sentAtUtc) return `${daysSince(inv.sentAtUtc)}d`;
  return "—";
}

/** Newest first — issued timestamp, then invoice number. */
export function sortInvoices(rows: InvoiceSummaryRecord[]): InvoiceSummaryRecord[] {
  return [...rows].sort((a, b) => {
    if (a.issuedAtUtc !== b.issuedAtUtc) return a.issuedAtUtc < b.issuedAtUtc ? 1 : -1;
    return b.invoiceNumber.localeCompare(a.invoiceNumber);
  });
}

/** AR aging, computed client-side from the live invoice list: Sent invoices
 *  bucketed by days past dueDate (current ≤30 incl. not-yet-due / 31–60 / 61+). */
export interface ArAging {
  current: number;
  days31to60: number;
  days61plus: number;
}

export function arAgingFor(rows: InvoiceSummaryRecord[]): ArAging {
  const aging: ArAging = { current: 0, days31to60: 0, days61plus: 0 };
  for (const r of rows) {
    if (r.status !== "Sent") continue;
    const past = daysPastDue(r.dueDate) ?? 0;
    if (past > 60) aging.days61plus += r.totalCad;
    else if (past > 30) aging.days31to60 += r.totalCad;
    else aging.current += r.totalCad;
  }
  return aging;
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
