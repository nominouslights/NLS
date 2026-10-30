import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type {
  ActualTransactionRecord,
  BudgetCodeRecord,
  BudgetRollupRowRecord,
} from "@/lib/api/budgeting";
import { ApiError } from "@/lib/api/transport";
import type { BudgetPeriod } from "@/lib/types";

// Actuals vs Budget calls lib/api/budgeting directly, so its reads are mocked at the module
// boundary (as BudgetCodes.test.tsx does). What it pins: the rollup and the entered period's
// chart load together under ONE error state — a chart failure is not wiped by the rollup's
// success, RECORD says why it is disabled, a row click opens nothing without a chart, and RETRY
// refetches both. And that cents-bearing figures print to the cent beside each other.

const api = vi.hoisted(() => ({
  budgetRollup: vi.fn(),
  listActualTransactions: vi.fn(),
  listBudgetCodes: vi.fn(),
  removeActualTransaction: vi.fn(),
}));

vi.mock("@/lib/api/budgeting", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/budgeting")>()),
  ...api,
}));

import ActualsVsBudget from "@/components/screens/ActualsVsBudget";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const PERIOD: BudgetPeriod = {
  id: "p-q4",
  label: "Q4 2026",
  startsOn: "2026-10-01",
  endsOn: "2026-12-31",
  state: "Open",
  pk: "info",
  plannedRevenue: 0,
  plannedExpense: 0,
};

const FUEL = {
  id: "c-fuel",
  code: "FUEL",
  name: "Diesel",
  description: null,
  category: "Expense",
  serviceLine: null,
  costCentre: null,
  parentCodeId: null,
  parentCode: null,
  parentName: null,
  glAccountCode: null,
  taxTreatment: null,
  budgetOwnerUserId: null,
  budgetOwnerName: null,
  budgetOwnerEmail: null,
  reviewFrequency: "Monthly",
  isActive: true,
  createdByName: null,
  createdByEmail: null,
  modifiedByName: null,
  modifiedByEmail: null,
} as BudgetCodeRecord;

const ROWS: BudgetRollupRowRecord[] = [
  { budgetCodeId: "c-fuel", code: "FUEL", name: "Diesel", category: "Expense", plannedCad: 3.03, actualCad: 3, deltaCad: -0.03 },
];

const TXN: ActualTransactionRecord = {
  id: "t-1",
  budgetCodeId: "c-fuel",
  code: "FUEL",
  name: "Diesel",
  category: "Expense",
  serviceLine: null,
  isCodeActive: true,
  amountCad: 3,
  occurredOn: "2026-10-05",
  description: "Diesel top-up",
  source: "Manual",
  externalReference: null,
  createdBy: null,
  createdByEmail: null,
  modifiedBy: null,
  modifiedByEmail: null,
  createdAtUtc: "2026-10-05T00:00:00Z",
  updatedAtUtc: "2026-10-05T00:00:00Z",
};

function renderScreen() {
  return render(<ActualsVsBudget period={PERIOD} periods={[PERIOD]} />);
}

const recordButton = () => screen.getByText("+ RECORD TRANSACTION");

describe("ActualsVsBudget — the chart and the rollup load together", () => {
  it("keeps a chart failure visible after the rollup succeeds, and RETRY refetches the chart", async () => {
    api.budgetRollup.mockResolvedValue(ROWS);
    api.listActualTransactions.mockResolvedValue([TXN]);
    api.listBudgetCodes.mockRejectedValueOnce(
      new ApiError("Budgeting.Period.NotFound", "Chart unavailable.", 404),
    );
    renderScreen();

    expect(await screen.findByText("Chart unavailable.")).toBeTruthy();
    expect(screen.getByText(/budget codes could not be loaded/)).toBeTruthy();
    expect(recordButton().getAttribute("aria-disabled")).toBe("true");
    // The rollup still shows.
    expect(screen.getByText("Diesel top-up")).toBeTruthy();

    // A row click opens nothing without a chart.
    fireEvent.click(screen.getByText("Diesel top-up"));
    expect(screen.queryByText("Correct FUEL")).toBeNull();

    api.listBudgetCodes.mockResolvedValue([FUEL]);
    fireEvent.click(screen.getByText("RETRY"));

    await waitFor(() => expect(recordButton().getAttribute("aria-disabled")).toBeNull());
    expect(api.listBudgetCodes).toHaveBeenCalledTimes(2);
    expect(screen.queryByText("Chart unavailable.")).toBeNull();
    expect(screen.queryByText(/budget codes could not be loaded/)).toBeNull();

    fireEvent.click(screen.getByText("Diesel top-up"));
    expect(screen.getByText("Correct FUEL")).toBeTruthy();
  });

  it("prints cents-bearing plan, actual and delta to the cent", async () => {
    api.budgetRollup.mockResolvedValue(ROWS);
    api.listActualTransactions.mockResolvedValue([]);
    api.listBudgetCodes.mockResolvedValue([FUEL]);
    renderScreen();

    // Tile, section header and row each print the plan as $3.03 — never "$3".
    expect((await screen.findAllByText("$3.03")).length).toBeGreaterThan(0);
    expect(screen.getAllByText("−$0.03").length).toBe(2);
    expect(screen.getByText(/planned \$3\.03 · actual \$3$/)).toBeTruthy();
  });
});
