import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  bookeoErrorKind,
  bookeoErrorMessage,
  commitBookeoImport,
  deleteBookeoProductMapping,
  deleteBookeoUnitMapping,
  listBookeoImportBatches,
  listBookeoProductMappings,
  listBookeoUnitMappings,
  previewBookeoImport,
  saveBookeoProductMappings,
  saveBookeoUnitMappings,
} from "./bookeoImport";
import { ApiError } from "./transport";

// What actually goes on the wire for the Bookeo import. The routes and bodies are
// the contract in the bookeo-import spec (Trips module, /api/trips/imports/bookeo);
// a wrong route is a 404 the UI would report as "the import no longer exists".

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("../auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

const callPath = (n = 0) => String(fetchMock.mock.calls[n]?.[0]);
const callInit = (n = 0) => (fetchMock.mock.calls[n]?.[1] ?? {}) as RequestInit;
const headersOf = (n = 0) => (callInit(n).headers ?? {}) as Record<string, string>;

beforeEach(() => {
  vi.stubGlobal("fetch", fetchMock);
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("previewBookeoImport", () => {
  it("POSTs the file as multipart field `file`, without a JSON content type", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { batchId: "b1" }));
    const file = new File(["xls-bytes"], "bookings.xls", { type: "application/vnd.ms-excel" });

    await previewBookeoImport(file);

    expect(callPath()).toBe("/api/trips/imports/bookeo/preview");
    expect(callInit().method).toBe("POST");
    const body = callInit().body;
    expect(body).toBeInstanceOf(FormData);
    expect((body as FormData).get("file")).toBe(file);
    // The browser must set multipart/form-data with its boundary itself.
    expect(headersOf()["Content-Type"]).toBeUndefined();
    expect(headersOf().Authorization).toBe("Bearer access-1");
  });

  it("surfaces HeaderNotRecognized with the server's missing-column list", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(400, {
        code: "Trips.BookeoImport.HeaderNotRecognized",
        message: "This does not look like a Bookeo booking report. Missing column(s): Booking number, Product code.",
      }),
    );
    const err = await previewBookeoImport(new File(["x"], "x.xlsx")).catch((e) => e);
    expect(bookeoErrorKind(err)).toBe("HeaderNotRecognized");
    // The column names live only in the server's message, so it must be shown verbatim.
    expect(bookeoErrorMessage(err)).toBe(
      "Couldn't read the header row. This does not look like a Bookeo booking report. Missing column(s): Booking number, Product code.",
    );
  });

  it("surfaces the server's row-count message for an over-2,000-row FileTooLarge", async () => {
    const message =
      "The report has 2412 booking rows; at most 2000 can be imported at once. Export a shorter date range from Bookeo.";
    fetchMock.mockResolvedValueOnce(jsonResponse(400, { code: "Trips.BookeoImport.FileTooLarge", message }));
    const err = await previewBookeoImport(new File(["x"], "x.xlsx")).catch((e) => e);
    expect(bookeoErrorKind(err)).toBe("FileTooLarge");
    expect(bookeoErrorMessage(err)).toBe(`That report is too big to import. ${message}`);
    expect(bookeoErrorMessage(err)).not.toMatch(/5 MB/);
  });
});

describe("commitBookeoImport", () => {
  it("POSTs { planHash } as JSON to the batch's commit route", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { batchId: "b1", createdTripNumbers: [] }));

    await commitBookeoImport("b1", "hash-abc");

    expect(callPath()).toBe("/api/trips/imports/bookeo/b1/commit");
    expect(callInit().method).toBe("POST");
    expect(JSON.parse(String(callInit().body))).toEqual({ planHash: "hash-abc" });
    expect(headersOf()["Content-Type"]).toBe("application/json");
  });

  it("a 409 PreviewStale is recognised as stale, not as a generic failure", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Trips.BookeoImport.PreviewStale", message: "Plan hash mismatch." }),
    );
    const err = await commitBookeoImport("b1", "old").catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(409);
    expect(bookeoErrorKind(err)).toBe("PreviewStale");
    expect(bookeoErrorMessage(err)).toMatch(/changed since this preview/);
  });

  it("a 409 AlreadyCommitted is told apart from PreviewStale", async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(409, { code: "Trips.BookeoImport.AlreadyCommitted", message: "Already committed." }),
    );
    const err = await commitBookeoImport("b1", "h").catch((e) => e);
    expect(bookeoErrorKind(err)).toBe("AlreadyCommitted");
    expect(bookeoErrorMessage(err)).toMatch(/already been applied/);
  });

  it("a 404 is NotFound", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(404, { code: "Trips.BookeoImport.BatchNotFound", message: "x" }));
    const err = await commitBookeoImport("gone", "h").catch((e) => e);
    expect(bookeoErrorKind(err)).toBe("NotFound");
  });
});

describe("mapping and history routes", () => {
  it("product mappings: GET, PUT { mappings }, DELETE by id", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, []))
      .mockResolvedValueOnce(jsonResponse(200, []))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));
    const mapping = {
      productCode: "P1",
      productName: "Shuttle to Thompson",
      destination: null,
      routeId: "r1",
      direction: "Outbound" as const,
      residentStopRole: "Pickup" as const,
    };

    await listBookeoProductMappings();
    await saveBookeoProductMappings([mapping]);
    await deleteBookeoProductMapping("m1");

    expect(callPath(0)).toBe("/api/trips/imports/bookeo/product-mappings");
    expect(callPath(1)).toBe("/api/trips/imports/bookeo/product-mappings");
    expect(callInit(1).method).toBe("PUT");
    expect(JSON.parse(String(callInit(1).body))).toEqual({ mappings: [mapping] });
    expect(callPath(2)).toBe("/api/trips/imports/bookeo/product-mappings/m1");
    expect(callInit(2).method).toBe("DELETE");
  });

  it("unit mappings: GET, PUT { mappings }, DELETE by id", async () => {
    fetchMock
      .mockResolvedValueOnce(jsonResponse(200, []))
      .mockResolvedValueOnce(jsonResponse(200, []))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    await listBookeoUnitMappings();
    await saveBookeoUnitMappings([{ unitText: "Ford Transit 150", vehicleId: "v1" }]);
    await deleteBookeoUnitMapping("u1");

    expect(callPath(0)).toBe("/api/trips/imports/bookeo/unit-mappings");
    expect(callInit(1).method).toBe("PUT");
    expect(JSON.parse(String(callInit(1).body))).toEqual({
      mappings: [{ unitText: "Ford Transit 150", vehicleId: "v1" }],
    });
    expect(callPath(2)).toBe("/api/trips/imports/bookeo/unit-mappings/u1");
    expect(callInit(2).method).toBe("DELETE");
  });

  it("batches: GET with take", async () => {
    fetchMock.mockResolvedValueOnce(jsonResponse(200, []));
    await listBookeoImportBatches();
    expect(callPath()).toBe("/api/trips/imports/bookeo/batches?take=20");
  });
});

describe("bookeoErrorMessage", () => {
  it.each([
    ["Trips.BookeoImport.FileRequired", 400, /Choose a Bookeo booking report/],
    ["Trips.BookeoImport.FileTooLarge", 400, /too big to import\. server text/],
    ["Trips.BookeoImport.UnsupportedFile", 400, /\.xls or \.xlsx/],
  ])("maps %s", (code, status, pattern) => {
    expect(bookeoErrorMessage(new ApiError(code, "server text", status))).toMatch(pattern);
  });

  it("a bare 413 reads as FileTooLarge, with the fixed 5 MB text (not the HTTP status text)", () => {
    const bare = new ApiError("Http.413", "Payload Too Large", 413);
    expect(bookeoErrorKind(bare)).toBe("FileTooLarge");
    expect(bookeoErrorMessage(bare)).toBe(
      "That file is larger than 5 MB. Export a shorter date range from Bookeo and try again.",
    );
  });

  it("a coded error with no message falls back to the fixed text", () => {
    expect(bookeoErrorMessage(new ApiError("Trips.BookeoImport.FileTooLarge", "", 400))).toMatch(/larger than 5 MB/);
    expect(bookeoErrorMessage(new ApiError("Trips.BookeoImport.HeaderNotRecognized", "  ", 400))).toMatch(
      /header row is missing columns/,
    );
  });

  it("falls back to the server's message for an unknown code", () => {
    expect(bookeoErrorMessage(new ApiError("Trips.Other", "Something specific.", 400))).toBe("Something specific.");
    expect(bookeoErrorKind(new ApiError("Trips.Other", "x", 400))).toBeNull();
  });

  it("never mentions tax", () => {
    for (const code of ["FileRequired", "FileTooLarge", "UnsupportedFile", "HeaderNotRecognized", "PreviewStale", "AlreadyCommitted"]) {
      expect(bookeoErrorMessage(new ApiError(`Trips.BookeoImport.${code}`, "", 400))).not.toMatch(/GST|tax/i);
    }
  });
});
