// Vendor shares on budget items — the client mirror of the server's rules, so the common mistake
// is caught without a round trip and in the server's own words. The server re-checks all of it
// and its answer is the one that counts.
//
// An EXPENSE item names its vendors as dollar shares — each a register vendor with an amount —
// that, when there are any, sum to EXACTLY the item's amount (no partial splits; none at all is
// "No vendor"). A REVENUE item has no shares; its free-text `vendor` is the payer. Changing an
// item's amount without restating its shares is refused, never rebalanced — so nothing here
// rebalances either: `assignRemaining` and `splitEvenly` run only when the planner clicks for them.
//
// Mirrors (Backend/src/Budgeting/...):
//   - BudgetItemVendorShare.Create          → each share's vendor + amount rule
//   - BudgetAllocation.Parse (shares block) → count, each share, duplicates, then the sum
//   - BudgetAllocation.CheckCategory        → no shares on revenue, no new payer on expense
//   - BudgetItemVendorRule                  → added vendors must exist and be active
//   - BudgetAllocationErrors                → every message, verbatim
//
// Self-contained on purpose: lib/api/budgeting.ts imports this module (budgetItemError runs the
// shares rules in the server's order), so this one imports only TYPES from it — a value import
// back would be a module cycle.
//
// Money is handled in INTEGER CENTS throughout. A float sum of 0.1 + 0.2 is 0.30000000000000004,
// and the server compares the shares' decimal sum to the amount to the cent — a client that summed
// in floats would call a correct split wrong (or a wrong one right).

import type { BudgetCodeCategory } from "./types";
import { formatCadPrecise } from "./money";
import type {
  BudgetItemVendorShareInput,
  BudgetItemVendorShareRecord,
  VendorRecord,
} from "./api/budgeting";

/** Mirrors BudgetAllocation.MaxVendorShares. */
export const MAX_VENDOR_SHARES = 20;

/** Mirrors BudgetAllocation.AmountMax — the inclusive numeric(12,2) ceiling a share is held to. */
export const VENDOR_SHARE_AMOUNT_MAX = 999_999_999.99;

/** The server's messages, verbatim, keyed by the error code's suffix ("Budgeting.Allocation."). */
export const VENDOR_SHARE_MESSAGES = {
  VendorShareVendorRequired: "Every vendor share must name a vendor from the vendor register.",
  VendorShareAmountInvalid:
    "Each vendor share must be more than zero, in dollars and cents (at most two decimal places), and 999,999,999.99 or less.",
  DuplicateVendorShare:
    "A vendor can appear only once on a budget item. Combine its shares into one amount.",
  TooManyVendorShares: "A budget item can be split across at most 20 vendors.",
  VendorSharesNotAllowedForRevenue:
    "A revenue item has no vendor shares — vendors are who the business pays. Name the payer in the vendor (payer) field instead, and send no vendor shares.",
  PayerNotAllowedForExpense:
    "The free-text vendor is the payer on a revenue item. On an expense item, choose vendors from the vendor register as vendor shares instead, and leave the free-text vendor blank.",
  VendorNotFound:
    "That vendor is not in the vendor register. Add it to the register first, or choose an existing vendor.",
} as const;

/** Mirrors BudgetAllocationErrors.VendorSharesMustSumToAmount — built per request, naming both totals. */
export function vendorSharesMustSumMessage(sharesTotalCad: number, amountCad: number): string {
  return (
    `The vendor shares add up to ${money(sharesTotalCad)}, but the item's amount is ${money(amountCad)}. ` +
    "The shares must cover the whole amount exactly — adjust them, or remove every share to leave the item with no vendor."
  );
}

/** Mirrors BudgetAllocationErrors.VendorInactive (409) — only for a vendor newly added to the item. */
export function vendorInactiveMessage(vendorName: string): string {
  return `"${vendorName}" is a retired vendor and cannot be added to a budget item. Choose an active vendor, or restore it in the vendor register first.`;
}

// ---------------------------------------------------------------------------
// Cents
// ---------------------------------------------------------------------------

/**
 * Shifts the decimal point two places in the number's own shortest decimal spelling — "10.005"
 * becomes "10.005e2", which parses to exactly 1000.5. Never `value * 100`: `1.005 * 100` is
 * 100.49999999999999. The same digits JSON.stringify puts on the wire, so the same digits the
 * server's decimal parses.
 */
function shiftCents(value: number): number {
  const abs = Math.abs(value);
  const [mantissa, exponent] = String(abs).split("e");
  const shifted = Number(`${mantissa}e${Number(exponent ?? 0) + 2}`);
  return value < 0 ? -shifted : shifted;
}

/**
 * A figure as exact integer cents, or null when it has more than two decimal places. Mirrors the
 * share rule's `decimal.Round(amount, 2) != amount` check: 10.005 is not rounded, it is refused.
 */
export function exactCents(value: number): number | null {
  if (!Number.isFinite(value)) return null;
  const cents = shiftCents(value);
  return Number.isInteger(cents) ? cents : null;
}

/** A figure as integer cents, rounded half away from zero (BudgetAllocation.Round) — for display sums. */
export function roundedCents(value: number): number {
  if (!Number.isFinite(value)) return 0;
  const shifted = shiftCents(value);
  const rounded = Math.round(Math.abs(shifted));
  return shifted < 0 && rounded !== 0 ? -rounded : rounded;
}

/** Cents → a plain dollars number (exact for every cents value this app can hold). */
export function centsToCad(cents: number): number {
  return cents / 100;
}

/** Cents → "1234.50", the text a number field is given (never "1234.5"). */
export function centsToFieldText(cents: number): string {
  const negative = cents < 0;
  const abs = Math.abs(cents);
  const text = `${Math.trunc(abs / 100)}.${String(abs % 100).padStart(2, "0")}`;
  return negative ? `-${text}` : text;
}

/** Mirrors the C# "#,0.00" invariant format the server's sum message uses. */
function money(value: number): string {
  const cents = roundedCents(value);
  const negative = cents < 0;
  const abs = Math.abs(cents);
  const whole = Math.trunc(abs / 100)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, ",");
  return `${negative ? "-" : ""}${whole}.${String(abs % 100).padStart(2, "0")}`;
}

// ---------------------------------------------------------------------------
// Rules
// ---------------------------------------------------------------------------

/**
 * The first shape rule these shares break, as the server's message, or null. Mirrors the shares
 * block of BudgetAllocation.Parse IN ITS ORDER: the count first (before any share is read), then
 * each share in turn (its vendor, then its amount — BudgetItemVendorShare.Create), a duplicate
 * vendor the moment it repeats, and only after every share passed, the sum against the item's
 * (computed, rounded) amount. Exact cents throughout, so the sum can never be off by float error.
 *
 * `amountCad` is the amount the SERVER will store — itemAmount(input), already rounded.
 */
export function vendorSharesShapeError(
  shares: readonly BudgetItemVendorShareInput[],
  amountCad: number,
): string | null {
  const m = VENDOR_SHARE_MESSAGES;
  if (shares.length > MAX_VENDOR_SHARES) return m.TooManyVendorShares;

  const seen = new Set<string>();
  let totalCents = 0;
  for (const share of shares) {
    if (!share.vendorId) return m.VendorShareVendorRequired;
    const cents = share.amountCad === null ? null : exactCents(share.amountCad);
    if (cents === null || cents <= 0 || share.amountCad! > VENDOR_SHARE_AMOUNT_MAX) {
      return m.VendorShareAmountInvalid;
    }
    if (seen.has(share.vendorId)) return m.DuplicateVendorShare;
    seen.add(share.vendorId);
    totalCents += cents;
  }

  if (shares.length > 0) {
    const amountCents = roundedCents(amountCad);
    if (totalCents !== amountCents) {
      return vendorSharesMustSumMessage(centsToCad(totalCents), centsToCad(amountCents));
    }
  }
  return null;
}

/**
 * The category rule, mirroring BudgetAllocation.CheckCategory as it applies on CREATE (every
 * field counts as changed): a revenue item may carry no shares; an expense item may carry no
 * free-text payer. On update the server applies it only to what the write changes — this app
 * never trips it either way, because it sends `[]` for revenue and a null payer for expense.
 */
export function vendorSharesCategoryError(
  shares: readonly BudgetItemVendorShareInput[],
  payer: string | null,
  category: BudgetCodeCategory,
): string | null {
  if (category === "Revenue") {
    return shares.length > 0 ? VENDOR_SHARE_MESSAGES.VendorSharesNotAllowedForRevenue : null;
  }
  return payer !== null && payer.trim() !== "" ? VENDOR_SHARE_MESSAGES.PayerNotAllowedForExpense : null;
}

/**
 * The shares' first problem for an item of this amount and category, in the server's order —
 * the shape (Parse) first, then the category (Create/Update). Null when the server would accept
 * them as far as the item itself can tell (the register check is vendorRegisterError).
 */
export function vendorSharesError(
  shares: readonly BudgetItemVendorShareInput[],
  amountCad: number,
  category: BudgetCodeCategory,
  payer: string | null = null,
): string | null {
  return vendorSharesShapeError(shares, amountCad) ?? vendorSharesCategoryError(shares, payer, category);
}

/**
 * Mirrors BudgetItemVendorRule: only vendors the write ADDS (not already among the item's
 * `current` shares) are looked up, in request order — no such vendor → VendorNotFound, a retired
 * one → VendorInactive. A share the item already had passes untouched, so retiring a vendor never
 * freezes the items planned against it. Revenue skips the register entirely. Returns null when
 * the register has not loaded — there is nothing to check against, and the server decides.
 */
export function vendorRegisterError(
  shares: readonly BudgetItemVendorShareInput[],
  current: readonly Pick<BudgetItemVendorShareRecord, "vendorId">[],
  register: readonly Pick<VendorRecord, "id" | "name" | "isActive">[] | null,
  category: BudgetCodeCategory,
): string | null {
  if (category === "Revenue" || register === null) return null;
  const existing = new Set(current.map((s) => s.vendorId));
  const byId = new Map(register.map((v) => [v.id, v]));
  for (const share of shares) {
    if (!share.vendorId || existing.has(share.vendorId)) continue;
    const vendor = byId.get(share.vendorId);
    if (!vendor) return VENDOR_SHARE_MESSAGES.VendorNotFound;
    if (!vendor.isActive) return vendorInactiveMessage(vendor.name);
  }
  return null;
}

// ---------------------------------------------------------------------------
// The modal's arithmetic — explicit, never automatic
// ---------------------------------------------------------------------------

/**
 * Amount minus the shares, in exact cents → dollars. Positive: still to assign; negative: the
 * shares go over. A share with no amount yet counts as zero; the amount itself is rounded to
 * cents as the server stores it.
 */
export function remainingCad(
  amountCad: number,
  shares: readonly { amountCad: number | null }[],
): number {
  return centsToCad(remainingCents(amountCad, shares));
}

/** remainingCad in cents. */
export function remainingCents(
  amountCad: number,
  shares: readonly { amountCad: number | null }[],
): number {
  const assigned = shares.reduce((sum, s) => sum + (s.amountCad === null ? 0 : roundedCents(s.amountCad)), 0);
  return roundedCents(amountCad) - assigned;
}

/**
 * `count` cents-exact shares of `amountCad` that sum to it exactly: the remainder's odd cents go
 * one each to the first rows ($100 / 3 → 33.34, 33.33, 33.33). Empty for a count below 1.
 */
export function splitEvenly(amountCad: number, count: number): number[] {
  if (count < 1) return [];
  const total = roundedCents(amountCad);
  const base = Math.trunc(total / count);
  const extra = total - base * count;
  return Array.from({ length: count }, (_, i) => centsToCad(base + (i < extra ? 1 : 0)));
}

/**
 * The row's amount after "Assign remaining to this vendor": its current amount (blank = 0) plus
 * what is left, in exact cents. Null when there is nothing left to assign (fully assigned or over)
 * — the planner fixes an over-assignment by hand; nothing is taken from a row they did not touch.
 */
export function assignRemaining(
  amountCad: number,
  shares: readonly { amountCad: number | null }[],
  index: number,
): number | null {
  const left = remainingCents(amountCad, shares);
  if (left <= 0 || index < 0 || index >= shares.length) return null;
  const own = shares[index].amountCad;
  return centsToCad((own === null ? 0 : roundedCents(own)) + left);
}

// ---------------------------------------------------------------------------
// Presentation
// ---------------------------------------------------------------------------

/** One share as a list shows it. `name` is null only while the vendor's read row catches up. */
export interface ShareSummaryEntry {
  vendorId: string;
  name: string | null;
  retired: boolean;
  amountCad: number;
}

/**
 * The first `max` shares (the server keeps the planner's order) and how many more there are —
 * for "Kal Tire $1,200 · Esso $300 +2 more" on an item row. A share whose vendor has no read row
 * yet (name null) is not called retired: its isActive is false only because it is unresolved.
 */
export function shareSummary(
  shares: readonly BudgetItemVendorShareRecord[],
  max = 2,
): { shown: ShareSummaryEntry[]; more: number } {
  const shown = shares.slice(0, Math.max(0, max)).map((s) => ({
    vendorId: s.vendorId,
    name: s.vendorName,
    retired: s.vendorName !== null && !s.isActive,
    amountCad: s.amountCad,
  }));
  return { shown, more: Math.max(0, shares.length - shown.length) };
}

/** What a share row is called when its vendor's name has not reached the read model yet. */
export const PENDING_VENDOR_NAME = "Vendor (name loading)";

/**
 * The vendor fact on an item row of the dashboard. Revenue: "Payer: Alamos Gold" (null without
 * one). Expense: the first two shares with their amounts, a retired vendor written out as such,
 * then "+N more" — "Kal Tire $1,200 · Esso (retired) $300 · +2 more". An expense item with no
 * shares but legacy free text (a $0 item the backfill could not split) shows that text marked as
 * free text; with neither, null — the row says nothing rather than "No vendor".
 */
export function itemVendorText(
  category: BudgetCodeCategory,
  vendor: string | null,
  shares: readonly BudgetItemVendorShareRecord[],
  max = 2,
): string | null {
  if (category === "Revenue") return vendor ? `Payer: ${vendor}` : null;
  if (shares.length === 0) return vendor ? `${vendor} (free text)` : null;
  const { shown, more } = shareSummary(shares, max);
  const parts = shown.map(
    (s) => `${s.name ?? PENDING_VENDOR_NAME}${s.retired ? " (retired)" : ""} ${formatCadPrecise(s.amountCad)}`,
  );
  if (more > 0) parts.push(`+${more} more`);
  return parts.join(" · ");
}
