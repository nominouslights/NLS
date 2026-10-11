import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import BudgetItemFormModal, { type BudgetItemApi } from "@/components/BudgetItemFormModal";
import { NEW_VENDOR_OPTION } from "@/components/VendorSharesEditor";
import { ApiError } from "@/lib/api/transport";
import type { BudgetAllocationRecord, VendorRecord } from "@/lib/api/budgeting";
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
    vendor: null,
    vendorShares: [],
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
      vendorShares: [],
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

// ---------------------------------------------------------------------------
// Vendors — an expense item's vendor shares, a revenue item's payer. The register arrives as a
// prop (the dashboard loads it); the inline "+ New vendor…" modal takes its own api seam.
// ---------------------------------------------------------------------------

function vendorRecord(id: string, name: string, isActive = true): VendorRecord {
  return {
    id,
    name,
    contactName: null,
    email: null,
    phone: null,
    address: null,
    notes: null,
    gstRegistrationNumber: null,
    qboDisplayName: null,
    defaultBudgetCode: null,
    isActive,
    createdBy: null,
    modifiedBy: null,
    createdAtUtc: "2026-10-01T00:00:00+00:00",
    updatedAtUtc: "2026-10-01T00:00:00+00:00",
  };
}

const KAL = vendorRecord("v-kal", "Kal Tire");
const ESSO = vendorRecord("v-esso", "Esso Thompson");
const OLD = vendorRecord("v-old", "Old Fuel Co", false);
const GONE = vendorRecord("v-gone", "Gone Garage", false);
const REGISTER = [ESSO, GONE, KAL, OLD];

const shareRows = () => screen.getAllByTestId("vendor-share-row");
const pickVendor = (row: HTMLElement, vendorId: string) =>
  fireEvent.change(within(row).getByRole("combobox"), { target: { value: vendorId } });
const shareAmount = (row: HTMLElement, value: string) =>
  fireEvent.change(within(row).getByRole("spinbutton"), { target: { value } });
const pickedVendor = (row: HTMLElement) => (within(row).getByRole("combobox") as HTMLSelectElement).value;
const remaining = () => screen.getByTestId("share-remaining").textContent ?? "";
const optionLabels = (row: HTMLElement) =>
  within(row)
    .getAllByRole("option")
    .map((o) => o.textContent);

const EXPENSE_WHY = "What this spend buys and why this much — kilometres, rosters, quotes in hand.";

function fillWhatAndWhy(amount: string) {
  type("e.g. Winter tires, unit NL-04", "Winter tires");
  type("0.00", amount);
  type(EXPENSE_WHY, "Quote in hand.");
}

/** A lump-sum expense item as the server returns it after a save. */
function lumpItem(over: Partial<BudgetAllocationRecord> = {}): BudgetAllocationRecord {
  return record({
    title: "Winter tires",
    amountCad: 2450,
    quantity: null,
    unitCostCad: null,
    unit: null,
    tags: [],
    justification: "Quote in hand.",
    priority: "ShouldHave",
    spendType: "Operating",
    ...over,
  });
}

function inlineVendorApi(created: VendorRecord) {
  return {
    create: vi.fn().mockResolvedValue(created.id),
    update: vi.fn(),
    list: vi.fn().mockResolvedValue([...REGISTER, created]),
  };
}

describe("BudgetItemFormModal — expense vendor shares", () => {
  it("splits an item across two vendors, assigns the remainder on request, and POSTs every share", async () => {
    const api = makeApi([
      lumpItem({
        id: "new-item",
        vendorShares: [
          { vendorId: "v-kal", vendorName: "Kal Tire", isActive: true, amountCad: 2000 },
          { vendorId: "v-esso", vendorName: "Esso Thompson", isActive: true, amountCad: 450 },
        ],
      }),
    ]);
    const { onSaved } = renderModal({ presetCodeId: "fleet-tires", vendors: REGISTER, api });

    // The free-text vendor is gone from an expense item; the Vendors group replaces it.
    expect(screen.queryByText(/^Payer/)).toBeNull();
    expect(screen.queryByPlaceholderText("e.g. Alamos Gold")).toBeNull();
    expect(screen.getByText(/No vendor — this item counts under/)).toBeTruthy();

    fillWhatAndWhy("2450");
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    pickVendor(shareRows()[0], "v-kal");
    shareAmount(shareRows()[0], "2000");
    expect(remaining()).toContain("Remaining $450");

    pickVendor(shareRows()[1], "v-esso");
    fireEvent.click(within(shareRows()[1]).getByText("ASSIGN REMAINING"));
    expect((within(shareRows()[1]).getByRole("spinbutton") as HTMLInputElement).value).toBe("450.00");
    expect(remaining()).toContain("Fully assigned");

    fireEvent.click(screen.getByText("ADD ITEM"));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.create).toHaveBeenCalledWith(
      PERIOD,
      expect.objectContaining({
        amountCad: 2450,
        vendor: null,
        vendorShares: [
          { vendorId: "v-kal", amountCad: 2000 },
          { vendorId: "v-esso", amountCad: 450 },
        ],
      }),
    );
  });

  it("offers active vendors only, each once across the rows", () => {
    renderModal({ presetCodeId: "fleet-tires", vendors: REGISTER });
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    pickVendor(shareRows()[0], "v-kal");

    const second = optionLabels(shareRows()[1]);
    expect(second).toContain("Esso Thompson");
    expect(second).not.toContain("Kal Tire"); // taken by the first row
    expect(second.some((l) => l?.includes("Gone Garage"))).toBe(false);
    expect(second.some((l) => l?.includes("Old Fuel Co"))).toBe(false);
    expect(second).toContain("+ New vendor…");
  });

  it("shows Over by, and refuses the save in the server's words before the round trip", () => {
    const { api } = renderModal({ presetCodeId: "fleet-tires", vendors: REGISTER });
    fillWhatAndWhy("2450");
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    pickVendor(shareRows()[0], "v-kal");
    shareAmount(shareRows()[0], "2500");

    expect(remaining()).toContain("Over by $50");
    fireEvent.click(screen.getByText("ADD ITEM"));
    expect(
      screen.getByText(
        "The vendor shares add up to 2,500.00, but the item's amount is 2,450.00. The shares must cover the whole amount exactly — adjust them, or remove every share to leave the item with no vendor.",
      ),
    ).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("a changed amount moves the Remaining line — the shares are never rebalanced", () => {
    renderModal({ presetCodeId: "fleet-tires", vendors: REGISTER });
    fillWhatAndWhy("2450");
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    pickVendor(shareRows()[0], "v-kal");
    shareAmount(shareRows()[0], "2450");
    expect(remaining()).toContain("Fully assigned");

    type("0.00", "2500");
    expect(remaining()).toContain("Remaining $50");
    expect((within(shareRows()[0]).getByRole("spinbutton") as HTMLInputElement).value).toBe("2450");
  });

  it("removing every vendor sends [] — No vendor, explicitly — and clears legacy free text", async () => {
    const existing = lumpItem({
      vendor: "Kal Tire",
      vendorShares: [{ vendorId: "v-kal", vendorName: "Kal Tire", isActive: true, amountCad: 2450 }],
    });
    const api = makeApi([{ ...existing, vendor: null, vendorShares: [] }]);
    const { onSaved } = renderModal({ item: existing, vendors: REGISTER, api });

    expect(screen.getByText(/still carries the old free-text vendor/)).toBeTruthy();
    fireEvent.click(within(shareRows()[0]).getByText("REMOVE"));
    fireEvent.click(screen.getByText("SAVE CHANGES"));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.update).toHaveBeenCalledWith(
      PERIOD,
      "item-1",
      expect.objectContaining({ vendor: null, vendorShares: [] }),
    );
  });

  it("keeps the item's own since-retired vendor, marked, and saves it unchanged", async () => {
    const existing = lumpItem({
      vendorShares: [{ vendorId: "v-old", vendorName: "Old Fuel Co", isActive: false, amountCad: 2450 }],
    });
    const api = makeApi([existing]);
    const { onSaved } = renderModal({ item: existing, vendors: REGISTER, api });

    const row = shareRows()[0];
    expect(pickedVendor(row)).toBe("v-old");
    expect(optionLabels(row)).toContain("Old Fuel Co — retired");
    expect(optionLabels(row).some((l) => l?.includes("Gone Garage"))).toBe(false);
    expect(within(row).getByText("Retired")).toBeTruthy();

    fireEvent.click(screen.getByText("SAVE CHANGES"));
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.update).toHaveBeenCalledWith(
      PERIOD,
      "item-1",
      expect.objectContaining({ vendorShares: [{ vendorId: "v-old", amountCad: 2450 }] }),
    );
  });

  // The note must match the real rule: "already carried" is the SAVED item (BudgetItemVendorRule
  // looks up only vendors the write adds against the item's current shares), so removing the
  // retired vendor's row and re-adding it within the same edit is still offered — and accepted.
  it("a removed retired vendor is still offered within the same edit, and the note says when it is lost", async () => {
    const existing = lumpItem({
      vendorShares: [{ vendorId: "v-old", vendorName: "Old Fuel Co", isActive: false, amountCad: 2450 }],
    });
    const api = makeApi([existing]);
    const { onSaved } = renderModal({ item: existing, vendors: REGISTER, api });

    expect(
      within(shareRows()[0]).getByText("Retired — kept on this item. Once you save without it, it can't be added back."),
    ).toBeTruthy();
    expect(screen.queryByText(/once removed a retired vendor cannot be added back/)).toBeNull();

    fireEvent.click(within(shareRows()[0]).getByText("REMOVE"));
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    expect(optionLabels(shareRows()[0])).toContain("Old Fuel Co — retired");
    expect(optionLabels(shareRows()[0]).some((l) => l?.includes("Gone Garage"))).toBe(false);

    pickVendor(shareRows()[0], "v-old");
    shareAmount(shareRows()[0], "2450");
    fireEvent.click(screen.getByText("SAVE CHANGES"));
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.update).toHaveBeenCalledWith(
      PERIOD,
      "item-1",
      expect.objectContaining({ vendorShares: [{ vendorId: "v-old", amountCad: 2450 }] }),
    );
  });

  it("refuses a newly added vendor the register says was retired since it was picked (BudgetItemVendorRule)", () => {
    // The pick happens, then the dashboard's register copy is replaced by a fresher one in which
    // Kal Tire is retired: the save is refused in the server's 409 words, with no round trip.
    const props = {
      presetCodeId: "fleet-tires",
      periodId: PERIOD,
      periodLabel: "FY2026 Q4",
      category: "Expense" as const,
      codes: CODES,
      item: null,
      onClose: vi.fn(),
      onSaved: vi.fn(),
    };
    const api = makeApi();
    const { rerender } = render(<BudgetItemFormModal {...props} vendors={REGISTER} api={api} />);
    fillWhatAndWhy("100");
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    pickVendor(shareRows()[0], "v-kal");
    shareAmount(shareRows()[0], "100");

    rerender(<BudgetItemFormModal {...props} vendors={[{ ...KAL, isActive: false }, ESSO]} api={api} />);
    fireEvent.click(screen.getByText("ADD ITEM"));

    expect(
      screen.getByText(
        '"Kal Tire" is a retired vendor and cannot be added to a budget item. Choose an active vendor, or restore it in the vendor register first.',
      ),
    ).toBeTruthy();
    expect(api.create).not.toHaveBeenCalled();
  });

  it("+ NEW VENDOR… creates a vendor inline and puts it in a new row", async () => {
    const created = vendorRecord("v-new", "Northern Fuel");
    const vendorApi = inlineVendorApi(created);
    const onVendorsChanged = vi.fn();
    renderModal({ presetCodeId: "fleet-tires", vendors: REGISTER, vendorApi, onVendorsChanged });

    fireEvent.click(screen.getByText("+ NEW VENDOR…"));
    fireEvent.change(screen.getByPlaceholderText("Kal Tire Thompson"), { target: { value: "Northern Fuel" } });
    fireEvent.click(screen.getByText("CREATE VENDOR"));

    await waitFor(() => expect(shareRows()).toHaveLength(1));
    expect(pickedVendor(shareRows()[0])).toBe("v-new");
    expect(optionLabels(shareRows()[0])).toContain("Northern Fuel");
    expect(onVendorsChanged).toHaveBeenCalledWith([...REGISTER, created]);
    expect(vendorApi.create).toHaveBeenCalledWith(expect.objectContaining({ name: "Northern Fuel" }));
    expect(screen.queryByText("CREATE VENDOR")).toBeNull();
  });

  it("the row picker's + New vendor… fills THAT row", async () => {
    const created = vendorRecord("v-new", "Northern Fuel");
    renderModal({ presetCodeId: "fleet-tires", vendors: REGISTER, vendorApi: inlineVendorApi(created) });
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    fireEvent.click(screen.getByText("+ ADD VENDOR"));
    pickVendor(shareRows()[1], NEW_VENDOR_OPTION);
    fireEvent.change(screen.getByPlaceholderText("Kal Tire Thompson"), { target: { value: "Northern Fuel" } });
    fireEvent.click(screen.getByText("CREATE VENDOR"));

    await waitFor(() => expect(pickedVendor(shareRows()[1])).toBe("v-new"));
    expect(shareRows()).toHaveLength(2);
    expect(pickedVendor(shareRows()[0])).toBe("");
  });

  it("an inline name that belongs to a retired vendor is refused in the server's words", () => {
    renderModal({ presetCodeId: "fleet-tires", vendors: REGISTER });
    fireEvent.click(screen.getByText("+ NEW VENDOR…"));
    fireEvent.change(screen.getByPlaceholderText("Kal Tire Thompson"), { target: { value: "old fuel co" } });
    fireEvent.click(screen.getByText("RESTORE OLD FUEL CO"));

    expect(
      screen.getByText(
        '"Old Fuel Co" is a retired vendor and cannot be added to a budget item. Choose an active vendor, or restore it in the vendor register first.',
      ),
    ).toBeTruthy();
    expect(screen.queryAllByTestId("vendor-share-row")).toHaveLength(0);
  });
});

describe("BudgetItemFormModal — revenue payer", () => {
  it("shows a free-text Payer, no Vendors section, and sends the payer with no shares", async () => {
    const saved = lumpItem({
      id: "new-item",
      budgetCodeId: "rev-a",
      category: "Revenue",
      title: "Crew rotations",
      amountCad: 9000,
      justification: "Signed contract.",
      vendor: "Alamos Gold",
      vendorShares: [],
    });
    const api = makeApi([saved]);
    const { onSaved } = renderModal({ category: "Revenue", vendors: REGISTER, api });

    expect(screen.queryByTestId("vendor-shares")).toBeNull();
    expect(screen.queryByText("+ ADD VENDOR")).toBeNull();
    expect(screen.getByText(/^Payer/)).toBeTruthy();

    type("e.g. Alamos crew rotations, October", "Crew rotations");
    type("0.00", "9000");
    type("What this revenue rests on — contracts signed, trips booked, trailing volume.", "Signed contract.");
    type("e.g. Alamos Gold", "  Alamos Gold ");
    fireEvent.click(screen.getByText("ADD ITEM"));

    await waitFor(() => expect(onSaved).toHaveBeenCalledWith([saved]));
    expect(api.create).toHaveBeenCalledWith(
      PERIOD,
      expect.objectContaining({ budgetCodeId: "rev-a", vendor: "Alamos Gold", vendorShares: [] }),
    );
  });

  // An item split while its code was an expense code, the code reclassified Revenue since. The
  // server saves it unchanged (BudgetAllocation.Update: null keeps the shares, CheckCategory only
  // on change) — so an unrelated edit must send null, never [] that would wipe the split.
  const reclassified = () =>
    lumpItem({
      budgetCodeId: "rev-a",
      code: "REV-A",
      category: "Revenue",
      title: "Crew rotations",
      vendorShares: [
        { vendorId: "v-kal", vendorName: "Kal Tire", isActive: true, amountCad: 2000 },
        { vendorId: "v-old", vendorName: "Old Fuel Co", isActive: false, amountCad: 450 },
      ],
    });

  it("a reclassified item's title edit keeps its shares — shown read-only, sent as null", async () => {
    const existing = reclassified();
    const api = makeApi([{ ...existing, title: "Crew rotations, October" }]);
    const { onSaved } = renderModal({ category: "Revenue", item: existing, vendors: REGISTER, api });

    const kept = screen.getByTestId("kept-revenue-shares");
    expect(within(kept).getByText("Kept shares")).toBeTruthy(); // StatusChip: glyph + label
    expect(
      within(kept).getByText(/Shares from when this code was an expense code — kept unless you remove them/),
    ).toBeTruthy();
    expect(within(kept).getByText(/Kal Tire/)).toBeTruthy();
    expect(within(kept).getByText(/Old Fuel Co — retired/)).toBeTruthy();
    expect(screen.queryByTestId("vendor-shares")).toBeNull(); // no editor on revenue
    expect(screen.queryByText("+ ADD VENDOR")).toBeNull();

    type("e.g. Alamos crew rotations, October", "Crew rotations, October");
    fireEvent.click(screen.getByText("SAVE CHANGES"));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.update).toHaveBeenCalledWith(
      PERIOD,
      "item-1",
      expect.objectContaining({ title: "Crew rotations, October", vendorShares: null }),
    );
  });

  it("REMOVE SHARES on a reclassified item sends [] explicitly", async () => {
    const existing = reclassified();
    const api = makeApi([{ ...existing, vendorShares: [] }]);
    const { onSaved } = renderModal({ category: "Revenue", item: existing, vendors: REGISTER, api });

    fireEvent.click(screen.getByText("REMOVE SHARES"));
    expect(screen.queryByTestId("kept-revenue-shares")).toBeNull();
    expect(screen.getByText(/will be removed when you save/)).toBeTruthy();
    fireEvent.click(screen.getByText("SAVE CHANGES"));

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(api.update).toHaveBeenCalledWith(PERIOD, "item-1", expect.objectContaining({ vendorShares: [] }));
  });
});
