import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { CostCentreRecord } from "@/lib/api/budgeting";
import type { BudgetPeriod } from "@/lib/types";

// The Budget Codes screen calls lib/api/budgeting directly (no `api` prop), so the request
// functions are mocked at the module boundary; everything else (toBudgetCode, refetchUntil,
// labels) is the real implementation. What it pins: the screen's register never regresses — its
// slower INITIAL listCostCentres must not land on top of the newer register the code modal pushed
// up after an inline create.

const api = vi.hoisted(() => ({
  listBudgetCodes: vi.fn(),
  listBudgetOwnerCandidates: vi.fn(),
  listCostCentres: vi.fn(),
  createCostCentre: vi.fn(),
}));

vi.mock("@/lib/api/budgeting", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/budgeting")>()),
  ...api,
}));

import BudgetCodes from "@/components/screens/BudgetCodes";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function cc(id: string, code: string, name: string): CostCentreRecord {
  return {
    id,
    code,
    name,
    description: null,
    ownerUserId: null,
    ownerName: null,
    ownerEmail: null,
    parentId: null,
    parentCode: null,
    parentName: null,
    isActive: true,
    createdBy: null,
    createdByName: null,
    createdByEmail: null,
    modifiedBy: null,
    modifiedByName: null,
    modifiedByEmail: null,
    createdAtUtc: "2026-10-01T00:00:00+00:00",
    updatedAtUtc: "2026-10-01T00:00:00+00:00",
  };
}

const THOMPSON = cc("cc-thompson", "Thompson", "Thompson base");
const SNOW = cc("cc-snow", "Snow Lake", "Snow Lake base");

const PERIOD: BudgetPeriod = {
  id: "period-1",
  label: "Q4 2026",
  startsOn: "2026-10-01",
  endsOn: "2026-12-31",
  state: "Draft",
  pk: "off",
  plannedRevenue: 0,
  plannedExpense: 0,
};

/** The cost-centre <select> — found by its first option, since ui/Field labels carry no htmlFor. */
function ccSelect(): HTMLSelectElement {
  return (screen.getAllByRole("combobox") as HTMLSelectElement[]).find(
    (s) => s.options[0]?.textContent === "— No cost centre —",
  )!;
}

const optionTexts = (s: HTMLSelectElement) => [...s.options].map((o) => o.textContent);

describe("BudgetCodes screen — cost-centre register", () => {
  it("drops its slower initial register load once an inline create has pushed a newer one", async () => {
    // The initial load hangs until released; the modal's post-create refetch answers at once.
    let releaseInitial!: (rows: CostCentreRecord[]) => void;
    api.listCostCentres
      .mockImplementationOnce(() => new Promise<CostCentreRecord[]>((resolve) => (releaseInitial = resolve)))
      .mockResolvedValue([SNOW, THOMPSON]);
    api.listBudgetCodes.mockResolvedValue([]);
    api.listBudgetOwnerCandidates.mockResolvedValue([]);
    api.createCostCentre.mockResolvedValue("cc-snow");

    render(<BudgetCodes period={PERIOD} periods={[PERIOD]} selId={null} onSelect={vi.fn()} />);
    fireEvent.click((await screen.findAllByText("+ NEW CODE"))[0]);

    // Create Snow Lake inline while the screen's own load is still in flight.
    fireEvent.change(ccSelect(), { target: { value: "__new_cost_centre__" } });
    fireEvent.change(screen.getByPlaceholderText("THOMPSON"), { target: { value: "Snow Lake" } });
    fireEvent.change(screen.getByPlaceholderText("Thompson base"), { target: { value: "Snow Lake base" } });
    fireEvent.click(screen.getByText("CREATE COST CENTRE"));
    await waitFor(() => expect(ccSelect().value).toBe("Snow Lake"));

    // The pre-create read lands now — stale, without Snow Lake.
    await act(async () => releaseInitial([THOMPSON]));

    // A fresh modal has no inline-created entries of its own, so it shows exactly the screen's
    // register: Snow Lake must still be there.
    fireEvent.click(screen.getByText("CANCEL"));
    fireEvent.click(screen.getAllByText("+ NEW CODE")[0]);
    expect(optionTexts(ccSelect())).toEqual([
      "— No cost centre —",
      "Snow Lake · Snow Lake base",
      "Thompson · Thompson base",
      "+ New cost centre…",
    ]);
  });
});
