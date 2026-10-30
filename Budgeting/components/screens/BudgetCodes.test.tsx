import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { BudgetCodeRecord, CostCentreRecord } from "@/lib/api/budgeting";
import { ApiError } from "@/lib/api/transport";
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
  deleteBudgetCode: vi.fn(),
  setBudgetCodeActive: vi.fn(),
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

function code(
  id: string,
  codeStr: string,
  name: string,
  category: "Revenue" | "Expense",
  costCentre: string | null,
): BudgetCodeRecord {
  return {
    id,
    code: codeStr,
    name,
    description: null,
    category,
    serviceLine: null,
    costCentre,
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

// Server order (by code). Fleet/FLEET is one group with two spellings; RENT has no cost centre;
// FARES is revenue and lands in the Revenue bucket, last.
const CODES: BudgetCodeRecord[] = [
  code("c-fares", "FARES", "Passenger fares", "Revenue", null),
  code("c-fuel", "FUEL", "Diesel", "Expense", "Fleet"),
  code("c-rent", "RENT", "Hangar rent", "Expense", null),
  code("c-tires", "TIRES", "Tires", "Expense", "FLEET"),
  code("c-wages", "WAGES", "Driver wages", "Expense", "Fleet"),
];

/** A group header is a button whose accessible name starts with the group's label. */
const header = (label: string) =>
  screen
    .getAllByRole("button")
    .find((b) => b.getAttribute("aria-expanded") !== null && b.textContent?.startsWith(label))!;

function renderChart(selId: string | null, onSelect = vi.fn()) {
  api.listBudgetCodes.mockResolvedValue(CODES);
  api.listBudgetOwnerCandidates.mockResolvedValue([]);
  api.listCostCentres.mockResolvedValue([]);
  render(<BudgetCodes period={PERIOD} periods={[PERIOD]} selId={selId} onSelect={onSelect} />);
  return onSelect;
}

describe("BudgetCodes screen — grouped by cost centre", () => {
  it("opens only the selected code's group, locked, and merges spellings case-folded", async () => {
    renderChart("c-rent");
    await screen.findAllByText("Hangar rent");

    const fleet = header("Fleet");
    expect(fleet.getAttribute("aria-expanded")).toBe("false");
    expect(fleet.textContent).toContain("· 2 spellings");
    expect(header("No cost centre").getAttribute("aria-expanded")).toBe("true");
    expect(header("No cost centre").getAttribute("aria-disabled")).toBe("true");
    expect(header("No cost centre").textContent).toContain("· selected");
    expect(header("Revenue").textContent).toContain("· cost centres don't apply");
    // Collapsed groups render no cards.
    expect(screen.queryByText("Diesel")).toBeNull();

    // Revenue comes last.
    const order = screen
      .getAllByRole("button")
      .filter((b) => b.getAttribute("aria-expanded") !== null)
      .map((b) => b.textContent?.split("·")[0].replace(/\d.*$/, "").trim());
    expect(order).toEqual(["Fleet", "No cost centre", "Revenue"]);

    fireEvent.click(fleet);
    expect(header("Fleet").getAttribute("aria-expanded")).toBe("true");
    expect(screen.getByText("Diesel")).toBeTruthy();
  });

  it("never labels the fallback selection as selected", async () => {
    renderChart(null);
    // The fallback is the first card of the first group (FUEL in Fleet), not list[0] (FARES).
    expect((await screen.findAllByText("Diesel")).length).toBeGreaterThan(0);
    expect(header("Fleet").textContent).not.toContain("· selected");
  });

  it("opens every group while searching and says when the selection is filtered out", async () => {
    renderChart("c-rent");
    await screen.findAllByText("Hangar rent");

    fireEvent.change(screen.getByPlaceholderText("Code or name"), { target: { value: "ti" } });
    expect(header("Fleet").getAttribute("aria-expanded")).toBe("true");
    expect(header("Fleet").getAttribute("aria-disabled")).toBe("true");
    expect(header("Fleet").textContent).toContain("1 of 3");
    expect(screen.getByText("RENT is selected but not in these results.")).toBeTruthy();

    fireEvent.change(screen.getByPlaceholderText("Code or name"), { target: { value: "zzz" } });
    expect(screen.getByText(/No code or name matches/)).toBeTruthy();
  });

  it("keeps a group's whole-group label while a search narrows it to a minority spelling", async () => {
    renderChart("c-rent");
    await screen.findAllByText("Hangar rent");

    // TIRES is the one code spelled "FLEET"; the group is still labelled by its most common
    // spelling, still says it has two, and counts against the whole group.
    fireEvent.change(screen.getByPlaceholderText("Code or name"), { target: { value: "tires" } });
    const fleet = header("Fleet");
    expect(fleet).toBeDefined();
    expect(fleet.textContent).toContain("· 2 spellings");
    expect(fleet.textContent).toContain("1 of 3");
    // The only matching code's own spelling never becomes the label.
    expect(header("FLEET")).toBeUndefined();
  });

  it("finds a retired code in the screen-level search", async () => {
    api.listBudgetCodes.mockResolvedValue([
      ...CODES,
      { ...code("c-oil", "OIL", "Engine oil", "Expense", "Fleet"), isActive: false },
    ]);
    api.listBudgetOwnerCandidates.mockResolvedValue([]);
    api.listCostCentres.mockResolvedValue([]);
    render(<BudgetCodes period={PERIOD} periods={[PERIOD]} selId="c-rent" onSelect={vi.fn()} />);
    await screen.findAllByText("Hangar rent");

    fireEvent.change(screen.getByPlaceholderText("Code or name"), { target: { value: "engine" } });
    expect(screen.getByText("Engine oil")).toBeTruthy();
    expect(screen.getByText("Retired")).toBeTruthy();
    expect(header("Fleet").textContent).toContain("1 of 4");
  });

  it("shows a refused write's PeriodNotEditable message verbatim", async () => {
    renderChart("c-rent");
    await screen.findAllByText("Hangar rent");
    const message = "A period's budget codes can only be changed while the period is Draft or Open.";
    api.deleteBudgetCode.mockRejectedValue(new ApiError("Budgeting.Code.PeriodNotEditable", message, 409));

    fireEvent.click(screen.getByText("DELETE"));
    fireEvent.click(screen.getByText("CONFIRM DELETE"));

    expect(await screen.findByText(message)).toBeTruthy();
  });

  it("says in the retire confirm what blocks a retire, without promising it", async () => {
    renderChart("c-rent");
    await screen.findAllByText("Hangar rent");

    fireEvent.click(screen.getByText("RETIRE"));
    const note = screen.getByText(/Retiring RENT in Q4 2026 changes Q4 2026's chart only/);
    expect(note.textContent).toContain("budget items planned on it here have to be removed first");
    expect(note.textContent).toContain("a code with spend recorded in Q4 2026 cannot be retired");
    expect(note.textContent).toContain("The server may still refuse");
    expect(note.textContent).not.toContain("still count");
  });

  it("keeps the selection when a delete is refused, and shows the server's message", async () => {
    const onSelect = renderChart("c-rent");
    await screen.findAllByText("Hangar rent");
    api.deleteBudgetCode.mockRejectedValue(
      new ApiError("Budgeting.Code.InUse", "RENT has budget items in Q4 2026; retire it instead.", 409),
    );

    fireEvent.click(screen.getByText("DELETE"));
    fireEvent.click(screen.getByText("CONFIRM DELETE"));

    expect(await screen.findByText("RENT has budget items in Q4 2026; retire it instead.")).toBeTruthy();
    expect(onSelect).not.toHaveBeenCalledWith(null);
  });

  it("shows a refused retire's PeriodNotClosed message verbatim", async () => {
    renderChart("c-rent");
    await screen.findAllByText("Hangar rent");
    const message = "RENT can be retired once these periods are closed: Q3 2026.";
    api.setBudgetCodeActive.mockRejectedValue(new ApiError("Budgeting.Code.PeriodNotClosed", message, 409));

    fireEvent.click(screen.getByText("RETIRE"));
    fireEvent.click(screen.getByText("CONFIRM RETIRE"));

    expect(await screen.findByText(message)).toBeTruthy();
  });
});
