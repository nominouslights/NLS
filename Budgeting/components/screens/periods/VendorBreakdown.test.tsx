import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import VendorBreakdown from "@/components/screens/periods/VendorBreakdown";
import type { VendorRollup, VendorRollupRow } from "@/lib/api/budgeting";
import { PENDING_VENDOR_NAME } from "@/lib/vendorShares";

// The dashboard's "Expense by vendor" panel, fed the server's rollup as a prop (the dashboard owns
// the fetch — VendorPlannedRollup). Pins: rows in the server's order, a retired vendor marked with
// a written label (never colour alone), a vendor still loading named as such and NOT called
// retired, "No vendor" always shown, the total being the server's totalPlannedExpenseCad that the
// rows plus No vendor add up to, and no actual column (planned only).

afterEach(cleanup);

function row(vendorId: string, name: string | null, over: Partial<VendorRollupRow> = {}): VendorRollupRow {
  return { vendorId, name, isActive: true, itemCount: 1, plannedCad: 0, ...over };
}

const ROLLUP: VendorRollup = {
  periodId: "p",
  vendors: [
    row("v-esso", "Esso Thompson", { plannedCad: 450, itemCount: 1 }),
    row("v-kal", "Kal Tire", { plannedCad: 3200.5, itemCount: 2 }),
    row("v-old", "Old Fuel Co", { isActive: false, plannedCad: 100 }),
    row("v-new", null, { isActive: false, plannedCad: 49.5 }),
  ],
  noVendor: { itemCount: 3, plannedCad: 1200 },
  totalPlannedExpenseCad: 5000,
};

describe("VendorBreakdown", () => {
  it("lists the server's rows in its order, with item counts and planned figures", () => {
    render(<VendorBreakdown rollup={ROLLUP} error={null} />);

    const rows = screen.getAllByTestId("vendor-row");
    expect(rows).toHaveLength(4);
    expect(rows[0].textContent).toContain("Esso Thompson");
    expect(rows[1].textContent).toContain("Kal Tire");
    expect(rows[1].textContent).toContain("2 items");
    expect(rows[1].textContent).toContain("$3,200.50");
  });

  it("marks a retired vendor with a written label, and a loading one by name without calling it retired", () => {
    render(<VendorBreakdown rollup={ROLLUP} error={null} />);

    const rows = screen.getAllByTestId("vendor-row");
    expect(rows[2].textContent).toContain("Retired");
    expect(rows[3].textContent).toContain(PENDING_VENDOR_NAME);
    expect(rows[3].textContent).not.toContain("Retired");
    expect(rows[0].textContent).not.toContain("Retired");
  });

  it("always shows No vendor, even when nothing is on it", () => {
    render(
      <VendorBreakdown
        rollup={{ ...ROLLUP, vendors: [], noVendor: { itemCount: 0, plannedCad: 0 }, totalPlannedExpenseCad: 0 }}
        error={null}
      />,
    );

    expect(screen.queryAllByTestId("vendor-row")).toHaveLength(0);
    expect(screen.getByTestId("vendor-none").textContent).toContain("No vendor");
    expect(screen.getByTestId("vendor-none").textContent).toContain("$0");
    expect(screen.getByTestId("vendor-total").textContent).toContain("$0");
  });

  it("totals with the server's totalPlannedExpenseCad, which the rows plus No vendor add up to", () => {
    render(<VendorBreakdown rollup={ROLLUP} error={null} />);

    expect(screen.getByTestId("vendor-none").textContent).toContain("3 items");
    expect(screen.getByTestId("vendor-none").textContent).toContain("$1,200");
    expect(screen.getByTestId("vendor-total").textContent).toContain("$5,000");
    const summed = ROLLUP.vendors.reduce((s, r) => s + r.plannedCad, 0) + ROLLUP.noVendor.plannedCad;
    expect(summed).toBe(ROLLUP.totalPlannedExpenseCad);
  });

  it("has no actual column — planned only", () => {
    render(<VendorBreakdown rollup={ROLLUP} error={null} />);
    expect(screen.queryByText(/^Actual/)).toBeNull();
  });

  it("shows loading and a refused load without rows", () => {
    const { rerender } = render(<VendorBreakdown rollup={null} error={null} />);
    expect(screen.getByText("Loading the vendor rollup…")).toBeTruthy();

    rerender(<VendorBreakdown rollup={null} error="The budget period was not found." />);
    expect(screen.getByText("The budget period was not found.")).toBeTruthy();
    expect(screen.queryByTestId("vendor-total")).toBeNull();
  });
});
