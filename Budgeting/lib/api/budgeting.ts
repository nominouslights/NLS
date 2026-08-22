import { request } from "./transport";
import type {
  BudgetCode,
  BudgetCodeCategory,
  BudgetPeriod,
  BudgetReviewFrequency,
  BudgetServiceLine,
  BudgetTaxTreatment,
  PeriodState,
} from "@/lib/types";
import type { StatusKind } from "@/lib/theme";

// ---------------------------------------------------------------------------
// Budgeting API client — contract owned by Backend/ (Budgeting module,
// BudgetingEndpoints.cs). Shapes mirror the backend's BudgetPeriodResponse and
// the CreateBudgetPeriodRequest record exactly (JSON camelCase; enums as
// PascalCase strings). The server derives startsOn/endsOn/label from
// granularity + year + ordinal — nothing here invents a date range. Do not
// invent fields — extend only when the backend contract changes.
// ---------------------------------------------------------------------------

/** Wire enum (PascalCase, JsonStringEnumConverter server-side). */
export type PeriodGranularity = "Month" | "Quarter";

/** Mirrors BudgetPeriodResponse — list rows come from rm_budget_periods. */
export interface BudgetPeriodRecord {
  id: string;
  label: string;
  granularity: PeriodGranularity;
  year: number;
  ordinal: number;
  /** ISO date, inclusive. */
  startsOn: string;
  /** ISO date, inclusive. */
  endsOn: string;
  state: PeriodState;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** POST /api/budgeting/periods body (CreateBudgetPeriodRequest). */
export interface BudgetPeriodInput {
  granularity: PeriodGranularity;
  year: number;
  /** 1–12 for Month, 1–4 for Quarter. */
  ordinal: number;
}

/** Ordered by startsOn ascending server-side. */
export function listBudgetPeriods(): Promise<BudgetPeriodRecord[]> {
  return request<BudgetPeriodRecord[]>("/api/budgeting/periods");
}

/** POST → 201 { id } (id only; the row lands on the next projection read). */
export async function createBudgetPeriod(input: BudgetPeriodInput): Promise<string> {
  const res = await request<{ id: string }>("/api/budgeting/periods", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

// Reads are eventually consistent projections — after a mutation, refetch with
// a short retry until the change is visible.
export { refetchUntil } from "./shared";

/**
 * Period state → status kind, in the data-source file for the same reason
 * varianceKind lives in lib/data.ts: the mapping is decided once, next to the
 * data, so every screen agrees. A locked period is finished ("off"), an open
 * one is the live plan ("ontime"), a draft is informational ("info").
 */
export function periodKind(state: PeriodState): StatusKind {
  if (state === "Locked") return "off";
  if (state === "Open") return "ontime";
  return "info";
}

/**
 * Wire record → the view shape the screens already render. planned/allocated
 * are not on the wire yet — they belong to the allocations story — so real
 * periods carry honest zeros until allocations exist to sum.
 */
export function toBudgetPeriod(r: BudgetPeriodRecord): BudgetPeriod {
  return {
    id: r.id,
    label: r.label,
    startsOn: r.startsOn,
    endsOn: r.endsOn,
    state: r.state,
    pk: periodKind(r.state),
    planned: 0,
    allocated: 0,
  };
}

/** English month names, matching the backend's invariant-culture labels. */
export const MONTH_NAMES: string[] = [
  "January", "February", "March", "April", "May", "June",
  "July", "August", "September", "October", "November", "December",
];

/**
 * Client-side mirror of the server's derivation, for the modal's live preview
 * line only — the server result is what gets stored. Returns null when the
 * inputs are out of range (year 2020–2100; ordinal 1–12 / 1–4), which the
 * modal treats as "not submittable yet".
 */
export function previewPeriod(
  granularity: PeriodGranularity,
  year: number,
  ordinal: number,
): { label: string; startsOn: string; endsOn: string } | null {
  if (!Number.isInteger(year) || year < 2020 || year > 2100) return null;
  if (!Number.isInteger(ordinal)) return null;

  const iso = (y: number, m: number, d: number) =>
    `${y}-${String(m).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
  // Day 0 of the next month = the last day of this one. Local Date math only —
  // toISOString would shift the day west of UTC (see lib/period.ts).
  const lastDay = (y: number, m: number) => new Date(y, m, 0).getDate();

  if (granularity === "Month") {
    if (ordinal < 1 || ordinal > 12) return null;
    return {
      label: `${MONTH_NAMES[ordinal - 1]} ${year}`,
      startsOn: iso(year, ordinal, 1),
      endsOn: iso(year, ordinal, lastDay(year, ordinal)),
    };
  }

  if (ordinal < 1 || ordinal > 4) return null;
  const firstMonth = (ordinal - 1) * 3 + 1;
  const endMonth = firstMonth + 2;
  return {
    label: `FY${year} Q${ordinal}`,
    startsOn: iso(year, firstMonth, 1),
    endsOn: iso(year, endMonth, lastDay(year, endMonth)),
  };
}

// ---------------------------------------------------------------------------
// Budget codes — the chart of accounts every dollar is tagged to. Mirrors the
// backend's BudgetCodeResponse and the Create/UpdateBudgetCodeRequest records.
// ---------------------------------------------------------------------------

/** Mirrors BudgetCodeResponse — list rows come from rm_budget_codes. */
export interface BudgetCodeRecord {
  id: string;
  code: string;
  name: string;
  description: string | null;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  costCentre: string | null;
  parentCodeId: string | null;
  /** Resolved server-side from parentCodeId on every read, so it cannot go stale. */
  parentCode: string | null;
  parentName: string | null;
  glAccountCode: string | null;
  taxTreatment: BudgetTaxTreatment | null;
  budgetOwnerUserId: string | null;
  budgetOwnerEmail: string | null;
  reviewFrequency: BudgetReviewFrequency;
  isActive: boolean;
  createdBy: string | null;
  createdByEmail: string | null;
  modifiedBy: string | null;
  modifiedByEmail: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/**
 * POST /api/budgeting/codes body (CreateBudgetCodeRequest). Optional fields go on the wire as
 * null rather than being omitted, so a cleared field reads as "cleared" and not "unchanged".
 */
export interface BudgetCodeInput {
  code: string;
  name: string;
  description: string | null;
  category: BudgetCodeCategory;
  serviceLine: BudgetServiceLine | null;
  costCentre: string | null;
  parentCodeId: string | null;
  glAccountCode: string | null;
  taxTreatment: BudgetTaxTreatment | null;
  budgetOwnerUserId: string | null;
  reviewFrequency: BudgetReviewFrequency;
}

/**
 * PUT /api/budgeting/codes/{id} body (UpdateBudgetCodeRequest). No `code`: the code string is
 * set once at creation and is not renameable server-side, because allocations and actuals
 * reference it by string. A mistyped code is retire-and-recreate, not a rename.
 */
export type BudgetCodeUpdateInput = Omit<BudgetCodeInput, "code">;

/** Mirrors BudgetOwnerOptionResponse — the owner picker's options. */
export interface BudgetOwnerOption {
  userId: string;
  /** Identity has no name field, so email is the only human-readable identifier. */
  email: string;
  role: string;
}

/** Ordered by code ascending server-side. Includes retired codes. */
export function listBudgetCodes(): Promise<BudgetCodeRecord[]> {
  return request<BudgetCodeRecord[]>("/api/budgeting/codes");
}

/** The tenant's users, from Budgeting's replica of Identity's accounts. */
export function listBudgetOwnerCandidates(): Promise<BudgetOwnerOption[]> {
  return request<BudgetOwnerOption[]>("/api/budgeting/codes/owners");
}

/** POST → 201 { id } (id only; the row lands on the next projection read). */
export async function createBudgetCode(input: BudgetCodeInput): Promise<string> {
  const res = await request<{ id: string }>("/api/budgeting/codes", {
    method: "POST",
    body: JSON.stringify(input),
  });
  return res.id;
}

/** PUT → 204. */
export function updateBudgetCode(id: string, input: BudgetCodeUpdateInput): Promise<void> {
  return request<void>(`/api/budgeting/codes/${id}`, {
    method: "PUT",
    body: JSON.stringify(input),
  });
}

/**
 * POST → 204. Two routes rather than a body flag, matching the backend: retiring a code is a
 * flag flip, never a delete, so last period's allocations keep resolving.
 */
export function setBudgetCodeActive(id: string, active: boolean): Promise<void> {
  return request<void>(`/api/budgeting/codes/${id}/${active ? "activate" : "deactivate"}`, {
    method: "POST",
  });
}

/**
 * DELETE → 204, or 409 when the code has children or has ever been used. Retirement is the
 * normal path; this is only for a code created in error. The server's 409 message names
 * retirement as the alternative, so surfacing it verbatim is the right handling.
 */
export function deleteBudgetCode(id: string): Promise<void> {
  return request<void>(`/api/budgeting/codes/${id}`, { method: "DELETE" });
}

/** POST → 200 { created }. Idempotent: a second call creates nothing and returns 0. */
export function seedStarterBudgetCodes(): Promise<{ created: number }> {
  return request<{ created: number }>("/api/budgeting/codes/starter-set", { method: "POST" });
}

/**
 * Client-side mirror of the server's code normalization (BudgetCode.NormalizeCode), for the
 * modal's live preview only — the server result is what gets stored. Trim + upper case, so a
 * planner typing "fleet-maint" sees the "FLEET-MAINT" that will actually be saved.
 */
export function normalizeBudgetCode(code: string): string {
  return code.trim().toUpperCase();
}

/**
 * Whether a code string can be saved at all, mirroring BudgetCode.ValidateCode: 1–32 characters,
 * letters/digits/hyphens only, no leading or trailing hyphen. Client-side so the modal can
 * explain the problem before a round trip; the server re-checks and is authoritative.
 */
export const BUDGET_CODE_MAX_LENGTH = 32;

export function budgetCodeFormatError(code: string): string | null {
  const normalized = normalizeBudgetCode(code);
  if (normalized.length === 0) return "Enter a code.";
  if (normalized.length > BUDGET_CODE_MAX_LENGTH) {
    return `The code must be ${BUDGET_CODE_MAX_LENGTH} characters or fewer.`;
  }
  if (!/^[A-Z0-9]([A-Z0-9-]*[A-Z0-9])?$/.test(normalized)) {
    return "Use letters, digits and hyphens only, starting and ending with a letter or digit.";
  }
  return null;
}

/**
 * Category → status kind, here rather than in the screen for the same reason as periodKind: the
 * mapping is decided once, next to the data, so every screen agrees. Revenue is money coming in
 * ("ontime"); an expense is neither good nor bad on its own, so it stays informational ("info").
 * Retirement is rendered separately — an inactive code shows its own "off" chip beside this one.
 */
export function budgetCodeCategoryKind(category: BudgetCodeCategory): StatusKind {
  return category === "Revenue" ? "ontime" : "info";
}

// Label maps, typed as Record<Union, string> on purpose: adding a member to one of the wire
// unions then becomes a compile error here rather than a blank cell at runtime. These are the
// highest-drift-risk lines in the app.

export const SERVICE_LINE_LABELS: Record<BudgetServiceLine, string> = {
  ContractCrew: "Mine crew shuttle",
  Community: "Community passenger",
  Nihb: "NIHB medical",
  Charter: "Charter",
  Cargo: "Parcel / cargo",
  Grocery: "Grocery run",
  Fleet: "Fleet",
  Administrative: "Administrative",
  Apprenticeship: "Apprenticeship",
};

export const TAX_TREATMENT_LABELS: Record<BudgetTaxTreatment, string> = {
  GstApplicable: "GST applicable (5%)",
  ZeroRated: "Zero-rated",
  Exempt: "Exempt",
  NotApplicable: "N/A",
};

export const REVIEW_FREQUENCY_LABELS: Record<BudgetReviewFrequency, string> = {
  Monthly: "Monthly",
  Quarterly: "Quarterly",
  Annual: "Annual",
};

/**
 * The codes that may legally be picked as a parent, mirroring BudgetCodeParentRule: never the
 * code being edited, and never a code that already has a parent (the hierarchy is one level
 * deep). Pure and exported so it can be unit-tested without a DOM — and so the picker cannot
 * offer an option the server will reject.
 */
export function parentCandidates(codes: BudgetCode[], editingId: string | null): BudgetCode[] {
  return codes.filter((c) => c.id !== editingId && c.parentCodeId === null);
}

/** Wire record → the view shape the screens render. The only rename is isActive → active. */
export function toBudgetCode(r: BudgetCodeRecord): BudgetCode {
  return {
    id: r.id,
    code: r.code,
    name: r.name,
    description: r.description,
    category: r.category,
    serviceLine: r.serviceLine,
    costCentre: r.costCentre,
    parentCodeId: r.parentCodeId,
    parentCode: r.parentCode,
    parentName: r.parentName,
    glAccountCode: r.glAccountCode,
    taxTreatment: r.taxTreatment,
    budgetOwnerUserId: r.budgetOwnerUserId,
    budgetOwnerEmail: r.budgetOwnerEmail,
    reviewFrequency: r.reviewFrequency,
    active: r.isActive,
    createdByEmail: r.createdByEmail,
    modifiedByEmail: r.modifiedByEmail,
  };
}
