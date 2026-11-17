import { afterEach, describe, expect, it, vi } from "vitest";
import { resolveInspectionDefect } from "./maintenance";

// POST /api/fleet/inspections/{id}/defects/resolve answers with every defect it
// cleared — the addressed one first, then its open duplicates, oldest first. An
// API from before the cascade answers 204 with no body; that must still read as
// "the addressed defect was cleared", or the optimistic drop would do nothing.

const { request } = vi.hoisted(() => ({ request: vi.fn<(path: string, init?: RequestInit) => Promise<unknown>>() }));

vi.mock("./transport", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./transport")>()),
  request,
}));

afterEach(() => vi.clearAllMocks());

const body = { item: "Brakes", reason: "PreviouslyRepaired" as const, note: null, resolvedBy: "Dispatch" };

describe("resolveInspectionDefect", () => {
  it("posts the item in the body and returns the cleared list as the server ordered it", async () => {
    const resolved = [
      { inspectionId: "i-1", item: "Brakes", tripNumber: "NL-2026-0050", severity: "Minor" },
      { inspectionId: "i-3", item: "brakes", tripNumber: "NL-2026-0048", severity: "OutOfService" },
      { inspectionId: "i-2", item: "BRAKES ", tripNumber: null, severity: "Major" },
    ];
    request.mockResolvedValueOnce({ resolved });

    await expect(resolveInspectionDefect("i-1", body)).resolves.toEqual(resolved);
    expect(request).toHaveBeenCalledWith("/api/fleet/inspections/i-1/defects/resolve", {
      method: "POST",
      body: JSON.stringify(body),
    });
  });

  it("reads an empty body (an older API) as just the addressed defect", async () => {
    // transport.request returns undefined for a 204.
    request.mockResolvedValueOnce(undefined);
    await expect(resolveInspectionDefect("i-1", body)).resolves.toEqual([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: null },
    ]);
  });

  it("reads a body with no list the same way", async () => {
    request.mockResolvedValueOnce({});
    await expect(resolveInspectionDefect("i-1", body)).resolves.toEqual([
      { inspectionId: "i-1", item: "Brakes", tripNumber: null, severity: null },
    ]);
    request.mockResolvedValueOnce({ resolved: [] });
    await expect(resolveInspectionDefect("i-1", body)).resolves.toHaveLength(1);
  });

  it("lets an error through unchanged", async () => {
    const err = new Error("409");
    request.mockRejectedValueOnce(err);
    await expect(resolveInspectionDefect("i-1", body)).rejects.toBe(err);
  });
});
