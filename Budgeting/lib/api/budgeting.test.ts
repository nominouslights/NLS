import { describe, expect, it } from "vitest";
import {
  allocationAmountError,
  allocationCandidates,
  allocationJustificationError,
  assignmentState,
  budgetCodeCategoryKind,
  budgetCodeFormatError,
  canEditAllocations,
  copyOutcomeSummary,
  copySourceCandidates,
  costCentreApplies,
  coverage,
  defaultCopySource,
  leftToAssignCad,
  needsJustification,
  nextTransition,
  normalizeBudgetCode,
  ownerLabel,
  parentCandidates,
  periodKind,
  planBalanced,
  planningProgress,
  previewPeriod,
  stateAfter,
  toBudgetCode,
  toBudgetPeriod,
  unjustifiedLines,
  userDisplay,
  ALLOCATION_JUSTIFICATION_MAX_LENGTH,
  ASSIGNMENT_KINDS,
  ASSIGNMENT_LABELS,
  PERIOD_STATE_LABELS,
  PERIOD_STATE_ORDER,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type AssignmentState,
  type BudgetAllocationRecord,
  type BudgetCodeRecord,
  type BudgetPeriodRecord,
  type ChecklistStep,
  type LifecycleStep,
  type PeriodTransitionAction,
} from "./budgeting";
import type { BudgetCode, BudgetCodeCategory, BudgetPeriod, PeriodState } from "@/lib/types";

// Shared fixtures for the period and allocation helpers below.

const periodRecord: BudgetPeriodRecord = {
  id: "5f2b1e1c-0000-4000-8000-0000000000a1",
  label: "FY2026 Q4",
  granularity: "Quarter",
  year: 2026,
  ordinal: 4,
  startsOn: "2026-10-01",
  endsOn: "2026-12-31",
  state: "Draft",
  createdAtUtc: "2026-09-01T00:00:00+00:00",
  updatedAtUtc: "2026-09-01T00:00:00+00:00",
  plannedRevenueCad: 1_023_500,
  plannedExpenseCad: 245_000,
};

const period = (state: PeriodState, plannedRevenue = 0, plannedExpense = 0): BudgetPeriod => ({
  id: periodRecord.id,
  label: periodRecord.label,
  startsOn: periodRecord.startsOn,
  endsOn: periodRecord.endsOn,
  state,
  pk: periodKind(state),
  plannedRevenue,
  plannedExpense,
});

const makeCode = (
  id: string,
  category: BudgetCodeCategory = "Expense",
  active = true,
): BudgetCode => ({
  id,
  code: id.toUpperCase(),
  name: `Code ${id}`,
  description: null,
  category,
  serviceLine: null,
  costCentre: null,
  parentCodeId: null,
  parentCode: null,
  parentName: null,
  glAccountCode: null,
  taxTreatment: null,
  budgetOwnerUserId: null,
  budgetOwnerEmail: null,
  reviewFrequency: "Quarterly",
  active,
  createdByEmail: null,
  modifiedByEmail: null,
});

const makeLine = (
  budgetCodeId: string,
  category: BudgetCodeCategory = "Expense",
  isCodeActive = true,
  amountCad = 1000,
): BudgetAllocationRecord => ({
  id: `line-${budgetCodeId}`,
  periodId: periodRecord.id,
  budgetCodeId,
  code: budgetCodeId.toUpperCase(),
  name: `Code ${budgetCodeId}`,
  category,
  serviceLine: null,
  isCodeActive,
  amountCad,
  justification: "Because.",
  createdBy: null,
  createdByEmail: null,
  modifiedBy: null,
  modifiedByEmail: null,
  createdAtUtc: "2026-09-01T00:00:00+00:00",
  updatedAtUtc: "2026-09-01T00:00:00+00:00",
});

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

// periodKind, PERIOD_STATE_ORDER, canEditAllocations, nextTransition and stateAfter all mirror
// the C# PeriodState enum and BudgetPeriod.Transition / AllowsPlanChanges in
// Backend/src/Budgeting/Domain/Periods. Five states, forward only.

describe("periodKind", () => {
  // Every screen renders the kind's glyph + the state label together, never colour alone. Draft
  // and Finalized share "info" (the theme has five kinds and "over" means "problem"); the label
  // and the stepper tell them apart.
  it.each<[PeriodState, string]>([
    ["Draft", "info"],
    ["Finalized", "info"],
    ["Open", "ontime"],
    ["InReview", "soon"],
    ["Closed", "off"],
  ])("maps %s to %s", (state, kind) => {
    expect(periodKind(state)).toBe(kind);
  });
});

describe("PERIOD_STATE_ORDER and PERIOD_STATE_LABELS", () => {
  it("lists the five states once each, in lifecycle order", () => {
    expect(PERIOD_STATE_ORDER).toEqual(["Draft", "Finalized", "Open", "InReview", "Closed"]);
    expect(new Set(PERIOD_STATE_ORDER).size).toBe(5);
  });

  it("labels every state in the order", () => {
    for (const state of PERIOD_STATE_ORDER) {
      expect(PERIOD_STATE_LABELS[state]).toBeTruthy();
    }
    expect(PERIOD_STATE_LABELS.InReview).toBe("In review");
  });
});

describe("canEditAllocations", () => {
  // Mirrors BudgetPeriod.AllowsPlanChanges: Draft or Open. The server answers 409
  // Budgeting.Allocation.PeriodNotEditable everywhere else, so this is what hides the controls.
  it.each<[PeriodState, boolean]>([
    ["Draft", true],
    ["Finalized", false],
    ["Open", true],
    ["InReview", false],
    ["Closed", false],
  ])("%s → editable %s", (state, editable) => {
    expect(canEditAllocations(state)).toBe(editable);
  });
});

describe("nextTransition and stateAfter", () => {
  // The transition table in BudgetPeriod.Transition: each state has exactly one way forward and
  // Closed has none. stateAfter is the refetch predicate after the POST.
  it.each<[PeriodState, PeriodTransitionAction, PeriodState]>([
    ["Draft", "finalize", "Finalized"],
    ["Finalized", "open", "Open"],
    ["Open", "begin-review", "InReview"],
    ["InReview", "close", "Closed"],
  ])("%s → %s → %s", (from, action, to) => {
    const t = nextTransition(from);
    expect(t?.action).toBe(action);
    expect(t?.label).toBeTruthy();
    expect(t?.confirmLabel).not.toBe(t?.label);
    expect(stateAfter(action)).toBe(to);
  });

  it("offers nothing from Closed — the lifecycle is terminal there", () => {
    expect(nextTransition("Closed")).toBeNull();
  });

  it("walks the whole order forward, one step per state", () => {
    let state: PeriodState = PERIOD_STATE_ORDER[0];
    const visited = [state];
    for (let t = nextTransition(state); t !== null; t = nextTransition(state)) {
      state = stateAfter(t.action);
      visited.push(state);
    }
    expect(visited).toEqual(PERIOD_STATE_ORDER);
  });
});

describe("toBudgetPeriod", () => {
  it("maps the two totals, derives pk from state and leaves the dates untouched", () => {
    const view = toBudgetPeriod(periodRecord);

    expect(view.plannedRevenue).toBe(1_023_500);
    expect(view.plannedExpense).toBe(245_000);
    expect(view.pk).toBe("info");
    expect(view.state).toBe("Draft");
    expect(view.startsOn).toBe("2026-10-01");
    expect(view.endsOn).toBe("2026-12-31");
    expect(view.label).toBe("FY2026 Q4");
  });

  it.each<PeriodState>(PERIOD_STATE_ORDER)("derives pk for %s via periodKind", (state) => {
    expect(toBudgetPeriod({ ...periodRecord, state }).pk).toBe(periodKind(state));
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

// costCentreApplies mirrors the CostCentreNotAllowedForRevenue check in BudgetCode.Validate
// (BudgetCodeErrors.CostCentreNotAllowedForRevenue). The form hides the field and the detail
// panel hides the row on this answer, so the day the server rule changes, this is what fails.

describe("costCentreApplies", () => {
  it("applies to an expense code — a cost centre attributes cost", () => {
    expect(costCentreApplies("Expense")).toBe(true);
  });

  it("does not apply to a revenue code — the server rejects the combination", () => {
    expect(costCentreApplies("Revenue")).toBe(false);
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

// allocationCandidates mirrors the CodeRetired check in SetBudgetAllocationCommandHandler
// (BudgetAllocationErrors.CodeRetired) and the unique (tenant, period, code) index behind
// upsert-by-code: the picker must never offer a code the server will refuse or that would
// silently update an existing line.

describe("allocationCandidates", () => {
  const codes = [
    makeCode("rev-a", "Revenue"),
    makeCode("rev-b", "Revenue"),
    makeCode("rev-retired", "Revenue", false),
    makeCode("exp-a", "Expense"),
  ];

  it("offers only active codes of the requested category", () => {
    expect(allocationCandidates(codes, [], "Revenue", null).map((c) => c.id)).toEqual([
      "rev-a",
      "rev-b",
    ]);
    expect(allocationCandidates(codes, [], "Expense", null).map((c) => c.id)).toEqual(["exp-a"]);
  });

  it("excludes a code that already carries a line in the period", () => {
    const lines = [makeLine("rev-a", "Revenue")];

    expect(allocationCandidates(codes, lines, "Revenue", null).map((c) => c.id)).toEqual(["rev-b"]);
  });

  it("keeps the code of the line being edited selectable", () => {
    const lines = [makeLine("rev-a", "Revenue")];

    expect(allocationCandidates(codes, lines, "Revenue", "rev-a").map((c) => c.id)).toEqual([
      "rev-a",
      "rev-b",
    ]);
  });

  it("preserves the chart's order", () => {
    const reversed = [...codes].reverse();

    expect(allocationCandidates(reversed, [], "Revenue", null).map((c) => c.id)).toEqual([
      "rev-b",
      "rev-a",
    ]);
  });
});

// allocationAmountError / allocationJustificationError mirror BudgetAllocation.Validate
// (AmountRequired / AmountNegative / AmountTooLarge / JustificationRequired /
// JustificationTooLong). The whole-dollar rule is this app's own, stricter than the server's.

describe("allocationAmountError", () => {
  it.each(["0", "1", "612000", " 250 "])("accepts %j", (text) => {
    expect(allocationAmountError(text)).toBeNull();
  });

  it.each(["", "   ", "abc"])("requires an amount for %j", (text) => {
    expect(allocationAmountError(text)).toBe("Enter an amount.");
  });

  it("refuses cents — the app plans in whole dollars", () => {
    expect(allocationAmountError("12.50")).toContain("whole dollars");
  });

  it("refuses a negative amount", () => {
    expect(allocationAmountError("-1")).toContain("negative");
  });

  it("refuses an amount beyond decimal(12,2)", () => {
    expect(allocationAmountError("1000000000")).toContain("too large");
  });
});

describe("allocationJustificationError", () => {
  it("accepts a justification", () => {
    expect(allocationJustificationError("14 rotations confirmed")).toBeNull();
  });

  it.each(["", "   "])("requires one for %j — zero-based means argued from nothing", (text) => {
    expect(allocationJustificationError(text)).toContain("Enter a justification");
  });

  it("accepts exactly the maximum and refuses one more", () => {
    expect(allocationJustificationError("x".repeat(ALLOCATION_JUSTIFICATION_MAX_LENGTH))).toBeNull();
    expect(
      allocationJustificationError("x".repeat(ALLOCATION_JUSTIFICATION_MAX_LENGTH + 1)),
    ).toContain("1000 characters or fewer");
  });
});

// leftToAssignCad / assignmentState / ASSIGNMENT_KINDS / ASSIGNMENT_LABELS: the dashboard's
// headline tile, Ramsey's step 3. These REPLACE the deleted netCad / netKind / netLabel tests
// rather than adapting them, because the semantics invert: netKind(0) asserted "info" (a
// balanced plan is neutral) and the rule now is "ontime" (under zero-based budgeting $0 left is
// the GOAL). Editing that assertion in place would have hidden the inversion behind a one-word
// diff; deleting the block makes the change visible in review.
//
// No server rule to mirror here — the two totals are the server's sums — but the four-state
// classification is what keeps the tile honest, and the kind + label are what keep it from
// being colour alone.

describe("leftToAssignCad", () => {
  it("is planned revenue less planned expense", () => {
    expect(leftToAssignCad(period("Draft", 1_000, 250))).toBe(750);
    expect(leftToAssignCad(period("Draft", 250, 1_000))).toBe(-750);
  });
});

describe("assignmentState", () => {
  it.each<[string, BudgetPeriod, AssignmentState, string, string]>([
    ["nothing planned at all", period("Draft", 0, 0), "empty", "info", "Nothing planned yet"],
    ["every dollar given a job", period("Draft", 1_000, 1_000), "balanced", "ontime", "All assigned"],
    ["dollars still spare", period("Draft", 1_000, 600), "unassigned", "soon", "To assign"],
    ["more assigned than earned", period("Draft", 1_000, 1_400), "over", "over", "Over-assigned"],
  ])("%s → %s", (_why, p, state, kind, label) => {
    expect(assignmentState(p)).toBe(state);
    expect(ASSIGNMENT_KINDS[state]).toBe(kind);
    expect(ASSIGNMENT_LABELS[state]).toBe(label);
  });

  it("tells an empty period apart from a balanced one, though both sit at zero", () => {
    // The whole reason the state exists: left === 0 is ambiguous, and a tile reading
    // "All assigned ✓" over a period with nothing in it states the opposite of the truth.
    const empty = period("Draft", 0, 0);
    const balanced = period("Draft", 4_200, 4_200);

    expect(leftToAssignCad(empty)).toBe(leftToAssignCad(balanced));
    expect(assignmentState(empty)).toBe("empty");
    expect(assignmentState(balanced)).toBe("balanced");
  });

  it("is not empty when a period plans expenses against no revenue", () => {
    expect(assignmentState(period("Draft", 0, 900))).toBe("over");
  });
});

describe("planBalanced", () => {
  it("is true only for a balanced plan", () => {
    expect(planBalanced(period("Draft", 1_000, 1_000))).toBe(true);
    expect(planBalanced(period("Draft", 1_000, 600))).toBe(false);
    expect(planBalanced(period("Draft", 1_000, 1_400))).toBe(false);
  });

  it("is FALSE for an empty plan, which is what makes finalize warn on an untouched period", () => {
    expect(leftToAssignCad(period("Draft", 0, 0))).toBe(0);
    expect(planBalanced(period("Draft", 0, 0))).toBe(false);
  });
});

// needsJustification / unjustifiedLines mirror BudgetAllocation.NeedsJustification
// (`Justification.Length == 0`) — which is exactly the value BudgetAllocation.CopyInto writes —
// widened to Validate's IsNullOrWhiteSpace, so a line of spaces counts as unargued here too
// rather than only at the save that would refuse it (JustificationRequired).

describe("needsJustification and unjustifiedLines", () => {
  const withJustification = (justification: string): BudgetAllocationRecord => ({
    ...makeLine("exp-a"),
    justification,
  });

  it("is true for the empty string a copied line arrives with", () => {
    expect(needsJustification(withJustification(""))).toBe(true);
  });

  it("is true for whitespace, matching Validate's IsNullOrWhiteSpace", () => {
    expect(needsJustification(withJustification("   "))).toBe(true);
  });

  it("is false once somebody has argued the line", () => {
    expect(needsJustification(withJustification("Two extra runs a week."))).toBe(false);
  });

  it("counts only the unargued lines", () => {
    const lines = [
      withJustification(""),
      { ...makeLine("exp-b"), justification: "Quoted." },
      { ...makeLine("exp-c"), justification: " " },
    ];

    expect(unjustifiedLines(lines).map((l) => l.budgetCodeId)).toEqual(["exp-a", "exp-c"]);
  });

  it("is empty for a period with no lines", () => {
    expect(unjustifiedLines([])).toEqual([]);
  });
});

// copySourceCandidates / defaultCopySource mirror CopyBudgetAllocationsCommandHandler's guards.
// Only CopySourceIsTarget narrows the list: the handler checks AllowsPlanChanges on the TARGET
// and deliberately not on the source, so a Closed period is a legal source.

describe("copySourceCandidates", () => {
  const march = { ...period("Closed", 9_000, 9_000), id: "p-march", startsOn: "2026-03-01" };
  const april = { ...period("InReview"), id: "p-april", startsOn: "2026-04-01" };
  const may = { ...period("Draft"), id: "p-may", startsOn: "2026-05-01" };
  const all = [march, april, may];

  it("excludes the target and nothing else", () => {
    expect(copySourceCandidates(all, "p-may").map((p) => p.id)).toEqual(["p-march", "p-april"]);
  });

  it("OFFERS a Closed period — the server checks editability on the target only", () => {
    // The asymmetry a reader gets backwards. Copying a closed period's plan into a fresh Draft
    // is the entire point of the feature, so filtering by canEditAllocations here would refuse
    // the most common case with no error anywhere to explain it.
    expect(canEditAllocations(march.state)).toBe(false);
    expect(copySourceCandidates(all, "p-may")).toContain(march);
  });

  it("offers every other state too", () => {
    expect(copySourceCandidates(all, "p-march").map((p) => p.state)).toEqual(["InReview", "Draft"]);
  });

  it("is empty when the target is the only period there is", () => {
    expect(copySourceCandidates([may], "p-may")).toEqual([]);
  });
});

describe("defaultCopySource", () => {
  const march = { ...period("Closed"), id: "p-march", startsOn: "2026-03-01" };
  const april = { ...period("Closed"), id: "p-april", startsOn: "2026-04-01" };
  const may = { ...period("Draft"), id: "p-may", startsOn: "2026-05-01" };

  it("pre-selects the latest period before the target — 'last period'", () => {
    expect(defaultCopySource([march, april, may], "p-may")?.id).toBe("p-april");
  });

  it("falls back to the latest candidate when the target is the earliest", () => {
    expect(defaultCopySource([march, april, may], "p-march")?.id).toBe("p-may");
  });

  it("is null when there is nothing to copy from", () => {
    expect(defaultCopySource([may], "p-may")).toBeNull();
  });
});

// copyOutcomeSummary reports every bucket the server counts, because the invariant
// copied + skippedAlreadyPlanned + skippedRetiredCode === sourceLineCount (pinned server-side in
// CopyBudgetAllocationsCommandHandlerTests) is only reassuring if the user can see it add up.

describe("copyOutcomeSummary", () => {
  it("reports a clean copy, naming the missing justifications", () => {
    expect(
      copyOutcomeSummary({
        copied: 11,
        skippedAlreadyPlanned: 0,
        skippedRetiredCode: 0,
        sourceLineCount: 11,
      }),
    ).toBe("Copied 11 lines, each with no justification yet. 11 lines in the source period.");
  });

  it("omits a skip clause that is zero rather than writing '0 skipped'", () => {
    const summary = copyOutcomeSummary({
      copied: 3,
      skippedAlreadyPlanned: 2,
      skippedRetiredCode: 0,
      sourceLineCount: 5,
    });

    expect(summary).toContain("2 lines already planned here and left untouched");
    expect(summary).not.toContain("retired");
  });

  it("names both skip reasons when both happened", () => {
    const summary = copyOutcomeSummary({
      copied: 1,
      skippedAlreadyPlanned: 1,
      skippedRetiredCode: 1,
      sourceLineCount: 3,
    });

    expect(summary).toContain("1 line already planned here and left untouched");
    expect(summary).toContain("1 line on retired codes");
    expect(summary).toContain("3 lines in the source period");
  });

  it("says nothing was copied rather than claiming a copy, when every line was skipped", () => {
    expect(
      copyOutcomeSummary({
        copied: 0,
        skippedAlreadyPlanned: 4,
        skippedRetiredCode: 0,
        sourceLineCount: 4,
      }),
    ).toBe(
      "Nothing was copied — skipped 4 lines already planned here and left untouched. 4 lines in the source period.",
    );
  });

  it("treats an empty source as a success, not a failure", () => {
    // The server answers 200 with four zeroes for an empty source period, so the console must
    // say "nothing to copy" rather than raise an error for a button that worked.
    expect(
      copyOutcomeSummary({
        copied: 0,
        skippedAlreadyPlanned: 0,
        skippedRetiredCode: 0,
        sourceLineCount: 0,
      }),
    ).toBe("That period has no lines to copy — nothing was added.");
  });
});

describe("coverage", () => {
  const codes = [
    makeCode("rev-a", "Revenue"),
    makeCode("rev-b", "Revenue"),
    makeCode("rev-retired", "Revenue", false),
    makeCode("exp-a", "Expense"),
  ];

  it("counts planned active codes over active codes, per category", () => {
    const lines = [makeLine("rev-a", "Revenue"), makeLine("exp-a", "Expense")];

    expect(coverage(lines, codes, "Revenue")).toEqual({ planned: 1, active: 2 });
    expect(coverage(lines, codes, "Expense")).toEqual({ planned: 1, active: 1 });
  });

  it("does not count a line on a retired code — nothing a planner can still act on", () => {
    const lines = [makeLine("rev-retired", "Revenue", false)];

    expect(coverage(lines, codes, "Revenue")).toEqual({ planned: 0, active: 2 });
  });

  it("is 0/0 for a category with no codes", () => {
    expect(coverage([], [], "Expense")).toEqual({ planned: 0, active: 0 });
  });
});

// planningProgress feeds the dashboard's zero-based checklist and stepper from one derivation:
// four checklist rows (revenue, expense, balance, argued) then the five lifecycle states
// relative to PERIOD_STATE_ORDER. Step 4 of zero-based budgeting — track all month — gets no row
// on purpose: actuals are still mock, and a row that can never turn green is worse than a gap.
//
// Each row carries its own kind and status word, so PlanningChecklist renders branch-free and
// the colour decision is tested here rather than in a component.

describe("planningProgress", () => {
  const codes = [
    makeCode("rev-a", "Revenue"),
    makeCode("exp-a", "Expense"),
    makeCode("exp-b", "Expense"),
  ];
  const checklist = (steps: ReturnType<typeof planningProgress>) =>
    steps.filter((s): s is ChecklistStep => s.group === "checklist");
  const row = (steps: ReturnType<typeof planningProgress>, id: ChecklistStep["id"]) =>
    checklist(steps).find((s) => s.id === id)!;
  const lifecycle = (steps: ReturnType<typeof planningProgress>) =>
    steps.filter((s): s is LifecycleStep => s.group === "lifecycle").map((s) => [s.id, s.status]);

  it("marks both line steps pending for an empty Draft, and Draft current", () => {
    const steps = planningProgress(period("Draft"), [], codes);

    expect(checklist(steps).map((s) => [s.id, s.kind, s.status])).toEqual([
      ["revenue", "info", "Pending"],
      ["expense", "info", "Pending"],
      ["balance", "info", "Nothing planned yet"],
      ["argued", "info", "Pending"],
    ]);
    expect(lifecycle(steps)).toEqual([
      ["Draft", "current"],
      ["Finalized", "pending"],
      ["Open", "pending"],
      ["InReview", "pending"],
      ["Closed", "pending"],
    ]);
  });

  it("marks a line step done once it has a line, with the count and coverage in the detail", () => {
    const lines = [makeLine("rev-a", "Revenue"), makeLine("exp-a", "Expense")];
    const steps = planningProgress(period("Draft"), lines, codes);

    expect(row(steps, "revenue").status).toBe("Done");
    expect(row(steps, "revenue").kind).toBe("ontime");
    expect(row(steps, "revenue").detail).toBe("1 line · 1 of 1 active revenue codes planned");
    expect(row(steps, "expense").detail).toBe("1 line · 1 of 2 active expense codes planned");
  });

  it("writes the balance row's signed figure out, and never leaves the sign to colour", () => {
    const unassigned = row(planningProgress(period("Draft", 10_000, 5_800), [], codes), "balance");
    expect(unassigned.status).toBe("To assign");
    expect(unassigned.kind).toBe("soon");
    expect(unassigned.detail).toBe("Left to assign +$4,200 — give the rest a job.");

    const over = row(planningProgress(period("Draft", 10_000, 11_000), [], codes), "balance");
    expect(over.status).toBe("Over-assigned");
    expect(over.kind).toBe("over");
    expect(over.detail).toBe(
      "Left to assign −$1,000 — this plan assigns more than it plans to earn.",
    );

    const balanced = row(planningProgress(period("Draft", 10_000, 10_000), [], codes), "balance");
    expect(balanced.status).toBe("All assigned");
    expect(balanced.kind).toBe("ontime");
    expect(balanced.detail).toBe("Left to assign $0 — every planned dollar has a job.");
  });

  it("counts the lines a copy left unargued, and says why they are unargued", () => {
    const lines = [
      makeLine("rev-a", "Revenue"),
      { ...makeLine("exp-a", "Expense"), justification: "" },
      { ...makeLine("exp-b", "Expense"), justification: "   " },
    ];
    const argued = row(planningProgress(period("Draft", 1_000, 2_000), lines, codes), "argued");

    expect(argued.kind).toBe("soon");
    expect(argued.status).toBe("Needs work");
    expect(argued.detail).toBe(
      "2 of 3 lines still need a justification — a copied line brings its amount, not its argument.",
    );
  });

  it("marks the argued row done once every line carries an argument", () => {
    const lines = [makeLine("rev-a", "Revenue"), makeLine("exp-a", "Expense")];
    const argued = row(planningProgress(period("Draft", 1_000, 1_000), lines, codes), "argued");

    expect(argued.kind).toBe("ontime");
    expect(argued.status).toBe("Done");
    expect(argued.detail).toBe("All 2 lines carry a justification.");
  });

  it("marks the states before Open done and the ones after pending", () => {
    expect(lifecycle(planningProgress(period("Open"), [], codes))).toEqual([
      ["Draft", "done"],
      ["Finalized", "done"],
      ["Open", "current"],
      ["InReview", "pending"],
      ["Closed", "pending"],
    ]);
  });

  it("marks everything done but Closed, which is current, at the end", () => {
    expect(lifecycle(planningProgress(period("Closed"), [], codes))).toEqual([
      ["Draft", "done"],
      ["Finalized", "done"],
      ["Open", "done"],
      ["InReview", "done"],
      ["Closed", "current"],
    ]);
  });

  it("puts the four checklist rows first, then the five states in order", () => {
    const steps = planningProgress(period("Draft"), [], codes);

    expect(steps.map((s) => s.id)).toEqual([
      "revenue",
      "expense",
      "balance",
      "argued",
      ...PERIOD_STATE_ORDER,
    ]);
  });

  it("has no row for step 4 of zero-based budgeting — actuals are still mock", () => {
    const ids = checklist(planningProgress(period("Draft"), [], codes)).map((s) => s.id);

    expect(ids).toHaveLength(4);
    expect(ids).not.toContain("tracked");
  });
});

// Both of these decide how a *person* is rendered. The server sends the name and the email
// separately and resolves the name on every read (BudgetCodeReadService), so the fallback is a
// client decision — made once here rather than at each of the four call sites.

describe("ownerLabel", () => {
  const owner = {
    userId: "8f1d2c3b-4a5e-4f60-9a71-2b3c4d5e6f70",
    email: "lea@northernlink.ca",
    role: "Accountant",
    fullName: null as string | null,
  };

  it("shows the name with the email beside it, so similar names stay distinguishable", () => {
    expect(ownerLabel({ ...owner, fullName: "Léa Fontaine" })).toBe(
      "Léa Fontaine (lea@northernlink.ca)",
    );
  });

  it("shows the bare email for anyone who has not set a name", () => {
    expect(ownerLabel(owner)).toBe("lea@northernlink.ca");
  });

  it("shows the bare email when the name is only whitespace", () => {
    expect(ownerLabel({ ...owner, fullName: "   " })).toBe("lea@northernlink.ca");
  });
});

describe("userDisplay", () => {
  it("prefers the name", () => {
    expect(userDisplay("Léa Fontaine", "lea@northernlink.ca", "—")).toBe("Léa Fontaine");
  });

  it("falls back to the email when there is no name", () => {
    expect(userDisplay(null, "lea@northernlink.ca", "—")).toBe("lea@northernlink.ca");
  });

  it("falls back to the placeholder when the id resolved to nobody at all", () => {
    // Both null is what an unassigned budget owner looks like, and what a user id that is no
    // longer in the replica looks like. The caller decides the word.
    expect(userDisplay(null, null, "Unassigned")).toBe("Unassigned");
  });
});
