import { request } from "../api";
import { docStatusFor } from "../maintenanceStore";
import type { ServiceType, StatusKind } from "../theme";

// ---------------------------------------------------------------------------
// Clients API client — contract owned by Backend/ (Clients module,
// ClientsEndpoints). Shapes mirror the backend's ClientResponse /
// ContractResponse / PurchaseOrderResponse exactly (JSON camelCase, enums as
// PascalCase strings). Do not invent fields — extend only when the backend
// contract changes.
// CRM (contacts, interactions, follow-ups) has NO backend — it stays mocked
// in lib/clientStore.ts.
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
  gstApplicable: boolean;
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
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST /api/clients/{id}/contacts body. */
export interface ClientContactInput {
  name: string;
  title: string;
  email?: string | null;
  phone?: string | null;
  notes?: string | null;
  isPrimary: boolean;
}

export interface ContractRecord {
  id: string;
  clientId: string;
  clientName: string;
  startDate: string;
  endDate: string | null;
  billingModel: BillingModel;
  ratePerRoundTripCad: number | null;
  gstApplicable: boolean;
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
  gstApplicable: boolean;
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
  amountCad: number | null;
  note: string | null;
}

/** POST/PUT /api/clients/{id}/purchase-orders body. */
export interface PurchaseOrderInput {
  poNumber: string;
  issued: string;
  expiry?: string | null;
  amountCad?: number | null;
  note?: string | null;
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

export function updatePurchaseOrder(
  clientId: string,
  poId: string,
  input: PurchaseOrderInput,
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
