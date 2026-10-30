import type { StatusKind } from "@/lib/theme";
import {
  BUDGET_CODE_MAX_LENGTH,
  hasValidCodeFormat,
  normalizeBudgetCode,
  type VendorInput,
  type VendorRecord,
} from "@/lib/api/budgeting";

// Client-side mirrors of the vendor register's server rules
// (Backend/src/Budgeting/Domain/Vendors/Vendor.cs, VendorErrors.cs and
// Application/Vendors/VendorNameRule.cs). Pure, so lib/vendors.test.ts pins each one to the C#
// method it copies. The server re-checks everything and its answer is the one that counts — these
// exist so the common mistakes are explained before a round trip, in the server's own words.

/** Vendor.*MaxLength. Lengths are UTF-16 code units on both sides (C# string.Length). */
export const VENDOR_LIMITS = {
  /** Vendor.NameMaxLength = BudgetAllocation.VendorMaxLength. */
  name: 120,
  contactName: 120,
  /** RFC 5321 path limit. */
  email: 254,
  phone: 32,
  address: 500,
  notes: 2000,
  gstRegistrationNumber: 32,
  /** QuickBooks Online's own DisplayName limit. */
  qboDisplayName: 500,
  /** BudgetCode.CodeMaxLength. */
  defaultBudgetCode: BUDGET_CODE_MAX_LENGTH,
} as const;

/** VendorErrors' messages, verbatim — what the server would say, so client and server read alike. */
export const VENDOR_MESSAGES = {
  nameRequired: "A vendor needs a name.",
  nameTooLong: `The vendor name must be ${VENDOR_LIMITS.name} characters or fewer.`,
  contactNameTooLong: `The contact name must be ${VENDOR_LIMITS.contactName} characters or fewer.`,
  emailTooLong: `The email must be ${VENDOR_LIMITS.email} characters or fewer.`,
  emailInvalid: "The email must look like an address (name@example.com).",
  phoneTooLong: `The phone number must be ${VENDOR_LIMITS.phone} characters or fewer.`,
  addressTooLong: `The address must be ${VENDOR_LIMITS.address} characters or fewer.`,
  notesTooLong: `The notes must be ${VENDOR_LIMITS.notes} characters or fewer.`,
  gstRegistrationNumberTooLong: `The GST registration number must be ${VENDOR_LIMITS.gstRegistrationNumber} characters or fewer.`,
  qboDisplayNameTooLong: `The QuickBooks display name must be ${VENDOR_LIMITS.qboDisplayName} characters or fewer.`,
  defaultBudgetCodeTooLong: `The default budget code must be ${VENDOR_LIMITS.defaultBudgetCode} characters or fewer.`,
  defaultBudgetCodeInvalidFormat:
    "The default budget code may use letters, digits and hyphens only, and must start and end with a letter or digit (for example FLEET-MAINT).",
} as const;

/**
 * Mirrors Vendor.NormalizeName: trim + upper-invariant — the case-insensitive uniqueness key.
 * (JS toUpperCase and .NET ToUpperInvariant agree on every letter a vendor name realistically
 * carries; they differ on a handful of expanding cases such as "ß", where the server stays
 * authoritative and answers 409 itself.)
 */
export function normalizeVendorName(name: string | null | undefined): string {
  return (name ?? "").trim().toUpperCase();
}

/** Mirrors Vendor.Trimmed: blank optional text is null, never "". */
function trimmed(value: string | null | undefined): string | null {
  const t = (value ?? "").trim();
  return t.length === 0 ? null : t;
}

/**
 * Mirrors Vendor.LooksLikeEmail — deliberately loose: exactly one '@', something either side, no
 * whitespace (char.IsWhiteSpace ≈ /\s/). It catches a phone number typed into the email box; it
 * does not pretend to validate deliverability.
 */
export function looksLikeEmail(email: string): boolean {
  const at = email.indexOf("@");
  return at > 0 && at === email.lastIndexOf("@") && at < email.length - 1 && !/\s/.test(email);
}

/**
 * Mirrors Vendor.Normalize's rule order exactly — the first failure wins, as on the server — and
 * returns the server's message for it, or null when the input would be accepted. Every length is
 * checked on the TRIMMED value, and the default budget code on its normalized (trim + upper) form
 * via BudgetCode.NormalizeCode / HasValidCodeFormat (normalizeBudgetCode / hasValidCodeFormat).
 */
export function vendorError(input: VendorInput): string | null {
  const name = trimmed(input.name);
  if (name === null) return VENDOR_MESSAGES.nameRequired;
  if (name.length > VENDOR_LIMITS.name) return VENDOR_MESSAGES.nameTooLong;

  if ((trimmed(input.contactName)?.length ?? 0) > VENDOR_LIMITS.contactName) {
    return VENDOR_MESSAGES.contactNameTooLong;
  }

  const email = trimmed(input.email);
  if (email !== null && email.length > VENDOR_LIMITS.email) return VENDOR_MESSAGES.emailTooLong;
  if (email !== null && !looksLikeEmail(email)) return VENDOR_MESSAGES.emailInvalid;

  if ((trimmed(input.phone)?.length ?? 0) > VENDOR_LIMITS.phone) return VENDOR_MESSAGES.phoneTooLong;
  if ((trimmed(input.address)?.length ?? 0) > VENDOR_LIMITS.address) {
    return VENDOR_MESSAGES.addressTooLong;
  }
  if ((trimmed(input.notes)?.length ?? 0) > VENDOR_LIMITS.notes) return VENDOR_MESSAGES.notesTooLong;
  // Length only, by decision (Vendor.GstRegistrationNumber) — no checksum, no format, no rate.
  if ((trimmed(input.gstRegistrationNumber)?.length ?? 0) > VENDOR_LIMITS.gstRegistrationNumber) {
    return VENDOR_MESSAGES.gstRegistrationNumberTooLong;
  }
  if ((trimmed(input.qboDisplayName)?.length ?? 0) > VENDOR_LIMITS.qboDisplayName) {
    return VENDOR_MESSAGES.qboDisplayNameTooLong;
  }

  if (trimmed(input.defaultBudgetCode) !== null) {
    const code = normalizeBudgetCode(input.defaultBudgetCode ?? "");
    if (code.length > VENDOR_LIMITS.defaultBudgetCode) return VENDOR_MESSAGES.defaultBudgetCodeTooLong;
    if (!hasValidCodeFormat(code)) return VENDOR_MESSAGES.defaultBudgetCodeInvalidFormat;
  }

  return null;
}

/**
 * Mirrors VendorNameRule.EnsureUniqueAsync: another vendor — active OR retired — whose
 * normalized name equals this one, or null. A vendor never collides with itself (`exceptId`),
 * so re-casing its own name is allowed. A blank name collides with nothing (NameRequired comes
 * first on the server).
 */
export function findDuplicateVendor(
  vendors: readonly VendorRecord[],
  name: string,
  exceptId: string | null = null,
): VendorRecord | null {
  const key = normalizeVendorName(name);
  if (key.length === 0) return null;
  return vendors.find((v) => v.id !== exceptId && normalizeVendorName(v.name) === key) ?? null;
}

/** Mirrors VendorErrors.DuplicateName's two messages, so the pre-check reads like the 409. */
export function duplicateVendorMessage(existing: Pick<VendorRecord, "name" | "isActive">): string {
  return existing.isActive
    ? `A vendor named "${existing.name}" already exists. Vendor names are unique, ignoring case.`
    : `A retired vendor named "${existing.name}" already exists. Reactivate it instead of adding it again.`;
}

/** The status chip for a vendor — always a StatusChip, so colour never stands alone. */
export function vendorStatus(isActive: boolean): { kind: StatusKind; label: string } {
  return isActive ? { kind: "ontime", label: "Active" } : { kind: "off", label: "Retired" };
}

/**
 * The list's search: case-insensitive substring over the fields a person would search by —
 * name, QuickBooks name, contact, email, phone and default code. A blank query matches everything.
 */
export function vendorMatchesSearch(vendor: VendorRecord, query: string): boolean {
  const q = query.trim().toLowerCase();
  if (q.length === 0) return true;
  return [
    vendor.name,
    vendor.qboDisplayName,
    vendor.contactName,
    vendor.email,
    vendor.phone,
    vendor.defaultBudgetCode,
  ].some((field) => field?.toLowerCase().includes(q) ?? false);
}

/** The form's raw strings — every field a text box, "" when blank. */
export type VendorDraft = { [K in keyof VendorInput]: string };

export const BLANK_VENDOR_DRAFT: VendorDraft = {
  name: "",
  contactName: "",
  email: "",
  phone: "",
  address: "",
  notes: "",
  gstRegistrationNumber: "",
  qboDisplayName: "",
  defaultBudgetCode: "",
};

export function vendorToDraft(v: VendorRecord): VendorDraft {
  return {
    name: v.name,
    contactName: v.contactName ?? "",
    email: v.email ?? "",
    phone: v.phone ?? "",
    address: v.address ?? "",
    notes: v.notes ?? "",
    gstRegistrationNumber: v.gstRegistrationNumber ?? "",
    qboDisplayName: v.qboDisplayName ?? "",
    defaultBudgetCode: v.defaultBudgetCode ?? "",
  };
}

/**
 * Draft → the VendorRequest body: EVERY key, trimmed, blank as null — because PUT is a full
 * replace and an omitted key clears the field. The default code goes normalized, as it will be
 * stored, so the refetch predicate below can compare like with like.
 */
export function draftToVendorInput(d: VendorDraft): VendorInput {
  const code = trimmed(d.defaultBudgetCode);
  return {
    name: d.name.trim(),
    contactName: trimmed(d.contactName),
    email: trimmed(d.email),
    phone: trimmed(d.phone),
    address: trimmed(d.address),
    notes: trimmed(d.notes),
    gstRegistrationNumber: trimmed(d.gstRegistrationNumber),
    qboDisplayName: trimmed(d.qboDisplayName),
    defaultBudgetCode: code === null ? null : normalizeBudgetCode(code),
  };
}

/**
 * The post-save refetch predicate: the read row carries every value just written. On edit the
 * row was always there, so waiting for the id alone would settle on the stale projection.
 */
export function vendorReflects(record: VendorRecord, input: VendorInput): boolean {
  return (Object.keys(input) as (keyof VendorInput)[]).every((k) => record[k] === input[k]);
}
