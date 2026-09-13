import { describe, expect, it } from "vitest";
import {
  allocationAmountError,
  allocationCandidates,
  allocationJustificationError,
  budgetCodeCategoryKind,
  budgetCodeFormatError,
  canEditAllocations,
  costCentreApplies,
  coverage,
  netCad,
  netKind,
  netLabel,
  nextTransition,
  normalizeBudgetCode,
  parentCandidates,
  periodKind,
  planningProgress,
  previewPeriod,
  stateAfter,
  toBudgetCode,
  toBudgetPeriod,
  ALLOCATION_JUSTIFICATION_MAX_LENGTH,
  PERIOD_STATE_LABELS,
  PERIOD_STATE_ORDER,
  REVIEW_FREQUENCY_LABELS,
  SERVICE_LINE_LABELS,
  TAX_TREATMENT_LABELS,
  type BudgetAllocationRecord,
  type BudgetCodeRecord,
  type BudgetPeriodRecord,
  type LifecycleStep,
  type LineStep,
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

// netCad / netKind / netLabel: the dashboard's Net tile. No server rule to mirror — the sums are
// the server's — but the kind and label are what keep the tile from being colour alone.

describe("netCad, netKind and netLabel", () => {
  it("is planned revenue less planned expense", () => {
    expect(netCad(period("Draft", 1_000, 250))).toBe(750);
    expect(netCad(period("Draft", 250, 1_000))).toBe(-750);
  });

  it.each<[number, string, string]>([
    [750, "ontime", "Surplus"],
    [0, "info", "Balanced"],
    [-750, "over", "Deficit"],
  ])("net %d → %s / %s", (net, kind, label) => {
    expect(netKind(net)).toBe(kind);
    expect(netLabel(net)).toBe(label);
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
// steps (done once any line exists) then the five lifecycle states relative to PERIOD_STATE_ORDER.

describe("planningProgress", () => {
  const codes = [
    makeCode("rev-a", "Revenue"),
    makeCode("exp-a", "Expense"),
    makeCode("exp-b", "Expense"),
  ];
  const lineSteps = (steps: ReturnType<typeof planningProgress>) =>
    steps.filter((s): s is LineStep => s.group === "lines");
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
    expect(steps[0].detail).toBe("1 of 1 active revenue codes planned");
    expect(steps[1].detail).toBe("1 of 2 active expense codes planned");
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

  it("puts the two line steps first, then the five states in order", () => {
    const steps = planningProgress(period("Draft"), [], codes);

    expect(steps.map((s) => s.id)).toEqual(["revenue", "expense", ...PERIOD_STATE_ORDER]);
  });
});
