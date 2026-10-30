import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import type { BudgetCodeRecord, VendorRecord } from "@/lib/api/budgeting";
import type { BudgetPeriod } from "@/lib/types";

// The Period Dashboard calls lib/api/budgeting directly (no `api` prop), so the request functions
// are mocked at the module boundary; everything else is the real implementation — the BudgetCodes
// test's pattern. What it pins: the vendor register the item modal picks from is re-read every
// time the modal opens, so a vendor retired elsewhere after the dashboard loaded is not offered
// as active — and a slower older read never lands on top of a newer one.

const api = vi.hoisted(() => ({
  listBudgetAllocations: vi.fn(),
  listBudgetCodes: vi.fn(),
  getCostCentreRollup: vi.fn(),
  getVendorRollup: vi.fn(),
  listVendors: vi.fn(),
  listBudgetPeriods: vi.fn(),
}));

vi.mock("@/lib/api/budgeting", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/lib/api/budgeting")>()),
  ...api,
}));

import PeriodDashboard from "@/components/screens/periods/PeriodDashboard";

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

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

const EXPENSE_CODE: BudgetCodeRecord = {
  id: "fleet-tires",
  code: "FLEET-TIRES",
  name: "Tires",
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

function vendor(id: string, name: string, isActive = true): VendorRecord {
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

const KAL = vendor("v-kal", "Kal Tire");
const KAL_RETIRED = vendor("v-kal", "Kal Tire", false);
const ESSO = vendor("v-esso", "Esso Thompson");

function arrange() {
  api.listBudgetAllocations.mockResolvedValue([]);
  api.listBudgetCodes.mockResolvedValue([EXPENSE_CODE]);
  // The rollups' panels own their errors; a refusal keeps them out of this test's way.
  api.getCostCentreRollup.mockRejectedValue(new Error("not under test"));
  api.getVendorRollup.mockRejectedValue(new Error("not under test"));
  api.listBudgetPeriods.mockResolvedValue([]);
}

function renderDashboard() {
  render(
    <PeriodDashboard period={PERIOD} periods={[PERIOD]} onPeriodsRefreshed={vi.fn()} onOpenCodes={vi.fn()} />,
  );
}

const firstRowOptions = () =>
  within(screen.getAllByTestId("vendor-share-row")[0])
    .getAllByRole("option")
    .map((o) => o.textContent);

async function openItemModalWithOneRow() {
  fireEvent.click(await screen.findByText("+ ADD BUDGET ITEM"));
  fireEvent.click(screen.getByText("+ ADD VENDOR"));
}

describe("PeriodDashboard — the item modal's vendor register", () => {
  it("re-reads the register when the item modal opens: a vendor retired since the dashboard loaded is not offered", async () => {
    arrange();
    api.listVendors.mockResolvedValueOnce([KAL, ESSO]).mockResolvedValue([KAL_RETIRED, ESSO]);

    renderDashboard();
    await waitFor(() => expect(api.listVendors).toHaveBeenCalledTimes(1));

    await openItemModalWithOneRow();
    expect(api.listVendors).toHaveBeenCalledTimes(2);
    expect(api.listVendors).toHaveBeenLastCalledWith(true); // retired included, as on mount

    await waitFor(() => expect(firstRowOptions()).not.toContain("Kal Tire"));
    expect(firstRowOptions()).toContain("Esso Thompson");
    expect(firstRowOptions().some((l) => l?.includes("Kal Tire"))).toBe(false);
  });

  it("a slower earlier read landing after the modal's fresh one is dropped — the register never regresses", async () => {
    arrange();
    let releaseMountRead!: (rows: VendorRecord[]) => void;
    api.listVendors
      .mockImplementationOnce(() => new Promise<VendorRecord[]>((resolve) => (releaseMountRead = resolve)))
      .mockResolvedValue([KAL_RETIRED, ESSO]);

    renderDashboard();
    await openItemModalWithOneRow();
    await waitFor(() => expect(firstRowOptions()).toContain("Esso Thompson"));

    // The mount-time read — from before Kal Tire was retired — lands only now.
    await act(async () => releaseMountRead([KAL, ESSO]));

    expect(firstRowOptions()).not.toContain("Kal Tire");
    expect(firstRowOptions()).toContain("Esso Thompson");
  });
});
