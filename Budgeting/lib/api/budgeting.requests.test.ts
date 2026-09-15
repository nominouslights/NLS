import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  createBudgetCode,
  createBudgetPeriod,
  deleteBudgetCode,
  listBudgetCodes,
  listBudgetOwnerCandidates,
  listBudgetPeriods,
  seedStarterBudgetCodes,
  setBudgetCodeActive,
  updateBudgetCode,
  type BudgetCodeInput,
  type BudgetCodeUpdateInput,
} from "./budgeting";
import { ApiError } from "./transport";

// The request half of the budgeting client. budgeting.test.ts covers the pure mirrors of server
// rules; this covers what actually goes on the wire — route, method, body — because a wrong
// route here is a 404 the UI reports as a generic failure, and nothing else pins these strings.
//
// The routes below are the ones documented in Budgeting/CLAUDE.md's table and served by
// Backend/src/Budgeting/Infrastructure/Endpoints/BudgetingEndpoints.cs. Every one of them sits
// in the /api/budgeting group that carries the BudgetAccess policy — the real security
// boundary, of which RoleGate is only the UX shadow.

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

function noContent(): Response {
  return new Response(null, { status: 204 });
}

function callPath(n = 0): string {
  return String(fetchMock.mock.calls[n]?.[0]);
}

function callInit(n = 0): RequestInit {
  return (fetchMock.mock.calls[n]?.[1] ?? {}) as RequestInit;
}

const CODE_ID = "5f2b1e1c-0000-4000-8000-000000000001";

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("setBudgetCodeActive", () => {
  // The one route in this client built from a boolean. Two routes rather than a body flag is
  // the backend's shape, so an inverted ternary here would silently activate a code the planner
  // asked to retire — a 204 either way, with no error anywhere to notice it by.

  it("posts to /activate when active is true", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setBudgetCodeActive(CODE_ID, true);

    expect(callPath()).toBe(`/api/budgeting/codes/${CODE_ID}/activate`);
    expect(callInit().method).toBe("POST");
  });

  it("posts to /deactivate when active is false", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setBudgetCodeActive(CODE_ID, false);

    expect(callPath()).toBe(`/api/budgeting/codes/${CODE_ID}/deactivate`);
    expect(callInit().method).toBe("POST");
  });

  it("never sends the opposite verb", async () => {
    fetchMock.mockResolvedValue(noContent());

    await setBudgetCodeActive(CODE_ID, false);
    await setBudgetCodeActive(CODE_ID, true);

    expect(callPath(0)).not.toContain("/activate");
    expect(callPath(1)).not.toContain("/deactivate");
  });
});

describe("deleteBudgetCode", () => {
  it("issues a DELETE to the code's own route", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await deleteBudgetCode(CODE_ID);

    expect(callPath()).toBe(`/api/budgeting/codes/${CODE_ID}`);
    expect(callInit().method).toBe("DELETE");
  });

  it("surfaces the server's 409 message verbatim", async () => {
    // CLAUDE.md: the server's 409 names retirement as the alternative, so passing it through
    // unchanged is the correct handling — screens/BudgetCodes.tsx renders e.message directly.
    // A generic "Delete failed." here would strip the only instruction the user gets.
    const serverMessage =
      "This code has been used and cannot be deleted. Retire it instead so existing rows keep resolving.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "BudgetCode.InUse", message: serverMessage }),
    );

    const err = await deleteBudgetCode(CODE_ID).catch((e: unknown) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).message).toBe(serverMessage);
    expect(err).toMatchObject({ code: "BudgetCode.InUse", status: 409 });
  });

  it("surfaces the children-block 409 verbatim too", async () => {
    const serverMessage = "This code has child codes and cannot be deleted.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "BudgetCode.HasChildren", message: serverMessage }),
    );

    await expect(deleteBudgetCode(CODE_ID)).rejects.toMatchObject({
      code: "BudgetCode.HasChildren",
      message: serverMessage,
      status: 409,
    });
  });
});

describe("period requests", () => {
  it("lists periods with a GET", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, [{ id: "p1", label: "March 2026" }]));

    await expect(listBudgetPeriods()).resolves.toEqual([{ id: "p1", label: "March 2026" }]);
    expect(callPath()).toBe("/api/budgeting/periods");
    expect(callInit().method).toBeUndefined();
  });

  it("posts a period and returns only the new id", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: "p9" }));

    await expect(
      createBudgetPeriod({ granularity: "Quarter", year: 2026, ordinal: 4 }),
    ).resolves.toBe("p9");

    expect(callPath()).toBe("/api/budgeting/periods");
    expect(callInit().method).toBe("POST");
    expect(JSON.parse(String(callInit().body))).toEqual({
      granularity: "Quarter",
      year: 2026,
      ordinal: 4,
    });
  });
});

describe("code requests", () => {
  const input: BudgetCodeInput = {
    code: "ZBB-CREW-01",
    name: "Alamos crew shuttle",
    description: null,
    category: "Revenue",
    serviceLine: "ContractCrew",
    costCentre: null,
    parentCodeId: null,
    glAccountCode: null,
    taxTreatment: null,
    budgetOwnerUserId: null,
    reviewFrequency: "Quarterly",
  };

  it("lists codes with a GET", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));

    await expect(listBudgetCodes()).resolves.toEqual([]);
    expect(callPath()).toBe("/api/budgeting/codes");
  });

  it("lists owner candidates from the codes/owners route", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, [{ userId: "u1", email: "owner@northernlink.ca", role: "Owner" }]),
    );

    await expect(listBudgetOwnerCandidates()).resolves.toEqual([
      { userId: "u1", email: "owner@northernlink.ca", role: "Owner" },
    ]);
    expect(callPath()).toBe("/api/budgeting/codes/owners");
  });

  it("posts a new code and returns only the new id", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: "c9" }));

    await expect(createBudgetCode(input)).resolves.toBe("c9");
    expect(callPath()).toBe("/api/budgeting/codes");
    expect(callInit().method).toBe("POST");
  });

  it("sends cleared optional fields as explicit nulls, not omissions", async () => {
    // "Cleared" and "unchanged" must not look the same on the wire — the backend reads an
    // absent key as no change, so omitting a nulled field would make clearing it impossible.
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: "c9" }));

    await createBudgetCode(input);

    const body = JSON.parse(String(callInit().body)) as Record<string, unknown>;
    expect(body).toHaveProperty("costCentre", null);
    expect(body).toHaveProperty("glAccountCode", null);
    expect(body).toHaveProperty("budgetOwnerUserId", null);
  });

  it("PUTs an update without a code field — the code string is set once", async () => {
    // There is no rename endpoint: allocations and actuals reference a code by string, so a
    // rename would orphan every row already tagged. A `code` key here would be a 400 at best.
    fetchMock.mockResolvedValueOnce(noContent());

    // Spelled out rather than spread-minus-code, so the absence of `code` is the assertion's
    // premise and not an artefact of how the fixture was built.
    const update: BudgetCodeUpdateInput = {
      name: input.name,
      description: input.description,
      category: input.category,
      serviceLine: input.serviceLine,
      costCentre: input.costCentre,
      parentCodeId: input.parentCodeId,
      glAccountCode: input.glAccountCode,
      taxTreatment: input.taxTreatment,
      budgetOwnerUserId: input.budgetOwnerUserId,
      reviewFrequency: input.reviewFrequency,
    };
    await expect(updateBudgetCode(CODE_ID, update)).resolves.toBeUndefined();

    expect(callPath()).toBe(`/api/budgeting/codes/${CODE_ID}`);
    expect(callInit().method).toBe("PUT");
    expect(JSON.parse(String(callInit().body))).not.toHaveProperty("code");
  });

  it("posts the starter set and returns how many it created", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { created: 12 }));

    await expect(seedStarterBudgetCodes()).resolves.toEqual({ created: 12 });
    expect(callPath()).toBe("/api/budgeting/codes/starter-set");
    expect(callInit().method).toBe("POST");
  });

  it("reports the starter set as idempotent on a second call", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { created: 0 }));

    await expect(seedStarterBudgetCodes()).resolves.toEqual({ created: 0 });
  });
});
