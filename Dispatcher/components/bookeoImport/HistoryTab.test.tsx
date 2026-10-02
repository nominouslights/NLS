import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { HistoryTab } from "./HistoryTab";
import type { BookeoImportBatch } from "@/lib/api/bookeoImport";
import { summary } from "@/lib/bookeoImport.fixtures";

// /batches items carry `summary: BookeoImportSummary?` — a batch with no stored
// summary must render "—" in both count columns, never crash the History tab.

const { listBookeoImportBatches } = vi.hoisted(() => ({
  listBookeoImportBatches: vi.fn<() => Promise<BookeoImportBatch[]>>(),
}));

vi.mock("@/lib/api/bookeoImport", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/bookeoImport")>()),
  listBookeoImportBatches,
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const batch = (over: Partial<BookeoImportBatch>): BookeoImportBatch => ({
  batchId: "b1",
  fileName: "bookings.xls",
  uploadedBy: "dispatch@northernlink.ca",
  uploadedAtUtc: "2026-10-01T15:00:00Z",
  committedAtUtc: null,
  committedBy: null,
  summary: summary(),
  ...over,
});

describe("HistoryTab", () => {
  it("renders a batch whose summary is null with dashes", async () => {
    listBookeoImportBatches.mockResolvedValueOnce([
      batch({ batchId: "b-null", fileName: "no-summary.xls", summary: null }),
      batch({
        batchId: "b-ok",
        fileName: "with-summary.xls",
        summary: summary({ rows: 4, new: 3, changed: 1, tripsToCreate: 2, blockedGroups: 1 }),
      }),
    ]);

    render(<HistoryTab refreshKey={0} />);

    const nullRow = (await screen.findByText("no-summary.xls")).closest("tr")!;
    const cells = Array.from(nullRow.querySelectorAll("td")).map((td) => td.textContent);
    expect(cells.slice(3)).toEqual(["—", "—"]);

    const okRow = screen.getByText("with-summary.xls").closest("tr")!;
    expect(okRow.textContent).toContain("4 rows · 3 new · 1 changed");
    expect(okRow.textContent).toContain("2 create · 0 update · 0 cancel · 1 blocked");
  });
});
