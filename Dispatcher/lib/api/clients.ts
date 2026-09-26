import { request } from "./transport";
import { docStatusFor } from "../maintenanceStore";
import type { ServiceType, StatusKind } from "../theme";

// ---------------------------------------------------------------------------
// Clients API client — contract owned by Backend/ (Clients module,
// ClientsEndpoints). Shapes mirror the backend's ClientResponse /
// ContractResponse / PurchaseOrderResponse exactly (JSON camelCase, enums as
// PascalCase strings). Do not invent fields — extend only when the backend
// contract changes.
// CRM (interactions, follow-ups) has NO backend — it stays mocked in
// lib/clientStore.ts. Contacts ARE backed (list/create/update below).
// ---------------------------------------------------------------------------

export type ClientType = "Client" | "VendorPartner";

export type ClientServiceType =
  | "ContractCrew"
  | "Community"
  | "Nihb"
  | "Charter"
  | "Cargo"
  | "Grocery";

export type BillingModel = "RoundTripRate" | "Manual";
export type BillingFrequency = "Monthly" | "BiWeekly" | "Weekly";
export type ContractStatus = "Active" | "Ended" | "Terminated";

/** Active-contract summary embedded on ClientResponse (null = no active contract). */
export interface ActiveContractSummary {
  activeContractId: string;
  startDate: string; // DateOnly, "2026-07-01"
  endDate: string | null; // null = evergreen
  billingModel: BillingModel;
  ratePerRoundTripCad: number | null;
  budgetCode: string | null;
  billingFrequency: BillingFrequency;
  netTermsDays: number;
  defaultPoNumber: string | null;
}

export interface ClientRecord {
  id: string;
  name: string;
  type: ClientType;
  serviceType: ClientServiceType | null;
  tag: string;
  agreementReference: string | null;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  activeContract: ActiveContractSummary | null;
}

/** POST /api/clients / PUT /api/clients/{id} body. */
export interface ClientInput {
  name: string;
  type: ClientType;
  serviceType: ClientServiceType | null;
  tag: string;
  agreementReference?: string | null;
  notes?: string | null;
}

export interface ClientContactRecord {
  id: string;
  clientId: string;
  name: string;
  title: string;
  email: string | null;
  phone: string | null;
  notes: string | null;
  isPrimary: boolean;
  receivesEmailReports: boolean;
  receivesAccrualsReports: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST /api/clients/{id}/contacts and PUT /{contactId} body (full-document
 *  replace on update — every field is written, none merged). */
export interface ClientContactInput {
  name: string;
  title: string;
  email?: string | null;
  phone?: string | null;
  notes?: string | null;
  isPrimary: boolean;
  receivesEmailReports: boolean;
  receivesAccrualsReports: boolean;
}

export interface ContractRecord {
  id: string;
  clientId: string;
  clientName: string;
  startDate: string;
  endDate: string | null;
  billingModel: BillingModel;
  ratePerRoundTripCad: number | null;
  budgetCode: string | null;
  billingFrequency: BillingFrequency;
  netTermsDays: number;
  defaultPoNumber: string | null;
  status: ContractStatus;
}

/** POST/PUT /api/clients/{id}/contracts body (netTermsDays defaults to 30 on
 *  the backend when omitted; ratePerRoundTripCad required iff RoundTripRate).
 *  Overlapping-period create/update → 409 Clients.Contract.OverlappingPeriod. */
export interface ContractInput {
  startDate: string;
  endDate?: string | null;
  billingModel: BillingModel;
  ratePerRoundTripCad?: number | null;
  budgetCode?: string | null;
  billingFrequency: BillingFrequency;
  netTermsDays: number;
  defaultPoNumber?: string | null;
}

export interface PurchaseOrderRecord {
  id: string;
  clientId: string;
  poNumber: string;
  issued: string;
  expiry: string | null;
  /** Authorised value of the PO — null when the client issued it open-ended. */
  amountCad: number | null;
  /** Per-PO round-trip rate. Null = inherit the contract's
   *  ratePerRoundTripCad (see poEffectiveTerms — the PO overrides, the
   *  contract is the fallback). Every trip bills this figure. */
  roundTripRateCad: number | null;
  /**
   * Per-PO one-way rate. **Retained for history; no longer priced.** Billing
   * charges one full round-trip rate for every group, paired or not, because a
   * lone leg still deadheads the vehicle back — so neither the invoice
   * generator (Backend InvoiceDraftBuilder) nor this app's estimator reads it.
   * The backend deliberately kept the column and its data, so the value is
   * still round-tripped on every PUT rather than cleared.
   */
  oneWayRateCad: number | null;
  note: string | null;
}

/** POST /api/clients/{id}/purchase-orders body. Omit (or null) roundTripRateCad
 *  to inherit the contract rate. Creating a PO from nothing but a number and a
 *  date is legitimate — the terms often arrive later — so everything else is
 *  optional. oneWayRateCad is history-only and no longer priced; the PO form no
 *  longer offers it, and a create simply sends null. */
export interface PurchaseOrderInput {
  poNumber: string;
  issued: string;
  expiry?: string | null;
  amountCad?: number | null;
  roundTripRateCad?: number | null;
  oneWayRateCad?: number | null;
  note?: string | null;
}

/**
 * PUT /api/clients/{id}/purchase-orders/{poId} body. Same fields as
 * PurchaseOrderInput, but NOTHING is optional — every key must be present, though
 * a value may be null.
 *
 * PUT replaces the whole purchase order, so a key left out is a field cleared.
 * The backend rejects a missing key with a 400 (UpdatePurchaseOrderRequest's
 * members are `required`), and this type is the compile-time half of that same
 * guarantee: a caller that forgets `roundTripRateCad` fails to typecheck instead
 * of wiping a negotiated rate at runtime. Clearing a term on purpose is an
 * explicit `null`.
 *
 * Why it matters more than it looks: a wiped rate does not error. Pricing falls
 * back to the contract rate and produces a plausible figure that gets hand-keyed
 * into QuickBooks — a wrong invoice that looks right.
 *
 * `oneWayRateCad` is no longer priced or edited, but it is still REQUIRED here
 * for exactly this reason: PUT replaces the document, so a form that stopped
 * sending the key would erase a recorded negotiation the backend chose to keep.
 * Callers round-trip the existing value unchanged.
 */
export interface PurchaseOrderUpdateInput {
  poNumber: string;
  issued: string;
  expiry: string | null;
  amountCad: number | null;
  roundTripRateCad: number | null;
  oneWayRateCad: number | null;
  note: string | null;
}

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------

export function listClients(): Promise<ClientRecord[]> {
  return request<ClientRecord[]>("/api/clients");
}

export function getClient(id: string): Promise<ClientRecord> {
  return request<ClientRecord>(`/api/clients/${id}`);
}

/** POST → 201 { id }. */
export async function createClient(input: ClientInput): Promise<string> {
  const res = await request<{ id: string }>("/api/clients", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateClient(id: string, input: ClientInput): Promise<void> {
  return request<void>(`/api/clients/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function listContracts(clientId: string): Promise<ContractRecord[]> {
  return request<ContractRecord[]>(`/api/clients/${clientId}/contracts`);
}

/** POST → 201 { id }. 409 Clients.Contract.OverlappingPeriod on overlap. */
export async function createContract(clientId: string, input: ContractInput): Promise<string> {
  const res = await request<{ id: string }>(`/api/clients/${clientId}/contracts`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

export function updateContract(
  clientId: string,
  contractId: string,
  input: ContractInput,
): Promise<void> {
  return request<void>(`/api/clients/${clientId}/contracts/${contractId}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function terminateContract(clientId: string, contractId: string): Promise<void> {
  return request<void>(`/api/clients/${clientId}/contracts/${contractId}/terminate`, {
    method: "POST",
  });
}

export function listContacts(clientId: string): Promise<ClientContactRecord[]> {
  return request<ClientContactRecord[]>(`/api/clients/${clientId}/contacts`);
}

/** POST → 201 { id }. 409 Clients.ClientContact.PrimaryAlreadyExists if primary exists. */
export async function createContact(clientId: string, input: ClientContactInput): Promise<string> {
  const res = await request<{ id: string }>(`/api/clients/${clientId}/contacts`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT → 204. Full-document replace. 404 unknown / wrong-client contact;
 *  409 Clients.ClientContact.PrimaryAlreadyExists when promoting to primary
 *  while another primary exists; 400 blank name/title. */
export function updateContact(
  clientId: string,
  contactId: string,
  input: ClientContactInput,
): Promise<void> {
  return request<void>(`/api/clients/${clientId}/contacts/${contactId}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function listPurchaseOrders(clientId: string): Promise<PurchaseOrderRecord[]> {
  return request<PurchaseOrderRecord[]>(`/api/clients/${clientId}/purchase-orders`);
}

/** POST → 201 { id }. */
export async function createPurchaseOrder(
  clientId: string,
  input: PurchaseOrderInput,
): Promise<string> {
  const res = await request<{ id: string }>(`/api/clients/${clientId}/purchase-orders`, {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT is a full replace and every key is required — see PurchaseOrderUpdateInput. */
export function updatePurchaseOrder(
  clientId: string,
  poId: string,
  input: PurchaseOrderUpdateInput,
): Promise<void> {
  return request<void>(`/api/clients/${clientId}/purchase-orders/${poId}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

export function deletePurchaseOrder(clientId: string, poId: string): Promise<void> {
  return request<void>(`/api/clients/${clientId}/purchase-orders/${poId}`, {
    method: "DELETE",
  });
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible. Shared helper lives in
// lib/api/drivers.ts (same backend pattern).
export { refetchUntil } from "./drivers";

// ---------------------------------------------------------------------------
// Display derivations — status colour NEVER stands alone (StatusChip pairs
// the colour with a glyph and text label).
// ---------------------------------------------------------------------------

/** Backend ServiceType enum → the console's theme service key (svcMeta). */
const SVC_BY_SERVICE_TYPE: Record<ClientServiceType, ServiceType> = {
  ContractCrew: "alamos",
  Community: "community",
  Nihb: "nihb",
  Charter: "charter",
  Cargo: "cargo",
  Grocery: "grocery",
};

export function svcForServiceType(serviceType: ClientServiceType | null): ServiceType {
  if (!serviceType) return "community";
  return SVC_BY_SERVICE_TYPE[serviceType] ?? "community";
}

export const CLIENT_TYPE_LABELS: Record<ClientType, string> = {
  Client: "Client",
  VendorPartner: "Vendor / Partner",
};

export const SERVICE_TYPE_LABELS: Record<ClientServiceType, string> = {
  ContractCrew: "Contract crew",
  Community: "Community",
  Nihb: "NIHB",
  Charter: "Charter",
  Cargo: "Cargo",
  Grocery: "Grocery",
};

export const BILLING_MODEL_LABELS: Record<BillingModel, string> = {
  RoundTripRate: "Per round trip",
  Manual: "Manual billing",
};

export const BILLING_FREQUENCY_LABELS: Record<BillingFrequency, string> = {
  Monthly: "Monthly",
  BiWeekly: "Bi-weekly",
  Weekly: "Weekly",
};

/** PO / contract expiry chip kind — same thresholds as the Fleet document
 *  chips (ontime / soon within 60d / over). */
export function poExpiryKindFor(expiry: string | null): StatusKind {
  return docStatusFor(expiry);
}

/** Contract renewal chip for a client row/header, derived from the active
 *  contract's end date (docStatusFor thresholds — never stored). */
export function renewalChipFor(contract: ActiveContractSummary | null): {
  kind: StatusKind;
  label: string;
} {
  if (!contract) return { kind: "off", label: "No contract" };
  if (!contract.endDate) return { kind: "ontime", label: "Evergreen" };
  const kind = docStatusFor(contract.endDate);
  if (kind === "over") return { kind, label: "Contract ended" };
  if (kind === "soon") {
    const days = Math.max(0, Math.ceil((new Date(contract.endDate).getTime() - Date.now()) / 86_400_000));
    return { kind, label: `Renews in ${days}d` };
  }
  return { kind, label: "Active" };
}

/** "2026-07-01 → 2027-06-30" / "2026-07-01 → evergreen". */
export function contractTermLabel(contract: ActiveContractSummary | ContractRecord): string {
  return `${contract.startDate} → ${contract.endDate ?? "evergreen"}`;
}

// Rates can carry cents (unlike the whole-dollar formatCad in lib/api.ts).
const rateFmt = new Intl.NumberFormat("en-CA", {
  style: "currency",
  currency: "CAD",
  minimumFractionDigits: 0,
  maximumFractionDigits: 2,
});

/** Rate line: formatted CAD per round trip, or the billing-model label. */
export function contractRateLabel(contract: ActiveContractSummary | ContractRecord): string {
  if (contract.billingModel === "RoundTripRate" && contract.ratePerRoundTripCad != null) {
    return `${rateFmt.format(contract.ratePerRoundTripCad)} / round trip`;
  }
  return BILLING_MODEL_LABELS[contract.billingModel];
}

/** The contract's usable round-trip rate, or null — the ONE place the
 *  "RoundTripRate model with a rate recorded" test is made. Manual billing and
 *  a RoundTripRate contract with no rate both read as null. */
export function contractRoundTripRateCad(
  contract: ActiveContractSummary | ContractRecord | null,
): number | null {
  if (!contract || contract.billingModel !== "RoundTripRate") return null;
  return contract.ratePerRoundTripCad;
}

// ---------------------------------------------------------------------------
// Per-PO pricing terms — the PINNED rate resolution, mirrored leg for leg by
// Backend/src/Billing/Domain/Invoices/InvoiceDraftBuilder.cs:
//
//   roundTripRate = po?.roundTripRateCad ?? contractRate
//
// ONE rate, because there is no one-way price any more: every priced group
// bills one full round-trip rate, quantity 1, paired or not (a lone leg still
// deadheads the vehicle back, so charging half was under-billing). The PO's
// oneWayRateCad is retained as history and is deliberately NOT read here.
//
// It lives HERE, beside the PurchaseOrderRecord it resolves, rather than in
// lib/billing/accruals.ts — accruals.ts already imports this module, so the
// reverse would close an import cycle. accruals.ts calls this function; the PO
// dashboard and the accruals report both render poTermsLabel() off it. One
// derivation: a rate the report prices from can never be worded differently by
// the screen that edits it.
// ---------------------------------------------------------------------------

export interface PoEffectiveTerms {
  /** Round-trip rate actually used, or null when neither PO nor contract has one.
   *  The ONLY rate there is — every trip bills it in full. */
  roundTripRateCad: number | null;
  /** True when the round-trip rate was inherited from the contract. */
  roundTripFromContract: boolean;
}

export function poEffectiveTerms(
  po: PurchaseOrderRecord | null,
  contractRateCad: number | null,
): PoEffectiveTerms {
  const roundTripRateCad = po?.roundTripRateCad ?? contractRateCad;
  const roundTripFromContract = po?.roundTripRateCad == null && contractRateCad != null;
  return { roundTripRateCad, roundTripFromContract };
}

/**
 * The EFFECTIVE terms of a PO as one line — never the stored field alone. A
 * blank rate that silently means "the contract's" is how a pricing bug reaches
 * an invoice, so an inherited figure prints with the reason it applies:
 *
 *   "$1,600 / round trip"
 *   "$1,450 / round trip (from contract)"
 *   "No round-trip rate on the PO or contract"
 *
 * One figure only: a trip bills the full round-trip rate whether or not its
 * return leg exists, so a one-way figure here would describe money nothing
 * charges.
 */
export function poTermsLabel(
  po: PurchaseOrderRecord | null,
  contractRateCad: number | null,
): string {
  const t = poEffectiveTerms(po, contractRateCad);
  return t.roundTripRateCad == null
    ? "No round-trip rate on the PO or contract"
    : `${rateFmt.format(t.roundTripRateCad)} / round trip${t.roundTripFromContract ? " (from contract)" : ""}`;
}

/** Is `serviceDate` inside the PO's [issued, expiry] window? A PO with no
 *  expiry is open-ended. Never blocking — the accruals report flags a breach
 *  and still prices the trip (owner's decision: warn, don't block). */
export function isWithinPoWindow(po: PurchaseOrderRecord, serviceDate: string): boolean {
  if (serviceDate < po.issued) return false;
  return po.expiry === null || serviceDate <= po.expiry;
}

export function contractStatusKindFor(status: ContractStatus): StatusKind {
  switch (status) {
    case "Active":
      return "info";
    case "Ended":
      return "off";
    case "Terminated":
    default:
      return "over";
  }
}

/** Contract history ordering — newest start date first. */
export function sortContracts(rows: ContractRecord[]): ContractRecord[] {
  return [...rows].sort((a, b) => (a.startDate < b.startDate ? 1 : -1));
}

/** POs soonest expiry first (no-expiry rows last), matching the old posFor. */
export function sortPurchaseOrders(rows: PurchaseOrderRecord[]): PurchaseOrderRecord[] {
  return [...rows].sort((a, b) => {
    if (a.expiry === b.expiry) return a.poNumber.localeCompare(b.poNumber);
    if (a.expiry === null) return 1;
    if (b.expiry === null) return -1;
    return a.expiry < b.expiry ? -1 : 1;
  });
}
