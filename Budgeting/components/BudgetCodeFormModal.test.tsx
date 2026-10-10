import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import BudgetCodeFormModal, { type BudgetCodeApi } from "@/components/BudgetCodeFormModal";
import type { CostCentreApi } from "@/components/CostCentreFormModal";
import type { BudgetCodeRecord, CostCentreRecord } from "@/lib/api/budgeting";
import type { BudgetCode } from "@/lib/types";

// The budget-code form's cost-centre picker. Both modals take their requests as props (`api`,
// `costCentreApi`), so this injects vi.fn()s — the BudgetItemFormModal pattern. What it pins:
// only ACTIVE register entries are offered; a code's current value that is retired or not in
// the register stays selectable and marked, and saves unchanged (BudgetCodeCostCentreRule
// accepts an unchanged value); a Revenue code has no picker and sends null; and "+ New cost
// centre…" creates an entry inline and selects it.

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const PERIOD = "period-1";

function cc(id: string, code: string, name: string, isActive = true): CostCentreRecord {
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
    isActive,
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

const REGISTER = [cc("cc-leaf", "Leaf", "Leaf Rapids", false), cc("cc-thompson", "Thompson", "Thompson base")];

function code(over: Partial<BudgetCode> = {}): BudgetCode {
  return {
    id: "code-1",
    code: "FUEL",
    name: "Fuel",
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
    active: true,
    createdByName: null,
    createdByEmail: null,
    modifiedByName: null,
    modifiedByEmail: null,
    ...over,
  };
}

/** Enough of a record for the refetch predicates (id + name). */
function codeRecord(id: string, name: string): BudgetCodeRecord {
  return { id, name } as BudgetCodeRecord;
}

function renderModal(props: Partial<Parameters<typeof BudgetCodeFormModal>[0]> = {}) {
  const api = {
    create: vi.fn().mockResolvedValue("new-code"),
    update: vi.fn().mockResolvedValue(undefined),
    list: vi.fn().mockResolvedValue([codeRecord("code-1", "Fuel"), codeRecord("new-code", "Diesel")]),
  } satisfies BudgetCodeApi;
  const onSaved = vi.fn();
  const onCostCentresChanged = vi.fn();
  render(
    <BudgetCodeFormModal
      periodId={PERIOD}
      periodLabel="Q4 2026"
      code={null}
      allCodes={[]}
      owners={[]}
      costCentres={REGISTER}
      onCostCentresChanged={onCostCentresChanged}
      onClose={vi.fn()}
      onSaved={onSaved}
      api={api}
      {...props}
    />,
  );
  return { api, onSaved, onCostCentresChanged };
}

/** The cost-centre <select> — found by its first option, since ui/Field labels carry no htmlFor. */
function ccSelect(): HTMLSelectElement | undefined {
  return (screen.queryAllByRole("combobox") as HTMLSelectElement[]).find(
    (s) => s.options[0]?.textContent === "— No cost centre —",
  );
}

const optionTexts = (s: HTMLSelectElement) => [...s.options].map((o) => o.textContent);

describe("BudgetCodeFormModal — cost-centre picker", () => {
  it("offers only active register entries, plus none and + New cost centre…", () => {
    renderModal();
    const select = ccSelect()!;
    expect(optionTexts(select)).toEqual(["— No cost centre —", "Thompson · Thompson base", "+ New cost centre…"]);
    expect(select.value).toBe("");
  });

  it("keeps a RETIRED current value selectable and marked, and saves it unchanged", async () => {
    const { api, onSaved } = renderModal({ code: code({ costCentre: "Leaf" }) });
    const select = ccSelect()!;

    expect(select.value).toBe("Leaf");
    expect(optionTexts(select)).toContain("Leaf · Leaf Rapids (retired — kept)");
    expect(screen.getByTestId("cost-centre-kept").textContent).toContain("Retired");

    fireEvent.click(screen.getByText("SAVE CHANGES"));
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.update).toHaveBeenCalledWith(PERIOD, "code-1", expect.objectContaining({ costCentre: "Leaf" }));
  });

  it("keeps a current value the register does not know, marked Not in register", () => {
    renderModal({ code: code({ costCentre: "OPS-01" }) });
    expect(ccSelect()!.value).toBe("OPS-01");
    expect(screen.getByTestId("cost-centre-kept").textContent).toContain("Not in register");
  });

  it("drops the marker once an active entry is chosen instead", () => {
    renderModal({ code: code({ costCentre: "Leaf" }) });
    fireEvent.change(ccSelect()!, { target: { value: "Thompson" } });
    expect(ccSelect()!.value).toBe("Thompson");
    expect(screen.queryByTestId("cost-centre-kept")).toBeNull();
  });

  it("hides the picker for a Revenue code and sends null", async () => {
    const { api } = renderModal({ code: code({ costCentre: "Thompson" }) });
    expect(ccSelect()).toBeDefined();

    const category = (screen.getAllByRole("combobox") as HTMLSelectElement[]).find((s) =>
      [...s.options].some((o) => o.value === "Revenue"),
    )!;
    fireEvent.change(category, { target: { value: "Revenue" } });
    expect(ccSelect()).toBeUndefined();

    fireEvent.click(screen.getByText("SAVE CHANGES"));
    await waitFor(() => expect(api.update).toHaveBeenCalled());
    expect(api.update.mock.calls[0][2]).toMatchObject({ category: "Revenue", costCentre: null });
  });

  it("creates a cost centre inline and selects it — case kept", async () => {
    const created = cc("cc-snow", "Snow Lake", "Snow Lake base");
    const costCentreApi = {
      create: vi.fn().mockResolvedValue("cc-snow"),
      update: vi.fn(),
      list: vi.fn().mockResolvedValue([...REGISTER, created]),
    } satisfies CostCentreApi;
    const { api, onCostCentresChanged } = renderModal({ costCentreApi });

    fireEvent.change(ccSelect()!, { target: { value: "__new_cost_centre__" } });
    expect(screen.getByText("New Cost Centre")).toBeTruthy();
    // The picker itself did not move to the sentinel.
    expect(ccSelect()!.value).toBe("");

    fireEvent.change(screen.getByPlaceholderText("THOMPSON"), { target: { value: " Snow Lake " } });
    fireEvent.change(screen.getByPlaceholderText("Thompson base"), { target: { value: "Snow Lake base" } });
    fireEvent.click(screen.getByText("CREATE COST CENTRE"));

    await waitFor(() => expect(ccSelect()!.value).toBe("Snow Lake"));
    expect(costCentreApi.create).toHaveBeenCalledWith({
      code: "Snow Lake",
      name: "Snow Lake base",
      description: null,
      ownerUserId: null,
      parentId: null,
    });
    expect(costCentreApi.list).toHaveBeenCalledWith({ includeInactive: true });
    expect(onCostCentresChanged).toHaveBeenCalledWith([...REGISTER, created]);
    expect(screen.queryByText("New Cost Centre")).toBeNull();

    // And the code saves with it.
    fireEvent.change(screen.getByPlaceholderText("FLEET-MAINT"), { target: { value: "diesel" } });
    fireEvent.change(screen.getByPlaceholderText("Alamos crew shuttle"), { target: { value: "Diesel" } });
    fireEvent.click(screen.getByText("CREATE CODE"));
    await waitFor(() => expect(api.create).toHaveBeenCalled());
    expect(api.create.mock.calls[0][1]).toMatchObject({ code: "DIESEL", costCentre: "Snow Lake" });
  });

  it("shows a refused inline create's message verbatim and keeps the code form's value", async () => {
    const { ApiError } = await import("@/lib/api/transport");
    const message = "Another cost centre already uses that code.";
    const costCentreApi = {
      create: vi.fn().mockRejectedValue(new ApiError("Budgeting.CostCentre.DuplicateCode", message, 409)),
      update: vi.fn(),
      list: vi.fn(),
    } satisfies CostCentreApi;
    renderModal({ costCentreApi });

    fireEvent.change(ccSelect()!, { target: { value: "__new_cost_centre__" } });
    fireEvent.change(screen.getByPlaceholderText("THOMPSON"), { target: { value: "Thompson" } });
    fireEvent.change(screen.getByPlaceholderText("Thompson base"), { target: { value: "Again" } });
    fireEvent.click(screen.getByText("CREATE COST CENTRE"));

    await waitFor(() => expect(screen.getByText(message)).toBeTruthy());
    expect(costCentreApi.list).not.toHaveBeenCalled();
    expect(ccSelect()!.value).toBe("");
  });
});
