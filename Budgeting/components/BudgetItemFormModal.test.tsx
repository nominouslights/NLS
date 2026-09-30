import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import BudgetItemFormModal, { type BudgetItemApi } from "@/components/BudgetItemFormModal";
import { ApiError } from "@/lib/api/transport";
import type { BudgetAllocationRecord } from "@/lib/api/budgeting";
import type { BudgetCode, BudgetCodeCategory } from "@/lib/types";

// The modal takes its three requests as an `api` prop, so this drives it with vi.fn()s rather
// than stubbing the transport — the ProfileForm pattern. What it pins: the cost-mode toggle
// computes the server's total live (BudgetAllocation.Round, mirrored by computeItemAmount), the
// exact BudgetItemRequest body that goes to create/update, the refetch-until-visible hand-off,
// and that both a client-side and a server-side refusal show the server's words verbatim.

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

const PERIOD = "period-1";

function code(id: string, category: BudgetCodeCategory = "Expense", active = true): BudgetCode {
  return {
    id,
    code: id.toUpperCase(),
    name: `Code ${id}`,
    description: null,
    category,
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
    active,
    createdByName: null,
    createdByEmail: null,
    modifiedByName: null,
    modifiedByEmail: null,
  };
}

const CODES = [code("fleet-tires"), code("fleet-fuel"), code("old-code", "Expense", false), code("rev-a", "Revenue")];

function record(over: Partial<BudgetAllocationRecord> = {}): BudgetAllocationRecord {
  return {
    id: "item-1",
    periodId: PERIOD,
    budgetCodeId: "fleet-tires",
    code: "FLEET-TIRES",
    name: "Code fleet-tires",
    category: "Expense",
    serviceLine: null,
    isCodeActive: true,
    title: "Winter tires",
    amountCad: 2450,
    quantity: 4,
    unitCostCad: 612.5,
    unit: "tire",
    justification: "Quote in hand.",
    spendType: "Capital",
    recurrence: "OneTime",
    vendor: "Kal Tire",
    tags: ["winter"],
    priority: "MustHave",
    assumptions: null,
    consequenceIfUnfunded: null,
    createdBy: null,
    createdByEmail: null,
    modifiedBy: null,
    modifiedByEmail: null,
    createdAtUtc: "2026-09-01T00:00:00+00:00",
    updatedAtUtc: "2026-09-01T00:00:00+00:00",
    ...over,
  };
}

function makeApi(listResult: BudgetAllocationRecord[] = []): BudgetItemApi & {
  create: ReturnType<typeof vi.fn>;
  update: ReturnType<typeof vi.fn>;
  list: ReturnType<typeof vi.fn>;
} {
  return {
    create: vi.fn().mockResolvedValue("new-item"),
    update: vi.fn().mockResolvedValue(undefined),
    list: vi.fn().mockResolvedValue(listResult),
  };
}

function renderModal(props: Partial<Parameters<typeof BudgetItemFormModal>[0]> = {}) {
  const onSaved = vi.fn();
  const onClose = vi.fn();
  const api = (props.api as ReturnType<typeof makeApi>) ?? makeApi();
  render(
    <BudgetItemFormModal
      periodId={PERIOD}
      periodLabel="FY2026 Q4"
      category="Expense"
      codes={CODES}
      item={null}
      onClose={onClose}
      onSaved={onSaved}
      {...props}
      api={api}
    />,
  );
  return { api, onSaved, onClose };
}

// Queried by placeholder: ui/Field.tsx's FieldLabel passes no htmlFor, so labels don't associate.
const type = (placeholder: string, value: string) =>
  fireEvent.change(screen.getByPlaceholderText(placeholder), { target: { value } });

const total = () => screen.getByTestId("item-total").textContent ?? "";

describe("BudgetItemFormModal — cost", () => {
  it("toggles to quantity × unit cost and computes the total live", () => {
    renderModal({ presetCodeId: "fleet-tires" });

    fireEvent.click(screen.getByRole("button", { name: /Quantity × unit cost/ }));
    expect(screen.getByRole("button", { name: /Quantity × unit cost/ }).getAttribute("aria-pressed")).toBe("true");

    type("e.g. 12", "12");
    type("e.g. 450", "450");
    expect(total()).toContain("$5,400");
    expect(total()).toContain("12 × $450");

    type("e.g. month, litre, trip", "month");
    expect(total()).toContain("12 month × $450");
  });

  it("rounds like the server — each factor first, then the product, half away from zero", () => {
    renderModal({ presetCodeId: "fleet-tires" });
    fireEvent.click(screen.getByRole("button", { name: /Quantity × unit cost/ }));

    type("e.g. 12", "3");
    type("e.g. 450", "1.005");
    expect(total()).toContain("$3.03");
  });

  it("shows a lump sum to the cent", () => {
    renderModal({ presetCodeId: "fleet-tires" });

    type("0.00", "1200.5");
    expect(total()).toContain("$1,200.50");
  });
});

describe("BudgetItemFormModal — save", () => {
  it("POSTs the exact BudgetItemRequest for a built-up item, then hands back the refetched items", async () => {
    const saved = record({
      id: "new-item",
      budgetCodeId: "fleet-fuel",
      title: "Diesel",
      amountCad: 5400,
      justification: "14 rotations",
      priority: "ShouldHave",
    });
    const api = makeApi([saved]);
    const { onSaved, onClose } = renderModal({ presetCodeId: "fleet-fuel", api });

    type("e.g. Winter tires, unit NL-04", "  Diesel ");
    fireEvent.click(screen.getByRole("button", { name: /Quantity × unit cost/ }));
    type("e.g. 12", "12");
    type("e.g. 450", "450");
    type("e.g. month, litre, trip", "month");
    type("e.g. winter, safety", "fuel, Fuel, ops");
    type(
      "What this spend buys and why this much — kilometres, rosters, quotes in hand.",
      "14 rotations",
    );
    fireEvent.click(screen.getByRole("button", { name: /Recurring/ }));
    fireEvent.click(screen.getByText("ADD ITEM"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith([saved]));
    expect(api.create).toHaveBeenCalledWith(PERIOD, {
      budgetCodeId: "fleet-fuel",
      title: "Diesel",
      amountCad: null,
      quantity: 12,
      unitCostCad: 450,
      unit: "month",
      justification: "14 rotations",
      spendType: "Operating",
      recurrence: "Recurring",
      vendor: null,
      tags: ["fuel", "Fuel", "ops"],
      priority: "ShouldHave",
      assumptions: null,
      consequenceIfUnfunded: null,
    });
    expect(api.update).not.toHaveBeenCalled();
    expect(onClose).toHaveBeenCalled();
  });

  it("refuses before the round trip with the server's own message", () => {
    const { api } = renderModal({ presetCodeId: "fleet-tires" });

    fireEvent.click(screen.getByText("ADD ITEM"));

    expect(screen.getByText("Give the budget item a title — what is this money for?")).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("asks for a code when the section-level add has several to choose from", () => {
    const { api } = renderModal();

    type("e.g. Winter tires, unit NL-04", "Diesel");
    fireEvent.click(screen.getByText("ADD ITEM"));

    expect(screen.getByText("Choose the budget code this item is planned against.")).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("shows a server refusal verbatim and stays open", async () => {
    const api = makeApi();
    const message = "The plan can only change while the period is Draft or Open.";
    api.create.mockRejectedValueOnce(new ApiError("Budgeting.Allocation.PeriodNotEditable", message, 409));
    const { onClose } = renderModal({ presetCodeId: "fleet-tires", api });

    type("e.g. Winter tires, unit NL-04", "Tires");
    type("0.00", "100");
    type(
      "What this spend buys and why this much — kilometres, rosters, quotes in hand.",
      "Quote.",
    );
    fireEvent.click(screen.getByText("ADD ITEM"));

    expect(await screen.findByText(message)).toBeTruthy();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("PUTs an edit to the item's own id, and may move it to another active code", async () => {
    const existing = record();
    const moved = record({ budgetCodeId: "fleet-fuel" });
    const api = makeApi([moved]);
    const { onSaved } = renderModal({ item: existing, api });

    // The existing values are loaded, built-up mode included.
    expect(total()).toContain("$2,450");
    fireEvent.change(screen.getByDisplayValue("FLEET-TIRES · Code fleet-tires"), {
      target: { value: "fleet-fuel" },
    });
    fireEvent.click(screen.getByText("SAVE CHANGES"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith([moved]));
    expect(api.update).toHaveBeenCalledWith(
      PERIOD,
      "item-1",
      expect.objectContaining({
        budgetCodeId: "fleet-fuel",
        quantity: 4,
        unitCostCad: 612.5,
        amountCad: null,
        spendType: "Capital",
        priority: "MustHave",
        tags: ["winter"],
      }),
    );
    expect(api.create).not.toHaveBeenCalled();
  });

  it("opens an item on a retired code with no code chosen — it can only be moved", () => {
    const { api } = renderModal({ item: record({ budgetCodeId: "old-code", code: "OLD-CODE", isCodeActive: false }) });

    expect(screen.getByText(/which is retired/)).toBeTruthy();
    fireEvent.click(screen.getByText("SAVE CHANGES"));
    expect(screen.getByText("Choose the budget code this item is planned against.")).toBeTruthy();
    expect(api.update).not.toHaveBeenCalled();
  });
});

describe("BudgetItemFormModal — a period with no active codes", () => {
  // Codes belong to a period, so a fresh period's chart can be empty. The modal must point to
  // Budget Codes (for THIS period) rather than offer an empty picker.
  it("names the period, offers no picker, and sends the planner to Budget Codes", () => {
    const onOpenCodes = vi.fn();
    const { onClose, api } = renderModal({ codes: [code("rev-a", "Revenue")], onOpenCodes });

    expect(screen.queryByText("Budget code")).toBeNull();
    expect(screen.getByText(/FY2026 Q4 has no active expense code to plan against/)).toBeTruthy();
    expect(screen.getByText("ADD ITEM").getAttribute("aria-disabled")).toBe("true");

    fireEvent.click(screen.getByText("OPEN BUDGET CODES"));
    expect(onClose).toHaveBeenCalledTimes(1);
    expect(onOpenCodes).toHaveBeenCalledTimes(1);
    expect(api.create).not.toHaveBeenCalled();
  });
});
