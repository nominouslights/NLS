import { describe, expect, it } from "vitest";
import {
  actualAmountError,
  actualCodeCandidates,
  actualDateError,
  actualDescriptionError,
  allocationAmountError,
  allocationCandidates,
  allocationJustificationError,
  budgetCodeCategoryKind,
  budgetCodeFormatError,
  budgetCodeGroupKey,
  canEditAllocations,
  costCentreApplies,
  costCentreSuggestions,
  coverage,
  groupBudgetCodesByCostCentre,
  hasPlannedAmounts,
  matchesBudgetCodeQuery,
  nextTransition,
  normalizeBudgetCode,
  ownerLabel,
  parentCandidates,
  periodCovering,
  periodKind,
  planningProgress,
  previewPeriod,
  rollupDeltaPct,
  stateAfter,
  toBudgetCode,
  toBudgetPeriod,
  toBudgetRollup,
  userDisplay,
  zbbBalance,
  zbbBalanceDisplay,
  ACTUAL_DESCRIPTION_MAX_LENGTH,
  ALLOCATION_JUSTIFICATION_MAX_LENGTH,
  PERIOD_STATE_LABELS,
  PERIOD_STATE_ORDER,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  ZBB_BALANCE_KINDS,
  type BudgetAllocationRecord,
  type BudgetCodeRecord,
  type BudgetPeriodRecord,
  type BudgetRollupRowRecord,
  type BalanceStep,
  type LifecycleStep,
  type LineStep,
  type PeriodTransitionAction,
} from "./budgeting";
import type { BudgetCode, BudgetCodeCategory, BudgetPeriod, PeriodState } from "@/lib/types";
// The variance bands are the console's, not the server's — the rollup endpoint ships no band at
// all — and they still live beside the last mock array. The rollup tests below assert that the
// percentage this file computes lands in the band the Variance screen would show for it.
import { varianceKind, varianceLabel } from "@/lib/data";

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

  it("rejects a code over 32 characters", () => {
    expect(budgetCodeFormatError("A".repeat(33))).toContain("32 characters or fewer");
  });

  it.each(["-LEADING", "TRAILING-", "HAS SPACE", "HAS_UNDERSCORE", "HAS/SLASH"])(
    "rejects the malformed code %s",
    (code) => {
      expect(budgetCodeFormatError(code)).toContain("letters, digits and hyphens");
    },
  );
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

// The grouping helpers mirror no server rule except through costCentreApplies: a revenue code
// never carries a cost centre (BudgetCodeErrors.CostCentreNotAllowedForRevenue), so revenue codes
// get their own bucket. Everything else here is display — case-folding groups spellings, it never
// rewrites what the server stored.

describe("budgetCodeGroupKey", () => {
  it("puts a revenue code in the revenue bucket, even with a stray cost centre", () => {
    expect(budgetCodeGroupKey({ category: "Revenue", costCentre: null })).toBe("category:Revenue");
    expect(budgetCodeGroupKey({ category: "Revenue", costCentre: "OPS-01" })).toBe(
      "category:Revenue",
    );
  });

  it("puts an expense code without a cost centre in the no-cost-centre group", () => {
    expect(budgetCodeGroupKey({ category: "Expense", costCentre: null })).toBe("none");
    expect(budgetCodeGroupKey({ category: "Expense", costCentre: "   " })).toBe("none");
  });

  it("gives spellings that differ only in case the same key", () => {
    expect(budgetCodeGroupKey({ category: "Expense", costCentre: "Ops-01" })).toBe(
      budgetCodeGroupKey({ category: "Expense", costCentre: "OPS-01" }),
    );
  });

  it("does not let a cost centre spelled 'Revenue' collide with the revenue bucket", () => {
    expect(budgetCodeGroupKey({ category: "Expense", costCentre: "Revenue" })).not.toBe(
      budgetCodeGroupKey({ category: "Revenue", costCentre: null }),
    );
  });
});

describe("groupBudgetCodesByCostCentre", () => {
  it("orders named cost centres A–Z, then no cost centre, then revenue", () => {
    const groups = groupBudgetCodesByCostCentre([
      makeCode("rev", "Revenue"),
      makeCode("bare"),
      { ...makeCode("z"), costCentre: "Zeta" },
      { ...makeCode("a"), costCentre: "alpha" },
    ]);
    expect(groups.map((g) => g.label)).toEqual(["alpha", "Zeta", "No cost centre", "Revenue"]);
    expect(groups.map((g) => g.kind)).toEqual([
      "costCentre",
      "costCentre",
      "noCostCentre",
      "category",
    ]);
  });

  it("sorts numbered cost centres numerically, so CC-2 comes before CC-10", () => {
    const groups = groupBudgetCodesByCostCentre([
      { ...makeCode("ten"), costCentre: "CC-10" },
      { ...makeCode("two"), costCentre: "CC-2" },
    ]);
    expect(groups.map((g) => g.label)).toEqual(["CC-2", "CC-10"]);
  });

  it("skips empty groups, and returns nothing for no codes", () => {
    expect(groupBudgetCodesByCostCentre([])).toEqual([]);
    const groups = groupBudgetCodesByCostCentre([makeCode("bare")]);
    expect(groups.map((g) => g.key)).toEqual(["none"]);
  });

  it("keeps the server's order within a group", () => {
    const groups = groupBudgetCodesByCostCentre([
      { ...makeCode("c"), costCentre: "Fleet" },
      { ...makeCode("a"), costCentre: "Fleet" },
      { ...makeCode("b"), costCentre: "Fleet" },
    ]);
    expect(groups[0].codes.map((c) => c.id)).toEqual(["c", "a", "b"]);
  });

  it("labels a merged group with its most common spelling and lists every variant", () => {
    const groups = groupBudgetCodesByCostCentre([
      { ...makeCode("one"), costCentre: "fleet" },
      { ...makeCode("two"), costCentre: "Fleet" },
      { ...makeCode("three"), costCentre: "Fleet" },
      { ...makeCode("four"), costCentre: "FLEET" },
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0].label).toBe("Fleet");
    expect([...groups[0].spellings].sort()).toEqual(["FLEET", "Fleet", "fleet"]);
    expect(groups[0].codes).toHaveLength(4);
  });

  it("breaks an equal-count spelling tie by code-point order, whatever the input order", () => {
    const fleet = (id: string, costCentre: string) => ({ ...makeCode(id), costCentre });
    const forward = groupBudgetCodesByCostCentre([
      fleet("a", "Fleet"),
      fleet("b", "Fleet"),
      fleet("c", "FLEET"),
      fleet("d", "FLEET"),
    ]);
    const reversed = groupBudgetCodesByCostCentre([
      fleet("d", "FLEET"),
      fleet("c", "FLEET"),
      fleet("b", "Fleet"),
      fleet("a", "Fleet"),
    ]);
    // Upper-case letters precede lower-case ones in code-point order.
    expect(forward[0].label).toBe("FLEET");
    expect(reversed[0].label).toBe("FLEET");
    expect(forward[0].spellings).toEqual(["FLEET", "Fleet"]);
  });

  it("includes retired codes in their group", () => {
    const groups = groupBudgetCodesByCostCentre([
      { ...makeCode("old", "Expense", false), costCentre: "Fleet" },
    ]);
    expect(groups[0].codes.map((c) => c.id)).toEqual(["old"]);
  });

  it("agrees with budgetCodeGroupKey for every code it places", () => {
    const codes = [
      makeCode("rev", "Revenue"),
      makeCode("bare"),
      { ...makeCode("f"), costCentre: "fleet" },
      { ...makeCode("g"), costCentre: "FLEET" },
    ];
    for (const group of groupBudgetCodesByCostCentre(codes)) {
      for (const code of group.codes) expect(budgetCodeGroupKey(code)).toBe(group.key);
    }
  });
});

describe("matchesBudgetCodeQuery", () => {
  const fuel = {
    ...makeCode("zbb-fuel-01"),
    name: "Diesel and gasoline",
    description: "Pump fuel for the fleet",
    costCentre: "Fleet",
  };

  it("matches everything when the query is blank or whitespace", () => {
    expect(matchesBudgetCodeQuery(fuel, "")).toBe(true);
    expect(matchesBudgetCodeQuery(fuel, "   ")).toBe(true);
  });

  it("matches the code case-insensitively", () => {
    expect(matchesBudgetCodeQuery(fuel, "zbb-fuel")).toBe(true);
    expect(matchesBudgetCodeQuery(fuel, " ZBB-FUEL-01 ")).toBe(true);
  });

  it("matches a substring of the name", () => {
    expect(matchesBudgetCodeQuery(fuel, "gasol")).toBe(true);
  });

  it("matches a retired code — uniqueness counts it, so the planner must see it", () => {
    const retired = { ...fuel, active: false };
    expect(matchesBudgetCodeQuery(retired, "fuel")).toBe(true);
  });

  it("does not match on the cost centre or the description", () => {
    expect(matchesBudgetCodeQuery(fuel, "fleet")).toBe(false);
    expect(matchesBudgetCodeQuery(fuel, "pump")).toBe(false);
  });
});

describe("costCentreSuggestions", () => {
  it("offers each cost centre once, in its canonical spelling, A–Z", () => {
    expect(
      costCentreSuggestions([
        { ...makeCode("a"), costCentre: "Ops" },
        { ...makeCode("b"), costCentre: "fleet" },
        { ...makeCode("c"), costCentre: "Fleet" },
        { ...makeCode("d"), costCentre: "Fleet" },
      ]),
    ).toEqual(["Fleet", "Ops"]);
  });

  it("never offers a revenue code's value or a blank", () => {
    expect(
      costCentreSuggestions([
        { ...makeCode("rev", "Revenue"), costCentre: "Sales" },
        { ...makeCode("blank"), costCentre: "  " },
        makeCode("bare"),
      ]),
    ).toEqual([]);
  });

  it("is empty for a chart where no code has a cost centre, like the starter set", () => {
    expect(costCentreSuggestions([makeCode("a"), makeCode("b"), makeCode("r", "Revenue")])).toEqual(
      [],
    );
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

// zbbBalance: the dashboard's balance banner and the list row's verdict. No server rule to
// mirror — both totals are the server's own sums (BudgetPeriodResponse) — but the *judgement* on
// them is the zero-based rule this console exists to enforce, so all three arms are pinned here.
//
// The arm worth staring at is "unallocated". Its predecessor (netKind) read a surplus as
// "ontime", i.e. good news. Under zero-based, revenue with no expense line against it is the
// failure the rule exists to catch, so it carries the caution kind instead.

describe("zbbBalance", () => {
  it("is planned revenue less planned expense: a surplus is unallocated money, not good news", () => {
    expect(zbbBalance(period("Draft", 1_000, 250))).toEqual({ amount: 750, state: "unallocated" });
  });

  it("is over when assigned expense exceeds planned revenue", () => {
    expect(zbbBalance(period("Draft", 250, 1_000))).toEqual({ amount: -750, state: "over" });
  });

  it("is balanced when every planned dollar of revenue is assigned", () => {
    expect(zbbBalance(period("Draft", 1_000, 1_000))).toEqual({ amount: 0, state: "balanced" });
  });

  it("calls an unplanned period balanced — arithmetically true, which is why the views ask hasLines", () => {
    expect(zbbBalance(period("Draft", 0, 0))).toEqual({ amount: 0, state: "balanced" });
  });

  it.each<["balanced" | "unallocated" | "over", string]>([
    ["balanced", "ontime"],
    ["unallocated", "soon"],
    ["over", "over"],
  ])("maps %s to the %s status kind", (state, kind) => {
    expect(ZBB_BALANCE_KINDS[state]).toBe(kind);
  });
});

describe("zbbBalanceDisplay", () => {
  it("names the unallocated figure in words, not just in colour", () => {
    const d = zbbBalanceDisplay(zbbBalance(period("Draft", 1_000, 250)), true);

    expect(d.kind).toBe("soon");
    expect(d.headline).toBe("Unallocated $750");
  });

  it("names the overspend figure the same way", () => {
    const d = zbbBalanceDisplay(zbbBalance(period("Draft", 250, 1_000)), true);

    expect(d.kind).toBe("over");
    expect(d.headline).toBe("Over budget by $750");
  });

  it("reads Balanced only when there is a plan to balance", () => {
    const d = zbbBalanceDisplay(zbbBalance(period("Draft", 1_000, 1_000)), true);

    expect(d.kind).toBe("ontime");
    expect(d.headline).toBe("Balanced");
  });

  it("reads \"Nothing planned yet\" for an empty plan — zero equals zero is the wrong thing to say", () => {
    const d = zbbBalanceDisplay(zbbBalance(period("Draft", 0, 0)), false);

    expect(d.kind).toBe("info");
    expect(d.headline).toBe("Nothing planned yet");
  });
});

// The period list has the two totals but not the lines, so it answers "is anything planned?"
// from the totals; the dashboard, which fetches the lines, answers from lines.length.

describe("hasPlannedAmounts", () => {
  it("is false only when both totals are zero", () => {
    expect(hasPlannedAmounts(period("Draft", 0, 0))).toBe(false);
    expect(hasPlannedAmounts(period("Draft", 1_000, 0))).toBe(true);
    expect(hasPlannedAmounts(period("Draft", 0, 1_000))).toBe(true);
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

// planningProgress feeds the dashboard's checklist and stepper from one derivation: two line
// steps (done once any line exists), the zero-based balance step, then the five lifecycle states
// relative to PERIOD_STATE_ORDER.

describe("planningProgress", () => {
  const codes = [
    makeCode("rev-a", "Revenue"),
    makeCode("exp-a", "Expense"),
    makeCode("exp-b", "Expense"),
  ];
  const lineSteps = (steps: ReturnType<typeof planningProgress>) =>
    steps.filter((s): s is LineStep => s.group === "lines");
  const balanceStep = (steps: ReturnType<typeof planningProgress>) =>
    steps.find((s): s is BalanceStep => s.group === "balance")!;
  const lifecycle = (steps: ReturnType<typeof planningProgress>) =>
    steps.filter((s): s is LifecycleStep => s.group === "lifecycle").map((s) => [s.id, s.status]);

  it("marks both line steps pending for an empty Draft, and Draft current", () => {
    const steps = planningProgress(period("Draft"), [], codes);

    expect(lineSteps(steps).map((s) => [s.id, s.count, s.done])).toEqual([
      ["revenue", 0, false],
      ["expense", 0, false],
    ]);
    expect(lifecycle(steps)).toEqual([
      ["Draft", "current"],
      ["Finalized", "pending"],
      ["Open", "pending"],
      ["InReview", "pending"],
      ["Closed", "pending"],
    ]);
  });

  it("marks a line step done once it has a line, with coverage in the detail", () => {
    const lines = [makeLine("rev-a", "Revenue"), makeLine("exp-a", "Expense")];
    const steps = lineSteps(planningProgress(period("Draft"), lines, codes));

    expect(steps.map((s) => [s.id, s.count, s.done])).toEqual([
      ["revenue", 1, true],
      ["expense", 1, true],
    ]);
    // The whole supporting sentence is written here, not in PlanningChecklist, so the view can
    // stay a pure render of what this function decides.
    expect(steps[0].detail).toBe("1 line · 1 of 1 active revenue codes planned");
    expect(steps[1].detail).toBe("1 line · 1 of 2 active expense codes planned");
  });

  // The third checklist row is the zero-based rule itself. It is a warning, never a gate:
  // BudgetPeriod.Transition still finalizes an unbalanced plan, deliberately.

  it("marks the balance step done only when the plan balances and has lines", () => {
    const lines = [makeLine("rev-a", "Revenue", true, 1_000), makeLine("exp-a", "Expense", true, 1_000)];
    const step = balanceStep(planningProgress(period("Draft", 1_000, 1_000), lines, codes));

    expect(step.done).toBe(true);
    expect(step.empty).toBe(false);
    expect(step.balance).toEqual({ amount: 0, state: "balanced" });
    expect(step.detail).toBe("Balanced");
  });

  it("leaves it pending, naming the figure, when revenue is not fully assigned", () => {
    const lines = [makeLine("rev-a", "Revenue", true, 1_000)];
    const step = balanceStep(planningProgress(period("Draft", 1_000, 250), lines, codes));

    expect(step.done).toBe(false);
    expect(step.balance.state).toBe("unallocated");
    expect(step.detail).toBe("Unallocated $750");
  });

  it("leaves it pending when expense exceeds planned revenue", () => {
    const lines = [makeLine("exp-a", "Expense", true, 1_000)];
    const step = balanceStep(planningProgress(period("Draft", 250, 1_000), lines, codes));

    expect(step.done).toBe(false);
    expect(step.balance.state).toBe("over");
    expect(step.detail).toBe("Over budget by $750");
  });

  it("refuses to call an empty plan balanced — zero equals zero is not an assigned dollar", () => {
    const step = balanceStep(planningProgress(period("Draft", 0, 0), [], codes));

    expect(step.balance.state).toBe("balanced");
    expect(step.empty).toBe(true);
    expect(step.done).toBe(false);
    expect(step.detail).toBe("Nothing planned yet");
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

  it("puts the two line steps first, then the balance step, then the five states in order", () => {
    const steps = planningProgress(period("Draft"), [], codes);

    expect(steps.map((s) => s.id)).toEqual([
      "revenue",
      "expense",
      "balance",
      ...PERIOD_STATE_ORDER,
    ]);
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

// ---------------------------------------------------------------------------
// Actual transactions. Everything below re-derives a rule the server owns, so each block names
// the C# it mirrors — that naming is the only thing keeping the two copies honest.
// ---------------------------------------------------------------------------

const rollupRow = (
  code: string,
  category: BudgetCodeCategory,
  plannedCad: number,
  actualCad: number,
): BudgetRollupRowRecord => ({
  budgetCodeId: `id-${code}`,
  code,
  name: `Code ${code}`,
  category,
  plannedCad,
  actualCad,
  // As the server computes it (BudgetRollupBuilder.ToRow): actual − planned, plainly.
  deltaCad: actualCad - plannedCad,
});

// toBudgetRollup / rollupDeltaPct: the console's half of the rollup. The server ships both sides
// of the outer join (BudgetRollupBuilder.Build) and no band at all, so what is pinned here is
// the grouping, the per-category totals, and the percentage that feeds varianceKind's bands.

describe("toBudgetRollup", () => {
  const rows = [
    rollupRow("REV-A", "Revenue", 100_000, 90_000),
    rollupRow("REV-B", "Revenue", 0, 12_000),
    rollupRow("EXP-A", "Expense", 40_000, 0),
    rollupRow("EXP-B", "Expense", 10_000, 11_000),
  ];

  it("splits the rows by category and never sums the two sides together", () => {
    const rollup = toBudgetRollup(rows);

    expect(rollup.revenue.rows.map((r) => r.code)).toEqual(["REV-A", "REV-B"]);
    expect(rollup.expense.rows.map((r) => r.code)).toEqual(["EXP-A", "EXP-B"]);
    expect(rollup.revenue.plannedCad).toBe(100_000);
    expect(rollup.revenue.actualCad).toBe(102_000);
    expect(rollup.expense.plannedCad).toBe(50_000);
    expect(rollup.expense.actualCad).toBe(11_000);
  });

  it("keeps a code spent against but never planned — the row zero-based budgeting exists to surface", () => {
    const unplanned = toBudgetRollup(rows).revenue.rows.find((r) => r.code === "REV-B");

    expect(unplanned).toBeDefined();
    expect(unplanned!.plannedCad).toBe(0);
    expect(unplanned!.deltaCad).toBe(12_000);
  });

  it("keeps a code planned but never spent against", () => {
    const unspent = toBudgetRollup(rows).expense.rows.find((r) => r.code === "EXP-A");

    expect(unspent).toBeDefined();
    expect(unspent!.actualCad).toBe(0);
    expect(unspent!.deltaCad).toBe(-40_000);
  });

  it("totals each side from the rows' own deltas, so a total cannot disagree with its column", () => {
    const rollup = toBudgetRollup(rows);

    expect(rollup.revenue.deltaCad).toBe(2_000);
    expect(rollup.expense.deltaCad).toBe(-39_000);
  });

  it("reports an empty rollup, which is the screen's empty state", () => {
    expect(toBudgetRollup([]).empty).toBe(true);
    expect(toBudgetRollup(rows).empty).toBe(false);
  });
});

describe("rollupDeltaPct", () => {
  it("has no baseline for a code with actuals but no plan", () => {
    // planned 0 is not an infinite overrun — varianceKind(null) is the "off" band, "No baseline".
    expect(rollupDeltaPct(rollupRow("REV-B", "Revenue", 0, 12_000))).toBeNull();
    expect(varianceKind(rollupDeltaPct(rollupRow("REV-B", "Revenue", 0, 12_000)))).toBe("off");
    expect(varianceLabel(null)).toBe("No baseline");
  });

  it("reports a plain −100% for a code with a plan but no actuals", () => {
    expect(rollupDeltaPct(rollupRow("EXP-A", "Expense", 40_000, 0))).toBe(-100);
    expect(varianceKind(-100)).toBe("over");
  });

  it("is the signed delta as a share of what was planned", () => {
    expect(rollupDeltaPct(rollupRow("EXP-B", "Expense", 10_000, 11_000))).toBeCloseTo(10, 10);
    expect(rollupDeltaPct(rollupRow("REV-A", "Revenue", 100_000, 90_000))).toBeCloseTo(-10, 10);
  });
});

// periodCovering / actualDateError mirror CoveringPeriodRule.ValidateAsync and
// BudgetPeriod.Covers (inclusive on both ends). A transaction whose date falls in no period is
// refused rather than stored orphaned, and the form says so before the round trip.

describe("periodCovering", () => {
  const q4 = period("Open");
  const q1 = {
    ...period("Draft"),
    id: "period-q1",
    label: "FY2027 Q1",
    startsOn: "2027-01-01",
    endsOn: "2027-03-31",
  };
  const periods = [q4, q1];

  it("includes both end days — BudgetPeriod.Covers is inclusive", () => {
    expect(periodCovering(periods, "2026-10-01")?.id).toBe(q4.id);
    expect(periodCovering(periods, "2026-12-31")?.id).toBe(q4.id);
  });

  it("finds the period a date falls inside", () => {
    expect(periodCovering(periods, "2026-11-15")?.id).toBe(q4.id);
    expect(periodCovering(periods, "2027-02-02")?.id).toBe(q1.id);
  });

  it("returns null for a date in the gap between periods, and for no date at all", () => {
    expect(periodCovering(periods, "2025-12-31")).toBeNull();
    expect(periodCovering(periods, "")).toBeNull();
  });
});

describe("actualDateError", () => {
  const periods = [period("Open")];

  it("accepts a date inside a period", () => {
    expect(actualDateError("2026-11-15", periods)).toBeNull();
  });

  it("requires a date", () => {
    expect(actualDateError("", periods)).toContain("Enter the date");
  });

  it("refuses a date no period covers, naming the way out", () => {
    // Mirrors ActualTransactionErrors.NoPeriodCoversDate — the server's own 409 message is
    // shown verbatim when it still comes back; this is the same rule, said sooner.
    const problem = actualDateError("2030-01-01", periods);

    expect(problem).toContain("No budget period covers that date");
    expect(problem).toContain("Create the period first");
  });
});

// actualCodeCandidates mirrors the asymmetry between the two handlers:
// RecordActualTransactionCommandHandler refuses a retired code outright
// (ActualTransactionErrors.CodeRetired), while UpdateActualTransactionCommandHandler refuses
// only a move ONTO one — so a row already sitting on a retired code stays correctable in place.

describe("actualCodeCandidates", () => {
  const codes = [
    makeCode("rev-a", "Revenue"),
    makeCode("exp-a", "Expense"),
    makeCode("exp-retired", "Expense", false),
    makeCode("rev-retired", "Revenue", false),
  ];

  it("offers only active codes for a new transaction, both categories", () => {
    expect(actualCodeCandidates(codes, null).map((c) => c.id)).toEqual(["rev-a", "exp-a"]);
  });

  it("keeps the edited row's own code selectable even once it is retired", () => {
    expect(actualCodeCandidates(codes, "exp-retired").map((c) => c.id)).toEqual([
      "rev-a",
      "exp-a",
      "exp-retired",
    ]);
  });

  it("still offers no other retired code while editing — a move onto one is refused", () => {
    expect(actualCodeCandidates(codes, "exp-retired").map((c) => c.id)).not.toContain(
      "rev-retired",
    );
  });

  it("preserves the chart's order", () => {
    const reversed = [...codes].reverse();

    expect(actualCodeCandidates(reversed, null).map((c) => c.id)).toEqual(["exp-a", "rev-a"]);
  });
});

// actualAmountError / actualDescriptionError mirror ActualTransaction.Validate (AmountRequired /
// AmountNegative / AmountTooLarge / DescriptionRequired / DescriptionTooLong). The whole-dollar
// rule is this app's own, stricter than the server's, exactly as it is for allocations.

describe("actualAmountError", () => {
  it.each(["0", "1", "139400", " 250 "])("accepts %j", (text) => {
    expect(actualAmountError(text)).toBeNull();
  });

  it.each(["", "   ", "abc"])("requires an amount for %j", (text) => {
    expect(actualAmountError(text)).toBe("Enter an amount.");
  });

  it("refuses cents — the app enters whole dollars", () => {
    expect(actualAmountError("12.50")).toContain("whole dollars");
  });

  it("refuses a negative amount, naming the convention that replaces the minus sign", () => {
    // ActualTransactionErrors.AmountNegative: an expense is a positive amount on an expense
    // code, so the sign has no second job to do.
    expect(actualAmountError("-1")).toContain("positive amount on an expense code");
  });

  it("refuses an amount beyond decimal(12,2)", () => {
    expect(actualAmountError("1000000000")).toContain("too large");
  });
});

describe("actualDescriptionError", () => {
  it("accepts a description", () => {
    expect(actualDescriptionError("Fuel, Norman Wells depot, invoice 4471")).toBeNull();
  });

  it.each(["", "   "])("requires one for %j", (text) => {
    expect(actualDescriptionError(text)).toContain("what this transaction was for");
  });

  it("accepts exactly the maximum and refuses one more", () => {
    expect(actualDescriptionError("x".repeat(ACTUAL_DESCRIPTION_MAX_LENGTH))).toBeNull();
    expect(actualDescriptionError("x".repeat(ACTUAL_DESCRIPTION_MAX_LENGTH + 1))).toContain(
      "300 characters or fewer",
    );
  });
});
