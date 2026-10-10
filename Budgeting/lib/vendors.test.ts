import { describe, expect, it } from "vitest";
import { hasValidCodeFormat, type VendorInput, type VendorRecord } from "@/lib/api/budgeting";
import {
  VENDOR_LIMITS,
  VENDOR_MESSAGES,
  draftToVendorInput,
  duplicateVendorMessage,
  findDuplicateVendor,
  looksLikeEmail,
  normalizeVendorName,
  vendorError,
  vendorMatchesSearch,
  vendorReflects,
  vendorStatus,
  vendorToDraft,
} from "@/lib/vendors";

// Each function here mirrors a server rule in Backend/src/Budgeting — the C# method is named in
// every describe. Change the server rule, change this file: it is the only thing keeping the
// client-side copy honest.

const VALID: VendorInput = {
  name: "Kal Tire Thompson",
  contactName: null,
  email: null,
  phone: null,
  address: null,
  notes: null,
  gstRegistrationNumber: null,
  qboDisplayName: null,
  defaultBudgetCode: null,
};

const with_ = (over: Partial<VendorInput>): VendorInput => ({ ...VALID, ...over });

function vendor(id: string, name: string, isActive = true, over: Partial<VendorRecord> = {}): VendorRecord {
  return {
    id,
    name,
    contactName: null,
    email: null,
    phone: null,
    address: null,
    notes: null,
    gstRegistrationNumber: null,
    qboDisplayName: null,
    defaultBudgetCode: null,
    isActive,
    createdBy: null,
    modifiedBy: null,
    createdAtUtc: "2026-10-01T00:00:00+00:00",
    updatedAtUtc: "2026-10-01T00:00:00+00:00",
    ...over,
  };
}

describe("VENDOR_LIMITS — Vendor.*MaxLength", () => {
  it("matches the aggregate's constants", () => {
    expect(VENDOR_LIMITS).toEqual({
      name: 120, // = BudgetAllocation.VendorMaxLength
      contactName: 120,
      email: 254,
      phone: 32,
      address: 500,
      notes: 2000,
      gstRegistrationNumber: 32,
      qboDisplayName: 500,
      defaultBudgetCode: 32, // = BudgetCode.CodeMaxLength
    });
  });
});

describe("vendorError — Vendor.Normalize, rule for rule, with VendorErrors' messages", () => {
  it("accepts a name and nothing else", () => {
    expect(vendorError(VALID)).toBeNull();
  });

  it.each(["", "   "])("refuses a blank name (%j) — NameRequired", (name) => {
    expect(vendorError(with_({ name }))).toBe("A vendor needs a name.");
  });

  // Every cap on both sides of its boundary, measured after trim (Vendor.Trimmed).
  const caps: [keyof VendorInput, number, string][] = [
    ["name", 120, VENDOR_MESSAGES.nameTooLong],
    ["contactName", 120, VENDOR_MESSAGES.contactNameTooLong],
    ["phone", 32, VENDOR_MESSAGES.phoneTooLong],
    ["address", 500, VENDOR_MESSAGES.addressTooLong],
    ["notes", 2000, VENDOR_MESSAGES.notesTooLong],
    ["gstRegistrationNumber", 32, VENDOR_MESSAGES.gstRegistrationNumberTooLong],
    ["qboDisplayName", 500, VENDOR_MESSAGES.qboDisplayNameTooLong],
  ];

  it.each(caps)("%s: %i characters pass, one more fails", (key, max, message) => {
    expect(vendorError(with_({ [key]: "a".repeat(max) }))).toBeNull();
    expect(vendorError(with_({ [key]: "a".repeat(max + 1) }))).toBe(message);
  });

  it.each(caps)("%s: the cap is measured after trimming", (key, max) => {
    expect(vendorError(with_({ [key]: `  ${"a".repeat(max)}  ` }))).toBeNull();
  });

  it("email: 254 characters pass, 255 fail with EmailTooLong (before the shape check)", () => {
    const at254 = `${"a".repeat(254 - "@x.ca".length)}@x.ca`;
    expect(at254).toHaveLength(254);
    expect(vendorError(with_({ email: at254 }))).toBeNull();
    expect(vendorError(with_({ email: `a${at254}` }))).toBe(VENDOR_MESSAGES.emailTooLong);
    // Too long AND malformed reports the length first, as the server does.
    expect(vendorError(with_({ email: "a".repeat(255) }))).toBe(VENDOR_MESSAGES.emailTooLong);
  });

  it("email: a malformed address is EmailInvalid", () => {
    expect(vendorError(with_({ email: "204-555-0142" }))).toBe(
      "The email must look like an address (name@example.com).",
    );
  });

  it("email: padding is trimmed before the shape check", () => {
    expect(vendorError(with_({ email: "  orders@example.com  " }))).toBeNull();
  });

  it("default code: normalized (trim + upper) before the checks", () => {
    expect(vendorError(with_({ defaultBudgetCode: "  fleet-maint " }))).toBeNull();
  });

  it("default code: 32 characters pass, 33 fail with DefaultBudgetCodeTooLong", () => {
    expect(vendorError(with_({ defaultBudgetCode: "A".repeat(32) }))).toBeNull();
    expect(vendorError(with_({ defaultBudgetCode: "A".repeat(33) }))).toBe(
      "The default budget code must be 32 characters or fewer.",
    );
  });

  it.each(["-FUEL", "FUEL-", "FLEET MAINT", "FLEET_MAINT", "CAFÉ"])(
    "default code %j is DefaultBudgetCodeInvalidFormat",
    (code) => {
      expect(vendorError(with_({ defaultBudgetCode: code }))).toBe(
        VENDOR_MESSAGES.defaultBudgetCodeInvalidFormat,
      );
    },
  );

  it("default code: blank is no code at all, never a format error", () => {
    expect(vendorError(with_({ defaultBudgetCode: "   " }))).toBeNull();
  });

  it("reports the FIRST failure in the server's order", () => {
    const everythingWrong = with_({
      name: "",
      contactName: "a".repeat(121),
      email: "nope",
      defaultBudgetCode: "-X",
    });
    expect(vendorError(everythingWrong)).toBe(VENDOR_MESSAGES.nameRequired);
    expect(vendorError({ ...everythingWrong, name: "Ok" })).toBe(VENDOR_MESSAGES.contactNameTooLong);
    expect(vendorError({ ...everythingWrong, name: "Ok", contactName: null })).toBe(
      VENDOR_MESSAGES.emailInvalid,
    );
    expect(
      vendorError({ ...everythingWrong, name: "Ok", contactName: null, email: null }),
    ).toBe(VENDOR_MESSAGES.defaultBudgetCodeInvalidFormat);
  });

  it("asserts no tax rate anywhere — the GST number is length-checked only", () => {
    // Any 32-character string is accepted: no checksum, no format, no rate (Vendor.GstRegistrationNumber).
    expect(vendorError(with_({ gstRegistrationNumber: "not a real BN, any text is ok" }))).toBeNull();
  });
});

describe("looksLikeEmail — Vendor.LooksLikeEmail", () => {
  it.each(["a@b", "orders@example.com", "x.y+z@sub.example.ca"])("accepts %j", (email) => {
    expect(looksLikeEmail(email)).toBe(true);
  });

  it.each([
    ["no @", "orders.example.com"],
    ["@ first", "@example.com"],
    ["@ last", "orders@"],
    ["two @", "a@b@c"],
    ["a space", "or ders@example.com"],
    ["a tab", "orders@exa\tmple.com"],
  ])("refuses %s", (_, email) => {
    expect(looksLikeEmail(email)).toBe(false);
  });
});

describe("hasValidCodeFormat — BudgetCode.HasValidCodeFormat", () => {
  it.each(["A", "FUEL", "FLEET-MAINT", "ZBB-CREW-01", "9"])("accepts %j", (code) => {
    expect(hasValidCodeFormat(code)).toBe(true);
  });

  it.each(["", "-", "-A", "A-", "A B", "A_B", "É", "fuel"])("refuses %j", (code) => {
    // "fuel": the method takes an already-normalized code, so lower case is not valid input.
    expect(hasValidCodeFormat(code)).toBe(false);
  });
});

describe("normalizeVendorName — Vendor.NormalizeName", () => {
  it("trims and upper-cases", () => {
    expect(normalizeVendorName("  Acme fuel ")).toBe("ACME FUEL");
  });

  it("treats null and undefined as empty", () => {
    expect(normalizeVendorName(null)).toBe("");
    expect(normalizeVendorName(undefined)).toBe("");
  });

  it("keeps inner spacing — 'Acme  Fuel' is not 'Acme Fuel'", () => {
    expect(normalizeVendorName("Acme  Fuel")).not.toBe(normalizeVendorName("Acme Fuel"));
  });
});

describe("findDuplicateVendor — VendorNameRule.EnsureUniqueAsync", () => {
  const register = [
    vendor("v1", "Acme Fuel"),
    vendor("v2", "Kal Tire Thompson"),
    vendor("v3", "Old Hardware Co", false),
  ];

  it("finds a clash ignoring case and padding", () => {
    expect(findDuplicateVendor(register, "  acme FUEL ")?.id).toBe("v1");
  });

  it("finds a RETIRED vendor too — its name is still taken", () => {
    expect(findDuplicateVendor(register, "old hardware co")?.id).toBe("v3");
  });

  it("never collides with itself (exceptId) — re-casing your own name is allowed", () => {
    expect(findDuplicateVendor(register, "ACME FUEL", "v1")).toBeNull();
  });

  it("still collides with ANOTHER vendor when editing", () => {
    expect(findDuplicateVendor(register, "acme fuel", "v2")?.id).toBe("v1");
  });

  it("returns null for a new name and for a blank one", () => {
    expect(findDuplicateVendor(register, "Esso Thompson")).toBeNull();
    expect(findDuplicateVendor(register, "   ")).toBeNull();
  });
});

describe("duplicateVendorMessage — VendorErrors.DuplicateName", () => {
  it("names an active vendor", () => {
    expect(duplicateVendorMessage({ name: "Acme Fuel", isActive: true })).toBe(
      'A vendor named "Acme Fuel" already exists. Vendor names are unique, ignoring case.',
    );
  });

  it("tells you to reactivate a retired one", () => {
    expect(duplicateVendorMessage({ name: "Old Hardware Co", isActive: false })).toBe(
      'A retired vendor named "Old Hardware Co" already exists. Reactivate it instead of adding it again.',
    );
  });
});

describe("vendorStatus", () => {
  it("pairs each state with a kind and a written label", () => {
    expect(vendorStatus(true)).toEqual({ kind: "ontime", label: "Active" });
    expect(vendorStatus(false)).toEqual({ kind: "off", label: "Retired" });
  });
});

describe("vendorMatchesSearch", () => {
  const v = vendor("v1", "Kal Tire Thompson", true, {
    contactName: "Dana Okemow",
    email: "orders@kaltire.example",
    phone: "204-555-0142",
    qboDisplayName: "Kal Tire #412",
    defaultBudgetCode: "FLEET-TIRES",
  });

  it.each(["kal", "DANA", "kaltire.example", "555-0142", "#412", "fleet-t", "  tire "])(
    "matches %j",
    (q) => {
      expect(vendorMatchesSearch(v, q)).toBe(true);
    },
  );

  it("matches everything on a blank query and nothing on a miss", () => {
    expect(vendorMatchesSearch(v, "  ")).toBe(true);
    expect(vendorMatchesSearch(v, "esso")).toBe(false);
  });
});

describe("draftToVendorInput / vendorToDraft / vendorReflects", () => {
  it("sends every key, trimmed, blank as null, the default code normalized", () => {
    const input = draftToVendorInput({
      name: "  Kal Tire ",
      contactName: "",
      email: " orders@kaltire.example ",
      phone: "   ",
      address: "",
      notes: "",
      gstRegistrationNumber: " 123456789 RT0001 ",
      qboDisplayName: "",
      defaultBudgetCode: " fleet-tires ",
    });
    expect(input).toEqual({
      name: "Kal Tire",
      contactName: null,
      email: "orders@kaltire.example",
      phone: null,
      address: null,
      notes: null,
      gstRegistrationNumber: "123456789 RT0001",
      qboDisplayName: null,
      defaultBudgetCode: "FLEET-TIRES",
    });
  });

  it("round-trips a record through the draft", () => {
    const v = vendor("v1", "Acme Fuel", true, { email: "a@b.ca", notes: "Line 1\nLine 2" });
    const input = draftToVendorInput(vendorToDraft(v));
    expect(vendorReflects(v, input)).toBe(true);
  });

  it("is not satisfied by a stale row — every written value must be there", () => {
    const stale = vendor("v1", "Acme Fuel", true, { phone: "204-555-0100" });
    expect(vendorReflects(stale, { ...draftToVendorInput(vendorToDraft(stale)), phone: null })).toBe(false);
  });
});
