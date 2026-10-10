import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  createCostCentre,
  deleteCostCentre,
  getCostCentre,
  getCostCentreRollup,
  listCostCentres,
  setCostCentreActive,
  updateCostCentre,
  type CostCentreInput,
  type CostCentreRollup,
} from "./budgeting";
import { COST_CENTRE_MESSAGES, BUDGET_CODE_COST_CENTRE_MESSAGES } from "../costCentres";

// What each cost-centre request puts on the wire — route, method, body — and that every 4xx the
// server documents reaches the caller verbatim. The budgeting.requests.test.ts shape, in its own
// file so the cost-centre slice stays one reviewable unit.
//
// Routes: Backend/src/Budgeting/Infrastructure/Endpoints/BudgetingEndpoints.cs. The register is
// TENANT-WIDE (cost-centres*, never under periods/{id}); only the rollup is per period. All of
// them sit in the /api/budgeting group carrying the BudgetAccess policy.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("../auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

const noContent = () => new Response(null, { status: 204 });
const callPath = (n = 0) => String(fetchMock.mock.calls[n]?.[0]);
const callInit = (n = 0) => (fetchMock.mock.calls[n]?.[1] ?? {}) as RequestInit;

const CC_ID = "7c1d2e3f-0000-4000-8000-0000000000c1";
const PERIOD_ID = "7c1d2e3f-0000-4000-8000-0000000000b1";

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

const input: CostCentreInput = {
  code: "Thompson",
  name: "Thompson base",
  description: null,
  ownerUserId: "user-1",
  parentId: null,
};

describe("cost-centre register requests", () => {
  it("lists with includeInactive written out either way", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [])).mockResolvedValueOnce(jsonResponse(200, []));

    await listCostCentres({ includeInactive: true });
    await listCostCentres({ includeInactive: false });

    expect(callPath(0)).toBe("/api/budgeting/cost-centres?includeInactive=true");
    expect(callPath(1)).toBe("/api/budgeting/cost-centres?includeInactive=false");
    expect(callInit(0).method).toBeUndefined();
  });

  it("gets one entry by id", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: CC_ID }));

    await getCostCentre(CC_ID);

    expect(callPath()).toBe(`/api/budgeting/cost-centres/${CC_ID}`);
  });

  it("POSTs the whole CostCentreRequest — every key, explicit nulls, code case kept — and returns the 201's id", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: CC_ID }));

    await expect(createCostCentre(input)).resolves.toBe(CC_ID);

    expect(callPath()).toBe("/api/budgeting/cost-centres");
    expect(callInit().method).toBe("POST");
    expect(JSON.parse(String(callInit().body))).toEqual({
      code: "Thompson",
      name: "Thompson base",
      description: null,
      ownerUserId: "user-1",
      parentId: null,
    });
  });

  it("PUTs an update to the entry's route WITHOUT a code — the code is immutable", async () => {
    fetchMock.mockResolvedValueOnce(noContent());
    const { code: _code, ...update } = input;
    void _code;

    await expect(updateCostCentre(CC_ID, { ...update, parentId: "parent-1" })).resolves.toBeUndefined();

    expect(callPath()).toBe(`/api/budgeting/cost-centres/${CC_ID}`);
    expect(callInit().method).toBe("PUT");
    const body = JSON.parse(String(callInit().body));
    expect(body).toEqual({ ...update, parentId: "parent-1" });
    expect(body).not.toHaveProperty("code");
  });

  // Built from a boolean, like setBudgetCodeActive: an inverted ternary is a 204 either way.
  it("posts to /activate when active is true", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setCostCentreActive(CC_ID, true);

    expect(callPath()).toBe(`/api/budgeting/cost-centres/${CC_ID}/activate`);
    expect(callInit().method).toBe("POST");
  });

  it("posts to /deactivate when active is false", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setCostCentreActive(CC_ID, false);

    expect(callPath()).toBe(`/api/budgeting/cost-centres/${CC_ID}/deactivate`);
    expect(callInit().method).toBe("POST");
  });

  it("DELETEs by id, 204 → undefined", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await expect(deleteCostCentre(CC_ID)).resolves.toBeUndefined();

    expect(callPath()).toBe(`/api/budgeting/cost-centres/${CC_ID}`);
    expect(callInit().method).toBe("DELETE");
  });

  it("never builds a register route under a period", async () => {
    fetchMock.mockResolvedValue(noContent());

    await setCostCentreActive(CC_ID, false);
    await deleteCostCentre(CC_ID);
    await updateCostCentre(CC_ID, { name: "x", description: null, ownerUserId: null, parentId: null });

    for (const [url] of fetchMock.mock.calls) {
      expect(String(url)).toMatch(/^\/api\/budgeting\/cost-centres/);
    }
  });
});

describe("cost-centre refusals reach the caller verbatim", () => {
  // Each pair is (status, error code) as CostCentreErrors declares it; the message is
  // COST_CENTRE_MESSAGES, itself pinned to CostCentreErrors in lib/costCentres.test.ts.
  const cases: [number, keyof typeof COST_CENTRE_MESSAGES, () => Promise<unknown>][] = [
    [400, "CodeRequired", () => createCostCentre({ ...input, code: " " })],
    [400, "CodeTooLong", () => createCostCentre(input)],
    [400, "NameRequired", () => createCostCentre(input)],
    [400, "NameTooLong", () => createCostCentre(input)],
    [400, "DescriptionTooLong", () => createCostCentre(input)],
    [409, "DuplicateCode", () => createCostCentre(input)],
    [404, "OwnerNotFound", () => createCostCentre(input)],
    [404, "ParentNotFound", () => createCostCentre(input)],
    [400, "ParentIsNotTopLevel", () => createCostCentre(input)],
    [409, "ParentRetired", () => createCostCentre(input)],
    [400, "ParentIsSelf", () => updateCostCentre(CC_ID, { ...input, parentId: CC_ID })],
    [409, "HasChildrenCannotHaveParent", () => updateCostCentre(CC_ID, input)],
    [400, "CodeImmutable", () => updateCostCentre(CC_ID, input)],
    [404, "NotFound", () => updateCostCentre(CC_ID, input)],
    [409, "HasActiveChildren", () => setCostCentreActive(CC_ID, false)],
    [409, "HasChildren", () => deleteCostCentre(CC_ID)],
    [409, "InUse", () => deleteCostCentre(CC_ID)],
  ];

  it.each(cases)("%d Budgeting.CostCentre.%s", async (status, suffix, call) => {
    const code = `Budgeting.CostCentre.${suffix}`;
    const message = COST_CENTRE_MESSAGES[suffix];
    fetchMock.mockResolvedValueOnce(jsonResponse(status, { code, message }));

    await expect(call()).rejects.toMatchObject({ code, message, status });
  });

  it("the InUse message names retiring as the alternative", () => {
    expect(COST_CENTRE_MESSAGES.InUse).toContain("Retire it instead");
  });

  it.each<[number, keyof typeof BUDGET_CODE_COST_CENTRE_MESSAGES]>([
    [400, "CostCentreNotFound"],
    [409, "CostCentreRetired"],
  ])("a budget-code write's %d Budgeting.Code.%s is surfaced verbatim", async (status, suffix) => {
    const { createBudgetCode } = await import("./budgeting");
    const code = `Budgeting.Code.${suffix}`;
    const message = BUDGET_CODE_COST_CENTRE_MESSAGES[suffix];
    fetchMock.mockResolvedValueOnce(jsonResponse(status, { code, message }));

    await expect(
      createBudgetCode(PERIOD_ID, {
        code: "FUEL",
        name: "Fuel",
        description: null,
        category: "Expense",
        serviceLine: null,
        costCentre: "Thompson",
        parentCodeId: null,
        glAccountCode: null,
        taxTreatment: null,
        budgetOwnerUserId: null,
        reviewFrequency: "Quarterly",
      }),
    ).rejects.toMatchObject({ code, message, status });
  });
});

describe("getCostCentreRollup", () => {
  it("GETs the period's rollup and returns it untouched — planned only, no actual field", async () => {
    const body: CostCentreRollup = {
      periodId: PERIOD_ID,
      costCentres: [
        {
          costCentreId: CC_ID,
          code: "Thompson",
          name: "Thompson base",
          isActive: true,
          parentId: null,
          parentCode: null,
          ownerUserId: null,
          ownerName: null,
          ownerEmail: null,
          budgetCodeCount: 2,
          itemCount: 3,
          plannedCad: 1200.5,
        },
      ],
      noCostCentre: { budgetCodeCount: 1, itemCount: 1, plannedCad: 99.5 },
      totalPlannedExpenseCad: 1300,
    };
    fetchMock.mockResolvedValueOnce(jsonResponse(200, body));

    const result = await getCostCentreRollup(PERIOD_ID);

    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/rollups/cost-centres`);
    expect(callInit().method).toBeUndefined();
    expect(result).toEqual(body);
    expect(result.costCentres[0]).not.toHaveProperty("actualCad");
  });

  it("surfaces the period's 404 verbatim", async () => {
    const err = { code: "Budgeting.Period.NotFound", message: "The budget period was not found." };
    fetchMock.mockResolvedValueOnce(jsonResponse(404, err));

    await expect(getCostCentreRollup(PERIOD_ID)).rejects.toMatchObject({ ...err, status: 404 });
  });
});
