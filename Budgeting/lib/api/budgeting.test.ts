import { describe, expect, it } from "vitest";
import {
  budgetCodeCategoryKind,
  budgetCodeFormatError,
  normalizeBudgetCode,
  parentCandidates,
  periodKind,
  previewPeriod,
  toBudgetCode,
  toBudgetPeriod,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type BudgetCodeRecord,
  type BudgetPeriodRecord,
} from "./budgeting";
import type { BudgetCode } from "@/lib/types";

// previewPeriod is a display-only mirror of the server's derivation
// (BudgetPeriod.Create in Backend/src/Budgeting) — these cases pin it to the
// same rules so the modal's preview can never disagree with what gets stored.

describe("previewPeriod", () => {
  it("derives a quarter's dates and label", () => {
    expect(previewPeriod("Quarter", 2026, 4)).toEqual({
      label: "FY2026 Q4",
      startsOn: "2026-10-01",
      endsOn: "2026-12-31",
    });
  });

  it("derives Q1 at the year boundary", () => {
    expect(previewPeriod("Quarter", 2026, 1)).toEqual({
      label: "FY2026 Q1",
      startsOn: "2026-01-01",
      endsOn: "2026-03-31",
    });
  });

  it("derives a month's dates and label", () => {
    expect(previewPeriod("Month", 2026, 3)).toEqual({
      label: "March 2026",
      startsOn: "2026-03-01",
      endsOn: "2026-03-31",
    });
  });

  it("gives February its leap day in a leap year", () => {
    expect(previewPeriod("Month", 2028, 2)).toEqual({
      label: "February 2028",
      startsOn: "2028-02-01",
      endsOn: "2028-02-29",
    });
  });

  it.each([0, 13])("returns null for month ordinal %i", (ordinal) => {
    expect(previewPeriod("Month", 2026, ordinal)).toBeNull();
  });

  it.each([0, 5])("returns null for quarter ordinal %i", (ordinal) => {
    expect(previewPeriod("Quarter", 2026, ordinal)).toBeNull();
  });

  it("returns null outside the 2020–2100 year range", () => {
    expect(previewPeriod("Quarter", 2019, 1)).toBeNull();
    expect(previewPeriod("Quarter", 2101, 1)).toBeNull();
  });

  it("returns null for non-integer input (a half-typed year)", () => {
    expect(previewPeriod("Quarter", NaN, 1)).toBeNull();
    expect(previewPeriod("Month", 2026, 2.5)).toBeNull();
  });
});

describe("periodKind", () => {
  // The state→kind mapping the mock layer used to carry per row; every screen
  // renders the kind's glyph + the state label together, never colour alone.
  it("maps each period state to its status kind", () => {
    expect(periodKind("Draft")).toBe("info");
    expect(periodKind("Open")).toBe("ontime");
    expect(periodKind("Locked")).toBe("off");
  });
});

// toBudgetPeriod is the wire→view mapper Console.tsx runs over every row it fetches. Its
// sibling toBudgetCode has four cases; this had none, and the two things it decides on the
// screens' behalf are exactly the two a future allocations slice will be tempted to change.

describe("toBudgetPeriod", () => {
  const record: BudgetPeriodRecord = {
    id: "7c3d2f1a-0000-4000-8000-000000000001",
    label: "March 2026",
    granularity: "Month",
    year: 2026,
    ordinal: 3,
    startsOn: "2026-03-01",
    endsOn: "2026-03-31",
    state: "Open",
    createdAtUtc: "2026-02-01T00:00:00+00:00",
    updatedAtUtc: "2026-02-01T00:00:00+00:00",
  };

  it("carries the server-derived label and date range through unchanged", () => {
    // The server derives startsOn/endsOn/label from granularity + year + ordinal. Nothing here
    // re-derives them — previewPeriod exists for the modal's live preview only.
    const view = toBudgetPeriod(record);

    expect(view.id).toBe(record.id);
    expect(view.label).toBe("March 2026");
    expect(view.startsOn).toBe("2026-03-01");
    expect(view.endsOn).toBe("2026-03-31");
    expect(view.state).toBe("Open");
  });

  it("reports planned and allocated as honest zeros, not placeholders", () => {
    // Neither figure is on the wire yet — they belong to the allocations story. Zero is the
    // truthful answer today and makes the screens render their empty states; a plausible-looking
    // number here would read as real budget data that nobody entered.
    const view = toBudgetPeriod(record);

    expect(view.planned).toBe(0);
    expect(view.allocated).toBe(0);
  });

  it("derives pk from state via periodKind rather than reading it off the wire", () => {
    // The wire carries no status kind. If pk ever stopped tracking state, a locked period would
    // render with a live-plan chip — colour and glyph both saying the wrong thing.
    expect(toBudgetPeriod({ ...record, state: "Draft" }).pk).toBe(periodKind("Draft"));
    expect(toBudgetPeriod({ ...record, state: "Open" }).pk).toBe(periodKind("Open"));
    expect(toBudgetPeriod({ ...record, state: "Locked" }).pk).toBe(periodKind("Locked"));

    expect(toBudgetPeriod({ ...record, state: "Locked" }).pk).toBe("off");
  });

  it("drops the audit timestamps the screens do not render", () => {
    const view = toBudgetPeriod(record) as Record<string, unknown>;

    expect(view).not.toHaveProperty("createdAtUtc");
    expect(view).not.toHaveProperty("granularity");
  });
});

// normalizeBudgetCode and budgetCodeFormatError mirror the server's
// BudgetCode.NormalizeCode / ValidateCode — these cases pin them to the same
// rules so the modal's preview and its disabled-submit reasoning can never
// disagree with what the API accepts.

describe("normalizeBudgetCode", () => {
  it.each([
    ["zbb-crew-01", "ZBB-CREW-01"],
    ["  ZBB-CREW-01  ", "ZBB-CREW-01"],
    ["Zbb-Crew-01", "ZBB-CREW-01"],
  ])("normalizes %s to %s", (input, expected) => {
    expect(normalizeBudgetCode(input)).toBe(expected);
  });
});

describe("budgetCodeFormatError", () => {
  it.each(["ZBB-CREW-01", "zbb-crew-01", "FUEL01", "A", "  ZBB-FUEL-01 "])(
    "accepts %s",
    (code) => {
      expect(budgetCodeFormatError(code)).toBeNull();
    },
  );

  it.each(["", "   "])("rejects the blank code %j", (code) => {
    expect(budgetCodeFormatError(code)).toBe("Enter a code.");
  });

  it("accepts a code of exactly 32 characters", () => {
    // The server rejects on `normalizedCode.Length > CodeMaxLength`, so 32 is the last legal
    // length. Testing only 33 leaves the boundary itself unpinned: a `>=` slipped in here would
    // refuse a code the API would have accepted, and no test would notice.
    expect(budgetCodeFormatError("A".repeat(32))).toBeNull();
  });

  it("rejects a code over 32 characters", () => {
    expect(budgetCodeFormatError("A".repeat(33))).toContain("32 characters or fewer");
  });

  it("measures length after normalization, not before", () => {
    // NormalizeCode trims first, so surrounding whitespace must not count toward the 32.
    expect(budgetCodeFormatError(`  ${"A".repeat(32)}  `)).toBeNull();
  });

  it.each(["-LEADING", "TRAILING-", "HAS SPACE", "HAS_UNDERSCORE", "HAS/SLASH"])(
    "rejects the malformed code %s",
    (code) => {
      expect(budgetCodeFormatError(code)).toContain("letters, digits and hyphens");
    },
  );

  it.each([
    ["an accented letter", "ZBB-CRÊW"],
    ["a homoglyph in Cyrillic", "ZВВ-CREW"],
    ["an en dash standing in for a hyphen", "ZBB–CREW"],
    ["full-width digits", "ZBB-０１"],
    ["an emoji", "ZBB-CREW-🚌"],
  ])("rejects %s — the server is ASCII-only", (_label, code) => {
    // ValidateCode loops with char.IsAsciiLetterOrDigit, so anything outside ASCII is a 400.
    // Worth pinning because the two sneaky cases above look correct in the input field: the
    // Cyrillic В and the en dash are pixel-near their ASCII counterparts, and without this the
    // client would wave them through and let the planner meet a server error instead.
    expect(budgetCodeFormatError(code)).toContain("letters, digits and hyphens");
  });
});

describe("budgetCodeCategoryKind", () => {
  it("maps each category to its status kind", () => {
    expect(budgetCodeCategoryKind("Revenue")).toBe("ontime");
    expect(budgetCodeCategoryKind("Expense")).toBe("info");
  });
});


// The label maps are exhaustive by construction (Record<Union, string> makes a missing member a
// compile error), so these pin the *strings* — specifically the six that have to match the
// backend enum exactly.

describe("SERVICE_LINE_LABELS", () => {
  it("spells the six revenue members exactly as TripServiceType does", () => {
    // THE highest-consequence assertion in this file. BudgetServiceLine's first six members are
    // byte-identical to Backend/src/Trips/Domain/Trips/TripServiceType.cs so that Stage 6.2's
    // revenue-mix report can join on the string Trips and Billing already emit. A typo here —
    // "NIHB" for "Nihb" — silently drops a whole revenue category from that report, with no
    // error on either side.
    const keys = Object.keys(SERVICE_LINE_LABELS);
    expect(keys.slice(0, 6)).toEqual([
      "ContractCrew",
      "Community",
      "Nihb",
      "Charter",
      "Cargo",
      "Grocery",
    ]);
  });

  it("adds the three overhead members that no trip can carry", () => {
    expect(Object.keys(SERVICE_LINE_LABELS).slice(6)).toEqual([
      "Fleet",
      "Administrative",
      "Apprenticeship",
    ]);
  });
});

describe("TAX_TREATMENT_LABELS", () => {
  it("covers every wire value", () => {
    expect(Object.keys(TAX_TREATMENT_LABELS)).toEqual([
      "GstApplicable",
      "ZeroRated",
      "Exempt",
      "NotApplicable",
    ]);
  });
});

describe("REVIEW_FREQUENCY_LABELS", () => {
  it("covers every wire value", () => {
    expect(Object.keys(REVIEW_FREQUENCY_LABELS)).toEqual(["Monthly", "Quarterly", "Annual"]);
  });
});

// parentCandidates mirrors BudgetCodeParentRule: the picker must not offer an option the server
// would reject with a 400.

describe("parentCandidates", () => {
  const code = (id: string, parentCodeId: string | null = null): BudgetCode => ({
    id,
    code: id.toUpperCase(),
    name: `Code ${id}`,
    description: null,
    category: "Expense",
    serviceLine: null,
    costCentre: null,
    parentCodeId,
    parentCode: null,
    parentName: null,
    glAccountCode: null,
    taxTreatment: null,
    budgetOwnerUserId: null,
    budgetOwnerEmail: null,
    reviewFrequency: "Quarterly",
    active: true,
    createdByEmail: null,
    modifiedByEmail: null,
  });

  it("excludes the code being edited — nothing may be its own parent", () => {
    const codes = [code("a"), code("b")];

    expect(parentCandidates(codes, "a").map((c) => c.id)).toEqual(["b"]);
  });

  it("excludes codes that already have a parent — the hierarchy is one level deep", () => {
    const codes = [code("a"), code("b", "a")];

    expect(parentCandidates(codes, null).map((c) => c.id)).toEqual(["a"]);
  });

  it("offers every top-level code when creating", () => {
    const codes = [code("a"), code("b")];

    expect(parentCandidates(codes, null).map((c) => c.id)).toEqual(["a", "b"]);
  });

  it("returns nothing when every code already has a parent", () => {
    expect(parentCandidates([code("b", "a")], null)).toEqual([]);
  });
});

describe("toBudgetCode", () => {
  const record: BudgetCodeRecord = {
    id: "5f2b1e1c-0000-4000-8000-000000000001",
    code: "ZBB-CREW-01",
    name: "Alamos crew shuttle",
    description: "Contracted crew rotation runs.",
    category: "Revenue",
    serviceLine: "ContractCrew",
    costCentre: "OPS-01",
    parentCodeId: null,
    parentCode: null,
    parentName: null,
    glAccountCode: "4000",
    taxTreatment: "GstApplicable",
    budgetOwnerUserId: "5f2b1e1c-0000-4000-8000-000000000009",
    budgetOwnerEmail: "planner@northernlink.ca",
    reviewFrequency: "Quarterly",
    isActive: true,
    createdBy: "5f2b1e1c-0000-4000-8000-000000000009",
    createdByEmail: "planner@northernlink.ca",
    modifiedBy: null,
    modifiedByEmail: null,
    createdAtUtc: "2026-08-12T00:00:00+00:00",
    updatedAtUtc: "2026-08-12T00:00:00+00:00",
  };

  it("renames isActive to the active flag the screens read", () => {
    expect(toBudgetCode(record).active).toBe(true);
    expect(toBudgetCode({ ...record, isActive: false }).active).toBe(false);
  });

  it("carries every classification, accounting and governance field through", () => {
    const view = toBudgetCode(record);

    expect(view.serviceLine).toBe("ContractCrew");
    expect(view.costCentre).toBe("OPS-01");
    expect(view.glAccountCode).toBe("4000");
    expect(view.taxTreatment).toBe("GstApplicable");
    expect(view.reviewFrequency).toBe("Quarterly");
    expect(view.budgetOwnerEmail).toBe("planner@northernlink.ca");
    expect(view.createdByEmail).toBe("planner@northernlink.ca");
  });

  it("passes nulls through rather than substituting placeholders", () => {
    // The screen decides how an absent value reads ("Unassigned", "—", "Top level"); the mapper
    // must not pre-empt that with a string of its own.
    const empty = toBudgetCode({
      ...record,
      description: null,
      serviceLine: null,
      costCentre: null,
      glAccountCode: null,
      taxTreatment: null,
      budgetOwnerUserId: null,
      budgetOwnerEmail: null,
      createdByEmail: null,
    });

    expect(empty.description).toBeNull();
    expect(empty.serviceLine).toBeNull();
    expect(empty.costCentre).toBeNull();
    expect(empty.glAccountCode).toBeNull();
    expect(empty.taxTreatment).toBeNull();
    expect(empty.budgetOwnerEmail).toBeNull();
    expect(empty.createdByEmail).toBeNull();
  });

  it("keeps the server-resolved parent display fields", () => {
    const child = toBudgetCode({
      ...record,
      parentCodeId: "5f2b1e1c-0000-4000-8000-000000000002",
      parentCode: "ZBB-REV",
      parentName: "Revenue rollup",
    });

    expect(child.parentCode).toBe("ZBB-REV");
    expect(child.parentName).toBe("Revenue rollup");
  });
});
