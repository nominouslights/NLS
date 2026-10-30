import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { BudgetCodeRecord } from "@/lib/api/budgeting";
import { toBudgetCode } from "@/lib/api/budgeting";
import type { BudgetPeriod } from "@/lib/types";

// The record modal calls lib/api/budgeting directly, so the request functions are mocked at the
// module boundary (as BudgetCodes.test.tsx does). What it pins is the per-period code rule the
// record/update handlers enforce through CoveringPeriodRule: budgetCodeId must be a code OF THE
// PERIOD COVERING occurredOn (404 Budgeting.Code.NotFound otherwise). So a date moved into
// another period fetches that period's chart, the selection follows the code STRING when that
// chart has an active code of it, and empties (no id the server would refuse) when it does not.

const api = vi.hoisted(() => ({
  listBudgetCodes: vi.fn(),
  recordActualTransaction: vi.fn(),
  updateActualTransaction: vi.fn(),
  listActualTransactions: vi.fn(),
}));

vi.mock("@/lib/api/budgeting", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/budgeting")>()),
  ...api,
}));

import ActualTransactionFormModal from "@/components/ActualTransactionFormModal";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function period(id: string, label: string, startsOn: string, endsOn: string): BudgetPeriod {
  return { id, label, startsOn, endsOn, state: "Open", pk: "info", plannedRevenue: 0, plannedExpense: 0 };
}

const Q3 = period("p-q3", "Q3 2026", "2026-07-01", "2026-09-30");
const Q4 = period("p-q4", "Q4 2026", "2026-10-01", "2026-12-31");

function code(id: string, codeString: string, isActive = true): BudgetCodeRecord {
  return {
    id,
    code: codeString,
    name: `Name ${codeString}`,
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
    reviewFrequency: "Quarterly",
    isActive,
    createdByName: null,
    createdByEmail: null,
    modifiedByName: null,
    modifiedByEmail: null,
  } as BudgetCodeRecord;
}

const Q3_CODES = [code("q3-fuel", "FUEL"), code("q3-wage", "WAGES")].map(toBudgetCode);

function codeSelect(): HTMLSelectElement {
  return screen.getByRole("combobox") as HTMLSelectElement;
}

function dateInput(): HTMLInputElement {
  return document.querySelector('input[type="date"]') as HTMLInputElement;
}

function renderModal() {
  return render(
    <ActualTransactionFormModal
      periods={[Q3, Q4]}
      openPeriod={Q3}
      openPeriodCodes={Q3_CODES}
      transaction={null}
      onClose={() => {}}
      onSaved={() => {}}
    />,
  );
}

describe("ActualTransactionFormModal — codes come from the covering period", () => {
  it("offers the open period's chart without fetching while the date stays inside it", () => {
    renderModal();
    fireEvent.change(dateInput(), { target: { value: "2026-08-15" } });
    expect(api.listBudgetCodes).not.toHaveBeenCalled();
    expect(codeSelect().value).toBe("q3-fuel");
  });

  it("moving the date into another period fetches that chart and follows the code string", async () => {
    api.listBudgetCodes.mockResolvedValue([code("q4-wage", "WAGES"), code("q4-fuel", "FUEL")]);
    renderModal();
    fireEvent.change(dateInput(), { target: { value: "2026-11-02" } });

    await waitFor(() => expect(codeSelect().value).toBe("q4-fuel"));
    expect(api.listBudgetCodes).toHaveBeenCalledWith("p-q4");
    // Only Q4's rows are offered — never Q3's ids, which the server would refuse.
    const values = Array.from(codeSelect().options).map((o) => o.value);
    expect(values).toEqual(["q4-wage", "q4-fuel"]);
    expect(screen.getByText(/counts in Q4 2026, not Q3 2026/)).toBeTruthy();
  });

  it("empties the selection when the other period has no active code of that string", async () => {
    api.listBudgetCodes.mockResolvedValue([code("q4-wage", "WAGES"), code("q4-fuel", "FUEL", false)]);
    renderModal();
    fireEvent.change(dateInput(), { target: { value: "2026-11-02" } });

    await waitFor(() => expect(codeSelect().value).toBe(""));
    expect(screen.getByText(/it has no active FUEL, so pick a code/)).toBeTruthy();

    fireEvent.click(screen.getByText("RECORD TRANSACTION"));
    expect(await screen.findByText(/Pick a budget code from Q4 2026's chart/)).toBeTruthy();
    expect(api.recordActualTransaction).not.toHaveBeenCalled();
  });

  it("posts the covering period's code id", async () => {
    api.listBudgetCodes.mockResolvedValue([code("q4-fuel", "FUEL")]);
    api.recordActualTransaction.mockResolvedValue("t-1");
    api.listActualTransactions.mockResolvedValue([
      {
        id: "t-1",
        budgetCodeId: "q4-fuel",
        amountCad: 250,
        occurredOn: "2026-11-02",
        description: "Diesel",
      },
    ]);
    renderModal();
    fireEvent.change(dateInput(), { target: { value: "2026-11-02" } });
    await waitFor(() => expect(codeSelect().value).toBe("q4-fuel"));
    fireEvent.change(screen.getByRole("spinbutton"), { target: { value: "250" } });
    fireEvent.change(screen.getByRole("textbox"), { target: { value: "Diesel" } });
    fireEvent.click(screen.getByText("RECORD TRANSACTION"));

    await waitFor(() => expect(api.recordActualTransaction).toHaveBeenCalled());
    expect(api.recordActualTransaction).toHaveBeenCalledWith({
      budgetCodeId: "q4-fuel",
      amountCad: 250,
      occurredOn: "2026-11-02",
      description: "Diesel",
    });
    expect(api.listActualTransactions).toHaveBeenCalledWith("p-q4");
  });
});
