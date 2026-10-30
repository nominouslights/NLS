import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import BookeoImportModal from "./BookeoImportModal";
import { group, preview, row, summary } from "@/lib/bookeoImport.fixtures";

// The unmapped-product loop: the dispatcher picks the file once, maps the
// product in the panel, and saving PUTs the mapping and re-runs the preview with
// the SAME File — they must never have to pick the spreadsheet again.

const { getAccessToken, getValidAccessToken, refreshAccessToken } = vi.hoisted(() => ({
  getAccessToken: vi.fn<() => string | null>(),
  getValidAccessToken: vi.fn<() => Promise<string | null>>(),
  refreshAccessToken: vi.fn<() => Promise<string>>(),
}));

vi.mock("@/lib/auth", () => ({ getAccessToken, getValidAccessToken, refreshAccessToken }));

const fetchMock = vi.fn<typeof fetch>();

function json(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
}

const ROUTE = {
  id: "route-1",
  name: "Thompson Run",
  stops: [],
  origin: "Lynn Lake",
  destination: "Thompson",
  distanceKm: 320,
  estimatedDurationMinutes: 270,
  requiredLicenceClass: null,
  active: true,
  createdAtUtc: "2026-01-01T00:00:00Z",
  updatedAtUtc: "2026-01-01T00:00:00Z",
};

const PRODUCT = { productCode: "41578HWK6CT1A0E4970BA2", productName: "Shuttle to Thompson", destination: null, rowCount: 1 };

const UNMAPPED = preview({
  batchId: "batch-1",
  planHash: "hash-1",
  summary: summary({ rows: 1, new: 1, blockedGroups: 0 }),
  unmappedProducts: [PRODUCT],
  rows: [
    row({
      bookingNumber: "0000000000000003",
      productName: "Shuttle to Thompson",
      groupKey: null,
      issues: [{ code: "ProductNotMapped", severity: "Block", message: "Product is not mapped to a route." }],
    }),
  ],
});

const MAPPED = preview({
  batchId: "batch-2",
  planHash: "hash-2",
  summary: summary({ rows: 1, new: 1, tripsToCreate: 1 }),
  groups: [group({ key: "g1", routeName: "Thompson Run", action: "Create", bookingNumbers: ["0000000000000003"] })],
  rows: [row({ bookingNumber: "0000000000000003", productName: "Shuttle to Thompson", groupKey: "g1" })],
});

let previewCalls: FormData[] = [];
let putBodies: unknown[] = [];

beforeEach(() => {
  previewCalls = [];
  putBodies = [];
  getValidAccessToken.mockResolvedValue("access-1");
  getAccessToken.mockReturnValue("access-1");
  fetchMock.mockImplementation(async (input, init) => {
    const url = String(input);
    const method = init?.method ?? "GET";
    if (url === "/api/trips/routes") return json(200, [ROUTE]);
    if (url === "/api/fleet/vehicles") return json(200, []);
    if (url.endsWith("/preview") && method === "POST") {
      previewCalls.push(init!.body as FormData);
      return json(200, previewCalls.length === 1 ? UNMAPPED : MAPPED);
    }
    if (url.endsWith("/product-mappings") && method === "PUT") {
      putBodies.push(JSON.parse(String(init!.body)));
      return json(200, []);
    }
    return json(404, { code: "Test.Unexpected", message: `${method} ${url}` });
  });
  vi.stubGlobal("fetch", fetchMock);
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("BookeoImportModal — unmapped product → save → re-preview", () => {
  it("saves the mapping and re-previews the same file without a second pick", async () => {
    render(<BookeoImportModal onClose={() => undefined} />);

    const file = new File(["xls"], "bookeo_sample.xls", { type: "application/vnd.ms-excel" });
    fireEvent.change(screen.getByTestId("bookeo-file"), { target: { files: [file] } });

    // The panel appears, and Confirm is blocked while the product is unmapped.
    await screen.findByTestId("unmapped-product");
    expect(screen.getByText(/Map 1 product to a route first/)).toBeTruthy();
    expect(screen.getByText(/APPLY 0 GROUPS/).getAttribute("aria-disabled")).toBe("true");

    // The route picker is fed by the routes API; "to Thompson" pre-suggests Pickup.
    await screen.findByRole("option", { name: /Thompson Run/ });
    fireEvent.change(screen.getByLabelText("Route"), { target: { value: "route-1" } });
    fireEvent.change(screen.getByLabelText("Direction"), { target: { value: "Outbound" } });
    expect((screen.getByLabelText("Resident stop is the") as HTMLSelectElement).value).toBe("Pickup");

    fireEvent.click(screen.getByText("SAVE 1 MAPPING & RE-PREVIEW"));

    await waitFor(() => expect(previewCalls).toHaveLength(2));
    expect(putBodies).toEqual([
      {
        mappings: [
          {
            productCode: PRODUCT.productCode,
            productName: "Shuttle to Thompson",
            destination: null,
            routeId: "route-1",
            direction: "Outbound",
            residentStopRole: "Pickup",
          },
        ],
      },
    ]);
    // The very same File object went up both times.
    expect(previewCalls[0].get("file")).toBe(file);
    expect(previewCalls[1].get("file")).toBe(file);

    // The fresh plan: no unmapped panel, one group to create, Confirm live.
    await waitFor(() => expect(screen.queryByTestId("unmapped-product")).toBeNull());
    const apply = await screen.findByText("APPLY 1 GROUP");
    expect(apply.getAttribute("aria-disabled")).toBeNull();
    expect(screen.getByText(/Saved 1 product mapping/)).toBeTruthy();
    // Money copy says tax-inclusive gross; nothing anywhere mentions GST.
    expect(document.body.textContent).not.toMatch(/GST/);
  });
});
