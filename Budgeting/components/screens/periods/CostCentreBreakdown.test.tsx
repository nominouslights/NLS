import { afterEach, describe, expect, it } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import CostCentreBreakdown from "@/components/screens/periods/CostCentreBreakdown";
import type { CostCentreRollup, CostCentreRollupRow } from "@/lib/api/budgeting";

// The dashboard's "Expense by cost centre" panel, fed a rollup as a prop (the dashboard owns the
// fetch). Pins: children grouped under their parent, an absent parent named on the row, the
// "No cost centre" bucket always shown, retired/unregistered rows carrying a chip with a written
// label, owners shown, and the total being the server's totalPlannedExpenseCad — with no actual
// column anywhere (the rollup is planned only).

afterEach(cleanup);

function row(code: string, over: Partial<CostCentreRollupRow> = {}): CostCentreRollupRow {
  return {
    costCentreId: code.toLowerCase(),
    code,
    name: `${code} base`,
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

const ROLLUP: CostCentreRollup = {
  periodId: "p",
  costCentres: [
    row("NORTH", { plannedCad: 1000, ownerName: "Lea Fontaine", ownerEmail: "lea@example.ca" }),
    row("NORTH-A", { parentId: "north", parentCode: "NORTH", plannedCad: 250.5, ownerEmail: "ops@example.ca", itemCount: 2 }),
    row("OLD", { isActive: false, plannedCad: 40, parentId: "gone", parentCode: "GONE" }),
    row("Raw", { costCentreId: null, plannedCad: 9.5 }),
  ],
  noCostCentre: { budgetCodeCount: 2, itemCount: 3, plannedCad: 100 },
  totalPlannedExpenseCad: 1400,
};

describe("CostCentreBreakdown", () => {
  it("groups a child under its parent, with the group's subtotal", () => {
    render(<CostCentreBreakdown rollup={ROLLUP} error={null} />);

    const groups = screen.getAllByTestId("cc-group");
    expect(groups).toHaveLength(3); // NORTH (+ NORTH-A), OLD, Raw
    const north = groups[0];
    expect(within(north).getByTestId("cc-row").textContent).toContain("NORTH");
    expect(within(north).getByTestId("cc-child").textContent).toContain("NORTH-A");
    expect(within(north).getByTestId("cc-subtotal").textContent).toContain("$1,250.50");
  });

  it("shows owners by name, falling back to the email", () => {
    render(<CostCentreBreakdown rollup={ROLLUP} error={null} />);

    expect(screen.getAllByTestId("cc-row")[0].textContent).toContain("Lea Fontaine");
    expect(screen.getByTestId("cc-child").textContent).toContain("ops@example.ca");
  });

  it("names an absent parent on the row and marks retired and unregistered rows with a written label", () => {
    render(<CostCentreBreakdown rollup={ROLLUP} error={null} />);

    const rows = screen.getAllByTestId("cc-row");
    const old = rows.find((r) => r.textContent?.includes("OLD"))!;
    expect(old.textContent).toContain("↳ GONE");
    expect(old.textContent).toContain("Retired");
    expect(rows.find((r) => r.textContent?.includes("Raw"))!.textContent).toContain("Not in register");
  });

  it("always shows No cost centre, even at $0", () => {
    render(
      <CostCentreBreakdown
        rollup={{ ...ROLLUP, costCentres: [], noCostCentre: { budgetCodeCount: 0, itemCount: 0, plannedCad: 0 }, totalPlannedExpenseCad: 0 }}
        error={null}
      />,
    );

    expect(screen.getByTestId("cc-none").textContent).toContain("No cost centre");
    expect(screen.getByTestId("cc-none").textContent).toContain("$0");
  });

  it("totals with the server's totalPlannedExpenseCad, which the rows plus No cost centre add up to", () => {
    render(<CostCentreBreakdown rollup={ROLLUP} error={null} />);

    expect(screen.getByTestId("cc-none").textContent).toContain("$100");
    expect(screen.getByTestId("cc-total").textContent).toContain("$1,400");
    const summed = ROLLUP.costCentres.reduce((s, r) => s + r.plannedCad, 0) + ROLLUP.noCostCentre.plannedCad;
    expect(summed).toBe(ROLLUP.totalPlannedExpenseCad);
  });

  it("has no actual column — planned only", () => {
    render(<CostCentreBreakdown rollup={ROLLUP} error={null} />);
    expect(screen.queryByText(/^Actual/)).toBeNull();
  });

  it("shows loading and a refused load without rows", () => {
    const { rerender } = render(<CostCentreBreakdown rollup={null} error={null} />);
    expect(screen.getByText("Loading the cost-centre rollup…")).toBeTruthy();

    rerender(<CostCentreBreakdown rollup={null} error="The budget period was not found." />);
    expect(screen.getByText("The budget period was not found.")).toBeTruthy();
    expect(screen.queryByTestId("cc-total")).toBeNull();
  });
});
