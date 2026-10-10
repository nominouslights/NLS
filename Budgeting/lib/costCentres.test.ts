import { describe, expect, it } from "vitest";
import {
  BUDGET_CODE_COST_CENTRE_MESSAGES,
  COST_CENTRE_LIMITS,
  COST_CENTRE_MESSAGES,
  budgetCodeCostCentreError,
  costCentreError,
  costCentreOptions,
  costCentreReflects,
  costCentreStatus,
  findCostCentre,
  groupRollup,
  hasChildren,
  normalizeCostCentreCode,
  normalizeCostCentreText,
  parentCandidates,
  rollupSum,
  sameCostCentreCode,
} from "./costCentres";
import type { CostCentreRecord, CostCentreRollup, CostCentreRollupRow } from "./api/budgeting";

// The client-side mirrors of the cost-centre register's rules, each pinned to the C# it mirrors
// (Backend/src/Budgeting/Domain/CostCentres/, Application/CostCentres/,
// Application/Codes/BudgetCodeCostCentreRule.cs). Change a rule there, change it here.

function cc(id: string, over: Partial<CostCentreRecord> = {}): CostCentreRecord {
  return {
    id,
    code: id.toUpperCase(),
    name: `Centre ${id}`,
    description: null,
    ownerUserId: null,
    ownerName: null,
    ownerEmail: null,
    parentId: null,
    parentCode: null,
    parentName: null,
    isActive: true,
    createdBy: null,
    createdByName: null,
    createdByEmail: null,
    modifiedBy: null,
    modifiedByName: null,
    modifiedByEmail: null,
    createdAtUtc: "2026-10-01T00:00:00+00:00",
    updatedAtUtc: "2026-10-01T00:00:00+00:00",
    ...over,
  };
}

describe("limits and messages — CostCentre / CostCentreErrors", () => {
  it("mirrors CodeMaxLength (= BudgetCode.CostCentreMaxLength), NameMaxLength, DescriptionMaxLength", () => {
    expect(COST_CENTRE_LIMITS).toEqual({ codeMaxLength: 32, nameMaxLength: 120, descriptionMaxLength: 1000 });
  });

  it("carries the length messages with the constants interpolated as the server does", () => {
    expect(COST_CENTRE_MESSAGES.CodeTooLong).toBe("The cost-centre code must be 32 characters or fewer.");
    expect(COST_CENTRE_MESSAGES.NameTooLong).toBe("The name must be 120 characters or fewer.");
    expect(COST_CENTRE_MESSAGES.DescriptionTooLong).toBe("The description must be 1000 characters or fewer.");
  });

  it("names every CostCentreErrors member", () => {
    expect(Object.keys(COST_CENTRE_MESSAGES).sort()).toEqual(
      [
        "NotFound",
        "CodeRequired",
        "CodeTooLong",
        "CodeImmutable",
        "DuplicateCode",
        "NameRequired",
        "NameTooLong",
        "DescriptionTooLong",
        "OwnerNotFound",
        "ParentNotFound",
        "ParentIsSelf",
        "ParentIsNotTopLevel",
        "ParentRetired",
        "HasChildrenCannotHaveParent",
        "HasActiveChildren",
        "HasChildren",
        "InUse",
      ].sort(),
    );
  });

  it("carries BudgetCodeErrors.CostCentreNotFound / CostCentreRetired verbatim", () => {
    expect(BUDGET_CODE_COST_CENTRE_MESSAGES.CostCentreNotFound).toBe(
      "That cost centre is not in the cost-centre register. Add it to the register first, or choose an existing one.",
    );
    expect(BUDGET_CODE_COST_CENTRE_MESSAGES.CostCentreRetired).toBe(
      "That cost centre is retired and cannot be given to a budget code. Choose an active one, or restore it in the register first.",
    );
  });
});

describe("normalizeCostCentreCode / sameCostCentreCode — CostCentre.NormalizeCode + ordinal match", () => {
  it("trims only and keeps case", () => {
    expect(normalizeCostCentreCode("  Ops-01 ")).toBe("Ops-01");
    expect(normalizeCostCentreCode(null)).toBe("");
    expect(normalizeCostCentreCode("   ")).toBe("");
  });

  it("matches case-sensitively, after trimming", () => {
    expect(sameCostCentreCode(" OPS-01", "OPS-01 ")).toBe(true);
    expect(sameCostCentreCode("ops-01", "OPS-01")).toBe(false);
  });

  it("findCostCentre is ordinal too", () => {
    const register = [cc("a", { code: "Thompson" })];
    expect(findCostCentre(register, " Thompson ")?.id).toBe("a");
    expect(findCostCentre(register, "THOMPSON")).toBeNull();
    expect(findCostCentre(register, "")).toBeNull();
  });

  it("normalizeCostCentreText mirrors CostCentre.Normalize: blank → null, else trimmed", () => {
    expect(normalizeCostCentreText("  ")).toBeNull();
    expect(normalizeCostCentreText(null)).toBeNull();
    expect(normalizeCostCentreText(" x ")).toBe("x");
  });
});

describe("costCentreError — CostCentre.Create / UpdateCostCentreCommandHandler, in order", () => {
  const ok = { code: "Thompson", name: "Thompson base", description: null, parentId: null };

  it("accepts a valid entry", () => {
    expect(costCentreError(ok)).toBeNull();
  });

  it("on create: code required, then code length (both sides of 32), measured after trimming", () => {
    expect(costCentreError({ ...ok, code: "   " })).toBe(COST_CENTRE_MESSAGES.CodeRequired);
    expect(costCentreError({ ...ok, code: ` ${"x".repeat(32)} ` })).toBeNull();
    expect(costCentreError({ ...ok, code: "x".repeat(33) })).toBe(COST_CENTRE_MESSAGES.CodeTooLong);
  });

  it("code comes before name on create", () => {
    expect(costCentreError({ ...ok, code: "", name: "" })).toBe(COST_CENTRE_MESSAGES.CodeRequired);
  });

  it("name required, then name length (both sides of 120, trimmed)", () => {
    expect(costCentreError({ ...ok, name: "  " })).toBe(COST_CENTRE_MESSAGES.NameRequired);
    expect(costCentreError({ ...ok, name: ` ${"n".repeat(120)} ` })).toBeNull();
    expect(costCentreError({ ...ok, name: "n".repeat(121) })).toBe(COST_CENTRE_MESSAGES.NameTooLong);
  });

  it("description length (both sides of 1000, trimmed)", () => {
    expect(costCentreError({ ...ok, description: ` ${"d".repeat(1000)} ` })).toBeNull();
    expect(costCentreError({ ...ok, description: "d".repeat(1001) })).toBe(COST_CENTRE_MESSAGES.DescriptionTooLong);
  });

  it("on edit: no code rule (the code is not sent), and ParentIsSelf comes BEFORE the name rules", () => {
    expect(costCentreError({ ...ok, code: "" }, "me")).toBeNull();
    expect(costCentreError({ ...ok, name: "", parentId: "me" }, "me")).toBe(COST_CENTRE_MESSAGES.ParentIsSelf);
  });
});

describe("parentCandidates — CostCentreParentRule", () => {
  const top = cc("top");
  const retiredTop = cc("retired-top", { isActive: false });
  const child = cc("child", { parentId: "top" });
  const lone = cc("lone");

  it("on create offers active top-level entries only", () => {
    expect(parentCandidates([top, retiredTop, child, lone], null).map((c) => c.id)).toEqual(["top", "lone"]);
  });

  it("never offers the entry itself (ParentIsSelf)", () => {
    expect(parentCandidates([top, lone], "lone").map((c) => c.id)).toEqual(["top"]);
  });

  it("never offers an entry that has a parent (ParentIsNotTopLevel)", () => {
    expect(parentCandidates([top, child, lone], "lone").map((c) => c.id)).not.toContain("child");
  });

  it("keeps a CURRENT parent retired since, but never offers a retired one anew (ParentRetired)", () => {
    const underRetired = cc("under", { parentId: "retired-top" });
    expect(parentCandidates([retiredTop, top, underRetired], "under").map((c) => c.id)).toEqual(["retired-top", "top"]);
    expect(parentCandidates([retiredTop, top, lone], "lone").map((c) => c.id)).toEqual(["top"]);
  });

  it("offers nothing to an entry that already has children (HasChildrenCannotHaveParent)", () => {
    expect(parentCandidates([top, child, lone], "top")).toEqual([]);
  });

  it("counts a retired child as a child — HasChildrenAsync ignores status", () => {
    const retiredChild = cc("rc", { parentId: "top", isActive: false });
    expect(hasChildren([top, retiredChild], "top")).toBe(true);
    expect(parentCandidates([top, retiredChild, lone], "top")).toEqual([]);
  });
});

describe("budgetCodeCostCentreError — BudgetCodeCostCentreRule", () => {
  const register = [cc("a", { code: "Thompson" }), cc("b", { code: "Leaf", isActive: false })];

  it("accepts blank", () => {
    expect(budgetCodeCostCentreError("  ", null, register)).toBeNull();
  });

  it("accepts an active entry, matched after trimming", () => {
    expect(budgetCodeCostCentreError(" Thompson ", null, register)).toBeNull();
  });

  it("refuses a string the register lacks — including a case variant", () => {
    expect(budgetCodeCostCentreError("THOMPSON", null, register)).toBe(
      BUDGET_CODE_COST_CENTRE_MESSAGES.CostCentreNotFound,
    );
  });

  it("refuses a retired entry on create or on change", () => {
    expect(budgetCodeCostCentreError("Leaf", null, register)).toBe(BUDGET_CODE_COST_CENTRE_MESSAGES.CostCentreRetired);
    expect(budgetCodeCostCentreError("Leaf", "Thompson", register)).toBe(
      BUDGET_CODE_COST_CENTRE_MESSAGES.CostCentreRetired,
    );
  });

  it("always accepts an UNCHANGED value — retired or not in the register at all", () => {
    expect(budgetCodeCostCentreError("Leaf", "Leaf", register)).toBeNull();
    expect(budgetCodeCostCentreError("Gone", "Gone", register)).toBeNull();
  });

  it("leaves an over-length value to the aggregate, as the server does", () => {
    expect(budgetCodeCostCentreError("x".repeat(33), null, register)).toBeNull();
  });
});

describe("costCentreOptions — the budget-code form's picker", () => {
  const register = [
    cc("a", { code: "Leaf", name: "Leaf Rapids", isActive: false }),
    cc("b", { code: "Thompson", name: "Thompson base" }),
  ];

  it("offers active entries only, in register order, labelled code · name", () => {
    expect(costCentreOptions(register, null)).toEqual([
      { value: "Thompson", label: "Thompson · Thompson base", status: "active" },
    ]);
  });

  it("keeps a retired current value, marked retired, first", () => {
    const options = costCentreOptions(register, "Leaf");
    expect(options[0]).toEqual({ value: "Leaf", label: "Leaf · Leaf Rapids (retired — kept)", status: "retired" });
    expect(options).toHaveLength(2);
  });

  it("keeps a current value the register does not know, marked unregistered", () => {
    expect(costCentreOptions(register, "OPS-01")[0]).toMatchObject({ value: "OPS-01", status: "unregistered" });
  });

  it("does not duplicate an active current value", () => {
    expect(costCentreOptions(register, "Thompson")).toHaveLength(1);
  });

  it("offers nothing the server would refuse", () => {
    for (const current of [null, "Leaf", "OPS-01", "Thompson"]) {
      for (const o of costCentreOptions(register, current)) {
        expect(budgetCodeCostCentreError(o.value, current, register)).toBeNull();
      }
    }
  });
});

describe("costCentreStatus / costCentreReflects", () => {
  it("pairs a kind with a written label", () => {
    expect(costCentreStatus(true)).toEqual({ kind: "ontime", label: "Active" });
    expect(costCentreStatus(false)).toEqual({ kind: "off", label: "Retired" });
  });

  it("waits for the row to carry the written values, not merely to exist", () => {
    const row = cc("a", { name: "Old" });
    const expected = { name: "New", description: null, ownerUserId: null, parentId: null };
    expect(costCentreReflects([row], "a", expected)).toBe(false);
    expect(costCentreReflects([{ ...row, name: "New" }], "a", expected)).toBe(true);
    expect(costCentreReflects([], "a", expected)).toBe(false);
  });
});

function row(code: string, over: Partial<CostCentreRollupRow> = {}): CostCentreRollupRow {
  return {
    costCentreId: code.toLowerCase(),
    code,
    name: `${code} name`,
    isActive: true,
    parentId: null,
    parentCode: null,
    ownerUserId: null,
    ownerName: null,
    ownerEmail: null,
    budgetCodeCount: 1,
    itemCount: 1,
    plannedCad: 0,
    ...over,
  };
}

describe("groupRollup / rollupSum — the dashboard panel's shaping", () => {
  it("nests rows under a parent that is in the rollup, keeping server order, with a cents-exact subtotal", () => {
    const groups = groupRollup([
      row("NORTH", { plannedCad: 100.1 }),
      row("NORTH-A", { parentId: "north", parentCode: "NORTH", plannedCad: 0.2 }),
      row("SOUTH", { plannedCad: 5 }),
    ]);
    expect(groups.map((g) => g.row.code)).toEqual(["NORTH", "SOUTH"]);
    expect(groups[0].children.map((c) => c.code)).toEqual(["NORTH-A"]);
    expect(groups[0].subtotalCad).toBe(100.3);
    expect(groups[1].subtotalCad).toBe(5);
  });

  it("leaves a child whose parent is absent at the top level (its parentCode stays on the row)", () => {
    const groups = groupRollup([row("ORPHAN", { parentId: "gone", parentCode: "GONE" })]);
    expect(groups).toHaveLength(1);
    expect(groups[0].row.parentCode).toBe("GONE");
  });

  it("keeps an unregistered row (null id) as its own group", () => {
    const groups = groupRollup([row("Raw", { costCentreId: null })]);
    expect(groups[0].row.costCentreId).toBeNull();
    expect(groups[0].children).toEqual([]);
  });

  it("rows plus No cost centre sum to the server's total (CostCentrePlannedRollup.Build)", () => {
    const rollup: CostCentreRollup = {
      periodId: "p",
      costCentres: [row("A", { plannedCad: 0.1 }), row("B", { plannedCad: 0.2 })],
      noCostCentre: { budgetCodeCount: 1, itemCount: 2, plannedCad: 1234.56 },
      totalPlannedExpenseCad: 1234.86,
    };
    expect(rollupSum(rollup)).toBe(rollup.totalPlannedExpenseCad);
  });
});
