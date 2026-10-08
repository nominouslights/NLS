import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  copyBudgetAllocations,
  copyBudgetCodes,
  createBudgetItem,
  listBudgetAllocations,
  removeBudgetItem,
  updateBudgetItem,
  createBudgetCode,
  createBudgetPeriod,
  deleteBudgetCode,
  listBudgetCodes,
  listBudgetOwnerCandidates,
  listBudgetPeriods,
  seedStarterBudgetCodes,
  setBudgetCodeActive,
  updateBudgetCode,
  createVendor,
  deleteVendor,
  getVendor,
  listVendors,
  setVendorActive,
  updateVendor,
  type VendorInput,
  type BudgetCodeInput,
  type BudgetCodeUpdateInput,
  type BudgetItemInput,
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

// Every code route lives under the period since codes became per-period
// (BudgetingEndpoints.cs: periods/{id:guid}/codes...). The old tenant-wide codes* routes were
// REMOVED server-side, so a request still built the old way is a 404 — pinned per route below.
const PERIOD_ID = "5f2b1e1c-0000-4000-8000-0000000000b1";

describe("setBudgetCodeActive", () => {
  // The one route in this client built from a boolean. Two routes rather than a body flag is
  // the backend's shape, so an inverted ternary here would silently activate a code the planner
  // asked to retire — a 204 either way, with no error anywhere to notice it by.

  it("posts to the period's /activate route when active is true", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setBudgetCodeActive(PERIOD_ID, CODE_ID, true);

    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/codes/${CODE_ID}/activate`);
    expect(callInit().method).toBe("POST");
  });

  it("posts to the period's /deactivate route when active is false", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setBudgetCodeActive(PERIOD_ID, CODE_ID, false);

    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/codes/${CODE_ID}/deactivate`);
    expect(callInit().method).toBe("POST");
  });

  it("never sends the opposite verb", async () => {
    fetchMock.mockResolvedValue(noContent());

    await setBudgetCodeActive(PERIOD_ID, CODE_ID, false);
    await setBudgetCodeActive(PERIOD_ID, CODE_ID, true);

    expect(callPath(0)).not.toContain("/activate");
    expect(callPath(1)).not.toContain("/deactivate");
  });
});

describe("deleteBudgetCode", () => {
  it("issues a DELETE to the code's own route under its period", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await deleteBudgetCode(PERIOD_ID, CODE_ID);

    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/codes/${CODE_ID}`);
    expect(callInit().method).toBe("DELETE");
  });

  it("surfaces the server's 409 message verbatim", async () => {
    // CLAUDE.md: the server's 409 names retirement as the alternative, so passing it through
    // unchanged is the correct handling — screens/BudgetCodes.tsx renders e.message directly.
    // A generic "Delete failed." here would strip the only instruction the user gets.
    // Verbatim from BudgetCodeErrors.InUse.
    const serverMessage =
      "This budget code is referenced by budget allocations or actual transactions and cannot be deleted. Retire it instead — a retired code stays listed so existing rows keep resolving.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Budgeting.Code.InUse", message: serverMessage }),
    );

    const err = await deleteBudgetCode(PERIOD_ID, CODE_ID).catch((e: unknown) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).message).toBe(serverMessage);
    expect(err).toMatchObject({ code: "Budgeting.Code.InUse", status: 409 });
  });

  it("surfaces the children-block 409 verbatim too", async () => {
    const serverMessage = "This code has child codes and cannot be deleted.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "BudgetCode.HasChildren", message: serverMessage }),
    );

    await expect(deleteBudgetCode(PERIOD_ID, CODE_ID)).rejects.toMatchObject({
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

describe("copyBudgetAllocations", () => {
  // POST periods/{id}/allocations/copy, mapped inside the BudgetAccess group in
  // BudgetingEndpoints.cs and handled by CopyBudgetAllocationsCommandHandler. The route is the
  // one place a literal segment sits beside an {allocationId:guid} route on the same path — the :guid
  // constraint is what keeps "copy" from binding as an item id — so the exact string is worth
  // pinning here.
  const TARGET = "5f2b1e1c-0000-4000-8000-0000000000a1";
  const SOURCE = "5f2b1e1c-0000-4000-8000-0000000000a2";

  it("posts to the target period's copy route with the source in the body", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, {
        copied: 11,
        skippedAlreadyPlanned: 0,
        skippedRetiredCode: 0,
        sourceLineCount: 11,
      }),
    );

    await copyBudgetAllocations(TARGET, { sourcePeriodId: SOURCE });

    expect(callPath()).toBe(`/api/budgeting/periods/${TARGET}/allocations/copy`);
    expect(callInit().method).toBe("POST");
    expect(JSON.parse(String(callInit().body))).toEqual({ sourcePeriodId: SOURCE });
  });

  it("puts the TARGET in the route and the SOURCE in the body, never the other way round", async () => {
    // The reason the source is an object rather than a bare second string: two same-typed guids
    // swap silently, the server answers 200 either way, and the planner's new Draft quietly
    // overwrites nothing while last quarter's period gains lines. Named at the call site, the
    // mistake is unwriteable.
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, {
        copied: 0,
        skippedAlreadyPlanned: 0,
        skippedRetiredCode: 0,
        sourceLineCount: 0,
      }),
    );

    await copyBudgetAllocations(TARGET, { sourcePeriodId: SOURCE });

    expect(callPath()).toContain(TARGET);
    expect(callPath()).not.toContain(SOURCE);
    expect(String(callInit().body)).toContain(SOURCE);
    expect(String(callInit().body)).not.toContain(TARGET);
  });

  it("returns the four counts, which always sum to sourceLineCount", async () => {
    const body = {
      copied: 8,
      skippedAlreadyPlanned: 2,
      skippedRetiredCode: 1,
      sourceLineCount: 11,
    };
    fetchMock.mockResolvedValueOnce(jsonResponse(200, body));

    const result = await copyBudgetAllocations(TARGET, { sourcePeriodId: SOURCE });

    expect(result).toEqual(body);
    expect(result.copied + result.skippedAlreadyPlanned + result.skippedRetiredCode).toBe(
      result.sourceLineCount,
    );
  });

  it("treats an empty source period as a 200 with zeroes, not an error", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, {
        copied: 0,
        skippedAlreadyPlanned: 0,
        skippedRetiredCode: 0,
        sourceLineCount: 0,
      }),
    );

    await expect(copyBudgetAllocations(TARGET, { sourcePeriodId: SOURCE })).resolves.toEqual({
      copied: 0,
      skippedAlreadyPlanned: 0,
      skippedRetiredCode: 0,
      sourceLineCount: 0,
    });
  });

  it.each<[number, string, string]>([
    [400, "Budgeting.Allocation.CopySourceRequired", "Choose a period to copy from."],
    [
      400,
      "Budgeting.Allocation.CopySourceIsTarget",
      "A period cannot be copied onto itself. Choose a different source period.",
    ],
    [404, "Budgeting.Allocation.CopySourceNotFound", "The period to copy from was not found."],
    [
      409,
      "Budgeting.Allocation.PeriodNotEditable",
      "The plan can only change while the period is Draft or Open.",
    ],
  ])("surfaces the %d %s message verbatim", async (status, code, message) => {
    // Every 400/409 in this app is shown as the server wrote it: the wording is what names the
    // rule, and CopySourceNotFound exists precisely so the console can say WHICH of the two
    // period ids was wrong.
    fetchMock.mockResolvedValueOnce(jsonResponse(status, { code, message }));

    await expect(
      copyBudgetAllocations(TARGET, { sourcePeriodId: SOURCE }),
    ).rejects.toMatchObject({ code, message, status });
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

  it("lists the period's codes with a GET on the period route", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));

    await expect(listBudgetCodes(PERIOD_ID)).resolves.toEqual([]);
    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/codes`);
    expect(callInit().method).toBeUndefined();
  });

  it("lists owner candidates from the TENANT-WIDE codes/owners route — people, not codes", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, [{ userId: "u1", email: "owner@northernlink.ca", role: "Owner" }]),
    );

    await expect(listBudgetOwnerCandidates()).resolves.toEqual([
      { userId: "u1", email: "owner@northernlink.ca", role: "Owner" },
    ]);
    expect(callPath()).toBe("/api/budgeting/codes/owners");
  });

  it("posts a new code to the period's route and returns only the new id", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: "c9" }));

    await expect(createBudgetCode(PERIOD_ID, input)).resolves.toBe("c9");
    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/codes`);
    expect(callInit().method).toBe("POST");
    // The body shape did not change when codes moved under a period: the period is the route's,
    // never the body's.
    expect(JSON.parse(String(callInit().body))).toEqual(input);
    expect(JSON.parse(String(callInit().body))).not.toHaveProperty("periodId");
  });

  it("sends cleared optional fields as explicit nulls, not omissions", async () => {
    // "Cleared" and "unchanged" must not look the same on the wire — the backend reads an
    // absent key as no change, so omitting a nulled field would make clearing it impossible.
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: "c9" }));

    await createBudgetCode(PERIOD_ID, input);

    const body = JSON.parse(String(callInit().body)) as Record<string, unknown>;
    expect(body).toHaveProperty("costCentre", null);
    expect(body).toHaveProperty("glAccountCode", null);
    expect(body).toHaveProperty("budgetOwnerUserId", null);
  });

  it("PUTs an update to the code's route under its period, without a code field", async () => {
    // There is no rename endpoint: items and actuals reference a code by string, so a
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
    await expect(updateBudgetCode(PERIOD_ID, CODE_ID, update)).resolves.toBeUndefined();

    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/codes/${CODE_ID}`);
    expect(callInit().method).toBe("PUT");
    expect(JSON.parse(String(callInit().body))).toEqual(update);
    expect(JSON.parse(String(callInit().body))).not.toHaveProperty("code");
  });

  it("posts the starter set to the period's route and returns how many it created", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { created: 12 }));

    await expect(seedStarterBudgetCodes(PERIOD_ID)).resolves.toEqual({ created: 12 });
    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD_ID}/codes/starter-set`);
    expect(callInit().method).toBe("POST");
  });

  it("reports the starter set as idempotent on a second call", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { created: 0 }));

    await expect(seedStarterBudgetCodes(PERIOD_ID)).resolves.toEqual({ created: 0 });
  });

  it("never builds a route from the old tenant-wide codes* shape", async () => {
    // Those routes were removed server-side; only codes/owners stays tenant-wide.
    fetchMock.mockImplementation(async () => noContent());

    await listBudgetCodes(PERIOD_ID).catch(() => {});
    await createBudgetCode(PERIOD_ID, input).catch(() => {});
    await updateBudgetCode(PERIOD_ID, CODE_ID, { ...input }).catch(() => {});
    await setBudgetCodeActive(PERIOD_ID, CODE_ID, true).catch(() => {});
    await deleteBudgetCode(PERIOD_ID, CODE_ID).catch(() => {});
    await seedStarterBudgetCodes(PERIOD_ID).catch(() => {});
    await copyBudgetCodes(PERIOD_ID, { sourcePeriodId: "src" }).catch(() => {});

    const paths = fetchMock.mock.calls.map((c) => String(c[0]));
    expect(paths).toHaveLength(7);
    for (const path of paths) {
      expect(path.startsWith(`/api/budgeting/periods/${PERIOD_ID}/codes`)).toBe(true);
    }
  });

  it.each<[string, () => Promise<unknown>]>([
    ["create", () => createBudgetCode(PERIOD_ID, input)],
    ["retire", () => setBudgetCodeActive(PERIOD_ID, CODE_ID, false)],
    ["delete", () => deleteBudgetCode(PERIOD_ID, CODE_ID)],
    ["starter set", () => seedStarterBudgetCodes(PERIOD_ID)],
  ])("surfaces %s's 409 PeriodNotEditable verbatim", async (_name, call) => {
    // BudgetCodeErrors.PeriodNotEditable — every code write checks the period's lifecycle.
    const message = "A period's budget codes can only change while it is Draft or Open.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Budgeting.Code.PeriodNotEditable", message }),
    );

    await expect(call()).rejects.toMatchObject({
      code: "Budgeting.Code.PeriodNotEditable",
      message,
      status: 409,
    });
  });

  it("surfaces the per-period duplicate and parent messages verbatim", async () => {
    // BudgetCodeErrors.DuplicateCode / ParentNotFound — the wording now names the period rule.
    const duplicate =
      "Another budget code in this period already uses that code. Codes are unique within a period.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Budgeting.Code.DuplicateCode", message: duplicate }),
    );
    await expect(createBudgetCode(PERIOD_ID, input)).rejects.toMatchObject({ message: duplicate });

    const parent =
      "The parent budget code was not found in this period. A code can only roll up into a code of the same period.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(404, { code: "Budgeting.Code.ParentNotFound", message: parent }),
    );
    await expect(createBudgetCode(PERIOD_ID, input)).rejects.toMatchObject({ message: parent });
  });

  it("surfaces a 404 for a code from another period verbatim", async () => {
    // BudgetCodeErrors.NotFound: a code id from another period is "not found in this period".
    const message = "The budget code was not found in this period.";
    fetchMock.mockResolvedValueOnce(
      jsonResponse(404, { code: "Budgeting.Code.NotFound", message }),
    );

    await expect(deleteBudgetCode(PERIOD_ID, CODE_ID)).rejects.toMatchObject({
      code: "Budgeting.Code.NotFound",
      message,
      status: 404,
    });
  });
});

describe("copyBudgetCodes", () => {
  // POST periods/{id}/codes/copy (BudgetingEndpoints.cs → CopyBudgetCodesCommandHandler). Same
  // object-body convention as copyBudgetAllocations, for the same reason: two same-typed guids.
  const TARGET = "5f2b1e1c-0000-4000-8000-0000000000c1";
  const SOURCE = "5f2b1e1c-0000-4000-8000-0000000000c2";

  it("posts to the target period's codes/copy route with the source in the body", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, { copied: 14, skippedExisting: 0, skippedRetired: 0, sourceCodeCount: 14 }),
    );

    await copyBudgetCodes(TARGET, { sourcePeriodId: SOURCE });

    expect(callPath()).toBe(`/api/budgeting/periods/${TARGET}/codes/copy`);
    expect(callInit().method).toBe("POST");
    expect(JSON.parse(String(callInit().body))).toEqual({ sourcePeriodId: SOURCE });
  });

  it("puts the TARGET in the route and the SOURCE in the body, never the other way round", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(200, { copied: 0, skippedExisting: 0, skippedRetired: 0, sourceCodeCount: 0 }),
    );

    await copyBudgetCodes(TARGET, { sourcePeriodId: SOURCE });

    expect(callPath()).toContain(TARGET);
    expect(callPath()).not.toContain(SOURCE);
    expect(String(callInit().body)).toContain(SOURCE);
    expect(String(callInit().body)).not.toContain(TARGET);
  });

  it("returns the four counts, which always sum to sourceCodeCount", async () => {
    const body = { copied: 9, skippedExisting: 3, skippedRetired: 2, sourceCodeCount: 14 };
    fetchMock.mockResolvedValueOnce(jsonResponse(200, body));

    const result = await copyBudgetCodes(TARGET, { sourcePeriodId: SOURCE });

    expect(result).toEqual(body);
    expect(result.copied + result.skippedExisting + result.skippedRetired).toBe(
      result.sourceCodeCount,
    );
  });

  it.each<[number, string, string]>([
    [400, "Budgeting.Code.CopySourceRequired", "Choose a period to copy budget codes from."],
    [
      400,
      "Budgeting.Code.CopySourceIsTarget",
      "A period's budget codes cannot be copied onto itself. Choose a different source period.",
    ],
    [404, "Budgeting.Period.NotFound", "The budget period was not found."],
    [
      409,
      "Budgeting.Code.PeriodNotEditable",
      "A period's budget codes can only change while it is Draft or Open.",
    ],
    [404, "Budgeting.Code.CopySourceNotFound", "The period to copy budget codes from was not found."],
  ])("surfaces the %d %s message verbatim", async (status, code, message) => {
    // BudgetCodeErrors / BudgetPeriodErrors, in the handler's guard order. Two 404s exist so the
    // console can say WHICH period id was wrong: Period.NotFound is the target, CopySourceNotFound
    // the source.
    fetchMock.mockResolvedValueOnce(jsonResponse(status, { code, message }));

    await expect(copyBudgetCodes(TARGET, { sourcePeriodId: SOURCE })).rejects.toMatchObject({
      code,
      message,
      status,
    });
  });
});

describe("budget item requests", () => {
  // POST / PUT / DELETE periods/{id}/allocations[/{allocationId}] in the BudgetAccess group
  // (BudgetingEndpoints.cs). The old upsert-by-code PUT periods/{id}/allocations/{codeId} is gone:
  // an item is addressed by its OWN id, so a code id in that route segment would be a 404.
  const PERIOD = "5f2b1e1c-0000-4000-8000-0000000000a1";
  const ITEM = "5f2b1e1c-0000-4000-8000-0000000000b7";

  const input: BudgetItemInput = {
    budgetCodeId: CODE_ID,
    title: "Winter tires, unit NL-04",
    amountCad: null,
    quantity: 4,
    unitCostCad: 612.5,
    unit: "tire",
    justification: "Kal Tire quote in hand.",
    spendType: "Capital",
    recurrence: "OneTime",
    vendor: "Kal Tire",
    tags: ["winter", "safety"],
    priority: "MustHave",
    assumptions: null,
    consequenceIfUnfunded: "NL-04 runs summer tires into November.",
  };

  it("lists a period's items with a GET", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));

    await expect(listBudgetAllocations(PERIOD)).resolves.toEqual([]);
    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD}/allocations`);
    expect(callInit().method).toBeUndefined();
  });

  it("POSTs a new item to the period's allocations route and returns the 201's id", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: ITEM }));

    await expect(createBudgetItem(PERIOD, input)).resolves.toBe(ITEM);
    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD}/allocations`);
    expect(callInit().method).toBe("POST");
  });

  it("sends the whole BudgetItemRequest — every key, enums as their PascalCase strings, explicit nulls", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: ITEM }));

    await createBudgetItem(PERIOD, input);

    expect(JSON.parse(String(callInit().body))).toEqual({
      budgetCodeId: CODE_ID,
      title: "Winter tires, unit NL-04",
      amountCad: null,
      quantity: 4,
      unitCostCad: 612.5,
      unit: "tire",
      justification: "Kal Tire quote in hand.",
      spendType: "Capital",
      recurrence: "OneTime",
      vendor: "Kal Tire",
      tags: ["winter", "safety"],
      priority: "MustHave",
      assumptions: null,
      consequenceIfUnfunded: "NL-04 runs summer tires into November.",
    });
  });

  it("PUTs an update to the ITEM's own route, 204 → undefined", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await expect(updateBudgetItem(PERIOD, ITEM, { ...input, budgetCodeId: "moved" })).resolves.toBeUndefined();
    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD}/allocations/${ITEM}`);
    expect(callInit().method).toBe("PUT");
    // The code travels in the body, which is how an item moves to another code.
    expect(JSON.parse(String(callInit().body))).toMatchObject({ budgetCodeId: "moved" });
  });

  it("DELETEs by item id, 204 → undefined", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await expect(removeBudgetItem(PERIOD, ITEM)).resolves.toBeUndefined();
    expect(callPath()).toBe(`/api/budgeting/periods/${PERIOD}/allocations/${ITEM}`);
    expect(callInit().method).toBe("DELETE");
    expect(callPath()).not.toContain(CODE_ID);
  });

  it.each<[number, string, string]>([
    [400, "Budgeting.Allocation.TitleRequired", "Give the budget item a title — what is this money for?"],
    [
      400,
      "Budgeting.Allocation.QuantityWithoutUnitCost",
      "Quantity and unit cost go together: enter both, or leave both blank and enter a lump-sum amount.",
    ],
    [404, "Budgeting.Allocation.NotFound", "That budget item was not found in this period."],
    [
      409,
      "Budgeting.Allocation.CodeRetired",
      "That budget code is retired and cannot take new allocations. Restore it or pick another code.",
    ],
    [
      409,
      "Budgeting.Allocation.PeriodNotEditable",
      "The plan can only change while the period is Draft or Open.",
    ],
  ])("surfaces the %d %s message verbatim on update", async (status, code, message) => {
    fetchMock.mockResolvedValueOnce(jsonResponse(status, { code, message }));

    await expect(updateBudgetItem(PERIOD, ITEM, input)).rejects.toMatchObject({ code, message, status });
  });
});

// ---------------------------------------------------------------------------
// Vendors — TENANT-WIDE routes (BudgetingEndpoints.cs: budgeting.Map*("vendors...")), directly
// under /api/budgeting and never under a period. Messages below are verbatim from VendorErrors.
// ---------------------------------------------------------------------------

const VENDOR_ID = "5f2b1e1c-0000-4000-8000-0000000000c1";

const VENDOR_INPUT: VendorInput = {
  name: "Kal Tire Thompson",
  contactName: null,
  email: "orders@kaltire.example",
  phone: null,
  address: null,
  notes: null,
  gstRegistrationNumber: "123456789 RT0001",
  qboDisplayName: null,
  defaultBudgetCode: "FLEET-TIRES",
};

/** VendorRequest's nine keys — what both POST and PUT must always carry. */
const VENDOR_REQUEST_KEYS = [
  "address",
  "contactName",
  "defaultBudgetCode",
  "email",
  "gstRegistrationNumber",
  "name",
  "notes",
  "phone",
  "qboDisplayName",
];

describe("listVendors", () => {
  it("GETs the tenant-wide register, active only by default", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));

    await listVendors();

    expect(callPath()).toBe("/api/budgeting/vendors?includeInactive=false");
    expect(callInit().method ?? "GET").toBe("GET");
  });

  it("asks for retired vendors too when told to — and is never built under a period", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));

    await listVendors(true);

    expect(callPath()).toBe("/api/budgeting/vendors?includeInactive=true");
    expect(callPath()).not.toContain("/periods/");
  });
});

describe("getVendor", () => {
  it("GETs the vendor's own route", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { id: VENDOR_ID }));

    await getVendor(VENDOR_ID);

    expect(callPath()).toBe(`/api/budgeting/vendors/${VENDOR_ID}`);
  });

  it("surfaces a 404 NotFound verbatim", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(404, { code: "Budgeting.Vendor.NotFound", message: "The vendor was not found." }),
    );

    await expect(getVendor(VENDOR_ID)).rejects.toMatchObject({
      code: "Budgeting.Vendor.NotFound",
      status: 404,
      message: "The vendor was not found.",
    });
  });
});

describe("createVendor", () => {
  it("POSTs every VendorRequest key, nulls explicit, and returns the 201's id", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(201, { id: VENDOR_ID }));

    const id = await createVendor(VENDOR_INPUT);

    expect(id).toBe(VENDOR_ID);
    expect(callPath()).toBe("/api/budgeting/vendors");
    expect(callInit().method).toBe("POST");
    const body = JSON.parse(String(callInit().body));
    expect(Object.keys(body).sort()).toEqual(VENDOR_REQUEST_KEYS);
    expect(body).toEqual(VENDOR_INPUT);
    expect(body.contactName).toBeNull();
  });

  it("surfaces the active-vendor DuplicateName 409 verbatim", async () => {
    const message =
      'A vendor named "Kal Tire Thompson" already exists. Vendor names are unique, ignoring case.';
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Budgeting.Vendor.DuplicateName", message }),
    );

    await expect(createVendor(VENDOR_INPUT)).rejects.toMatchObject({
      code: "Budgeting.Vendor.DuplicateName",
      status: 409,
      message,
    });
  });

  it("surfaces the retired-vendor DuplicateName 409 verbatim — it says to reactivate", async () => {
    const message =
      'A retired vendor named "Kal Tire Thompson" already exists. Reactivate it instead of adding it again.';
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Budgeting.Vendor.DuplicateName", message }),
    );

    const err = await createVendor(VENDOR_INPUT).catch((e: unknown) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).message).toBe(message);
  });

  it.each([
    ["Budgeting.Vendor.NameRequired", "A vendor needs a name."],
    ["Budgeting.Vendor.NameTooLong", "The vendor name must be 120 characters or fewer."],
    ["Budgeting.Vendor.EmailInvalid", "The email must look like an address (name@example.com)."],
    [
      "Budgeting.Vendor.DefaultBudgetCodeInvalidFormat",
      "The default budget code may use letters, digits and hyphens only, and must start and end with a letter or digit (for example FLEET-MAINT).",
    ],
    [
      "Budgeting.Vendor.GstRegistrationNumberTooLong",
      "The GST registration number must be 32 characters or fewer.",
    ],
  ])("surfaces a 400 %s verbatim", async (code, message) => {
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { code, message }));

    await expect(createVendor(VENDOR_INPUT)).rejects.toMatchObject({ code, status: 400, message });
  });
});

describe("updateVendor", () => {
  it("PUTs to the vendor's route with EVERY key — PUT is a full replace", async () => {
    fetchMock.mockResolvedValueOnce(noContent());
    const cleared: VendorInput = { ...VENDOR_INPUT, email: null, gstRegistrationNumber: null };

    await updateVendor(VENDOR_ID, cleared);

    expect(callPath()).toBe(`/api/budgeting/vendors/${VENDOR_ID}`);
    expect(callInit().method).toBe("PUT");
    const body = JSON.parse(String(callInit().body));
    // An omitted key would clear the field server-side; a cleared one must be an explicit null.
    expect(Object.keys(body).sort()).toEqual(VENDOR_REQUEST_KEYS);
    expect(body.email).toBeNull();
    expect(body.gstRegistrationNumber).toBeNull();
    expect(body.name).toBe("Kal Tire Thompson");
  });

  it("surfaces a rename onto another vendor's name (409) verbatim", async () => {
    const message =
      'A vendor named "Esso Thompson" already exists. Vendor names are unique, ignoring case.';
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Budgeting.Vendor.DuplicateName", message }),
    );

    await expect(updateVendor(VENDOR_ID, VENDOR_INPUT)).rejects.toMatchObject({
      status: 409,
      message,
    });
  });
});

describe("setVendorActive", () => {
  it("deactivates on false — the retire route", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setVendorActive(VENDOR_ID, false);

    expect(callPath()).toBe(`/api/budgeting/vendors/${VENDOR_ID}/deactivate`);
    expect(callInit().method).toBe("POST");
  });

  it("activates on true — the restore route", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await setVendorActive(VENDOR_ID, true);

    expect(callPath()).toBe(`/api/budgeting/vendors/${VENDOR_ID}/activate`);
    expect(callInit().method).toBe("POST");
  });
});

describe("deleteVendor", () => {
  it("issues a DELETE to the vendor's own route", async () => {
    fetchMock.mockResolvedValueOnce(noContent());

    await deleteVendor(VENDOR_ID);

    expect(callPath()).toBe(`/api/budgeting/vendors/${VENDOR_ID}`);
    expect(callInit().method).toBe("DELETE");
  });

  it("surfaces the InUse 409 verbatim — its words name retiring as the alternative", async () => {
    // Verbatim from VendorErrors.InUse.
    const message =
      "This vendor is referenced by budget items and cannot be deleted. Retire it instead — a retired vendor stays listed so existing items keep resolving.";
    fetchMock.mockResolvedValueOnce(jsonResponse(409, { code: "Budgeting.Vendor.InUse", message }));

    const err = await deleteVendor(VENDOR_ID).catch((e: unknown) => e);

    expect(err).toBeInstanceOf(ApiError);
    expect(err).toMatchObject({ code: "Budgeting.Vendor.InUse", status: 409, message });
  });
});
