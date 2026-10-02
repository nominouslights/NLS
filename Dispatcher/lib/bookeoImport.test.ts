import { describe, expect, it } from "vitest";
import {
  confirmState,
  groupActionChip,
  groupRows,
  humanizeCode,
  issueChip,
  rowActionChip,
  sortIssues,
  suggestResidentRole,
  summaryChanges,
} from "./bookeoImport";
import { group, preview, row, summary } from "./bookeoImport.fixtures";
import { statusMeta } from "./theme";

describe("confirmState", () => {
  it("is disabled with no preview", () => {
    expect(confirmState(null).disabled).toBe(true);
  });

  it("counts Create/Update/Cancel groups as applicable and names the blocked ones", () => {
    const s = confirmState(
      preview({
        groups: [
          group({ key: "a", action: "Create" }),
          group({ key: "b", action: "Update" }),
          group({ key: "c", action: "Cancel" }),
          group({ key: "d", action: "Unchanged" }),
          group({ key: "e", action: "Blocked" }),
        ],
      }),
    );
    expect(s.disabled).toBe(false);
    expect(s.applicable).toBe(3);
    expect(s.label).toBe("Apply 3 groups (1 blocked group will be skipped)");
  });

  it("omits the blocked clause when nothing is blocked, and singularises", () => {
    expect(confirmState(preview({ groups: [group({ action: "Create" })] })).label).toBe("Apply 1 group");
  });

  it("is disabled while any product is unmapped, even with applicable groups", () => {
    const s = confirmState(
      preview({
        groups: [group({ action: "Create" })],
        unmappedProducts: [{ productCode: "P", productName: "Shuttle to Thompson", destination: null, rowCount: 2 }],
      }),
    );
    expect(s.disabled).toBe(true);
    expect(s.reason).toMatch(/Map 1 product/);
  });

  it("is disabled with zero applicable groups", () => {
    const s = confirmState(preview({ groups: [group({ action: "Unchanged" }), group({ key: "x", action: "Blocked" })] }));
    expect(s.disabled).toBe(true);
    expect(s.label).toBe("Apply 0 groups (1 blocked group will be skipped)");
  });

  it("is disabled while busy", () => {
    expect(confirmState(preview({ groups: [group()] }), true).disabled).toBe(true);
  });
});

describe("issue → chip", () => {
  it("Block = vermillion, Warning = gold, Info = neutral — each with its own icon", () => {
    const block = issueChip({ code: "Trips.BookeoImport.OverVehicleCapacity", severity: "Block", message: "" });
    const warn = issueChip({ code: "PaymentDue", severity: "Warning", message: "" });
    const info = issueChip({ code: "BookingChanged", severity: "Info", message: "" });
    expect(statusMeta(block.kind).c).toBe("#D55E00");
    expect(statusMeta(warn.kind).c).toBe("#E1B000");
    expect(info.kind).toBe("off");
    expect(new Set([block.glyph, warn.glyph, info.glyph]).size).toBe(3);
    for (const c of [block, warn, info]) expect(c.label.length).toBeGreaterThan(0);
  });

  it("labels from the code's last segment, humanised", () => {
    expect(humanizeCode("Trips.BookeoImport.VehicleDoubleBooked")).toBe("Vehicle double booked");
    expect(issueChip({ code: "PastDeparture", severity: "Warning", message: "x" }).label).toBe("Past departure");
  });

  it("sorts blocks before warnings before info", () => {
    const sorted = sortIssues([
      { code: "A", severity: "Info", message: "" },
      { code: "B", severity: "Block", message: "" },
      { code: "C", severity: "Warning", message: "" },
    ]);
    expect(sorted.map((i) => i.severity)).toEqual(["Block", "Warning", "Info"]);
  });

  it("action chips: Blocked is vermillion, Create is teal, every one carries a label", () => {
    expect(statusMeta(groupActionChip("Blocked").kind).c).toBe("#D55E00");
    expect(statusMeta(groupActionChip("Create").kind).c).toBe("#009E73");
    for (const a of ["Create", "Update", "Unchanged", "Cancel", "Blocked"] as const) {
      expect(groupActionChip(a).label).toBeTruthy();
      expect(groupActionChip(a).glyph).toBeTruthy();
    }
    for (const a of ["New", "Changed", "Cancelled", "Unchanged", "Skipped"] as const) {
      expect(rowActionChip(a).label).toBeTruthy();
    }
  });
});

describe("groupRows", () => {
  it("nests rows under their group in bookingNumbers order and keeps the rest ungrouped", () => {
    const r1 = row({ bookingNumber: "1", groupKey: "g1" });
    const r2 = row({ bookingNumber: "2", groupKey: "g1" });
    const r3 = row({ bookingNumber: "3", groupKey: "g2" });
    const unmapped = row({ bookingNumber: "4", groupKey: null });
    const orphan = row({ bookingNumber: "5", groupKey: "missing" });
    const out = groupRows({
      groups: [group({ key: "g1", bookingNumbers: ["2", "1"] }), group({ key: "g2", bookingNumbers: [] })],
      rows: [r1, r2, r3, unmapped, orphan],
    });
    expect(out.byGroup.get("g1")!.map((r) => r.bookingNumber)).toEqual(["2", "1"]);
    // g2's list omitted booking 3 — the row's own groupKey still places it.
    expect(out.byGroup.get("g2")!.map((r) => r.bookingNumber)).toEqual(["3"]);
    expect(out.ungrouped.map((r) => r.bookingNumber)).toEqual(["4", "5"]);
  });

  it("never places one row twice", () => {
    const r = row({ bookingNumber: "1", groupKey: "g1" });
    const out = groupRows({
      groups: [group({ key: "g1", bookingNumbers: ["1", "1"] })],
      rows: [r],
    });
    expect(out.byGroup.get("g1")).toHaveLength(1);
    expect(out.ungrouped).toHaveLength(0);
  });
});

describe("summaryChanges", () => {
  it("lists only the counts that moved", () => {
    expect(summaryChanges(summary({ tripsToCreate: 2, new: 3 }), summary({ tripsToCreate: 1, new: 3 }))).toEqual([
      "trips to create 2 → 1",
    ]);
  });
});

describe("suggestResidentRole", () => {
  it("to Thompson → Pickup, from Thompson → Dropoff, otherwise no guess", () => {
    expect(suggestResidentRole("Shuttle to Thompson")).toBe("Pickup");
    expect(suggestResidentRole("Shuttle from Thompson")).toBe("Dropoff");
    expect(suggestResidentRole("Lynn Lake <-> Leaf Rapids")).toBeNull();
  });
});
