import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import CostCentres, { type CostCentresApi } from "@/components/screens/CostCentres";
import { ApiError } from "@/lib/api/transport";
import type { CostCentreRecord } from "@/lib/api/budgeting";
import { COST_CENTRE_MESSAGES } from "@/lib/costCentres";

// The cost-centre register screen takes its requests as an `api` prop (the Vendors pattern), so
// this drives it with vi.fn()s. What it pins: an action's refusal is bound to the entry it was
// about — a delete's InUse 409 on one entry never offers RETIRE INSTEAD on another, and a
// selection change clears it — and a refused retire leaves "Show retired" where it was.

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

function cc(id: string, code: string, name: string, over: Partial<CostCentreRecord> = {}): CostCentreRecord {
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
    ...over,
  };
}

const NORTH = cc("cc-north", "North", "North region");
const NORTH_A = cc("cc-north-a", "North-A", "Thompson base", {
  parentId: "cc-north",
  parentCode: "North",
  parentName: "North region",
});
const OLD = cc("cc-old", "Old", "Old depot", { isActive: false });
const REGISTER = [NORTH, NORTH_A, OLD];

function makeApi() {
  return {
    list: vi.fn<CostCentresApi["list"]>().mockResolvedValue(REGISTER),
    create: vi.fn<CostCentresApi["create"]>().mockResolvedValue("new"),
    update: vi.fn<CostCentresApi["update"]>().mockResolvedValue(undefined),
    setActive: vi.fn<CostCentresApi["setActive"]>().mockResolvedValue(undefined),
    remove: vi.fn<CostCentresApi["remove"]>().mockResolvedValue(undefined),
    users: vi.fn<CostCentresApi["users"]>().mockResolvedValue([]),
  };
}

async function renderScreen(api = makeApi()) {
  render(<CostCentres api={api} />);
  await screen.findAllByText("North region");
  return api;
}

const row = (code: string) =>
  screen.getAllByTestId("cost-centre-row").find((r) => r.textContent?.includes(code))!;

describe("CostCentres screen", () => {
  it("binds a delete's InUse 409 to its entry: selecting another clears it, and nothing retires that one", async () => {
    const api = makeApi();
    api.remove.mockRejectedValueOnce(new ApiError("Budgeting.CostCentre.InUse", COST_CENTRE_MESSAGES.InUse, 409));
    await renderScreen(api);

    // Entry A (North, the first row) — delete refused as InUse.
    fireEvent.click(screen.getByText("DELETE"));
    fireEvent.click(screen.getByText("CONFIRM DELETE"));
    expect(await screen.findByText(COST_CENTRE_MESSAGES.InUse)).toBeTruthy();
    expect(screen.getByText("RETIRE INSTEAD")).toBeTruthy();

    // Select entry B: the refusal was about A, so neither the banner nor the offer follows.
    fireEvent.click(row("North-A"));
    expect(screen.queryByText(COST_CENTRE_MESSAGES.InUse)).toBeNull();
    expect(screen.queryByText("RETIRE INSTEAD")).toBeNull();
    expect(api.setActive).not.toHaveBeenCalled();
  });

  it("RETIRE INSTEAD retires the entry the InUse was for, on the second click", async () => {
    const api = makeApi();
    api.remove.mockRejectedValueOnce(new ApiError("Budgeting.CostCentre.InUse", COST_CENTRE_MESSAGES.InUse, 409));
    await renderScreen(api);

    fireEvent.click(row("North-A"));
    fireEvent.click(screen.getByText("DELETE"));
    fireEvent.click(screen.getByText("CONFIRM DELETE"));
    fireEvent.click(await screen.findByText("RETIRE INSTEAD"));
    expect(api.setActive).not.toHaveBeenCalled();

    api.list.mockResolvedValue([NORTH, { ...NORTH_A, isActive: false }, OLD]);
    fireEvent.click(screen.getAllByText("CONFIRM RETIRE")[0]);
    await waitFor(() => expect(api.setActive).toHaveBeenCalledWith("cc-north-a", false));
  });

  it("a refused retire (409 HasActiveChildren) leaves Show retired off and the entry active", async () => {
    const api = makeApi();
    api.setActive.mockRejectedValueOnce(
      new ApiError("Budgeting.CostCentre.HasActiveChildren", COST_CENTRE_MESSAGES.HasActiveChildren, 409),
    );
    await renderScreen(api);
    expect(screen.queryByText("Old depot")).toBeNull();

    fireEvent.click(screen.getByText("RETIRE"));
    fireEvent.click(screen.getByText("CONFIRM RETIRE"));

    expect(await screen.findByText(COST_CENTRE_MESSAGES.HasActiveChildren)).toBeTruthy();
    expect(api.setActive).toHaveBeenCalledWith("cc-north", false);
    // The toggle did not move: retired entries stay hidden, and it still offers to show them.
    expect(screen.getByText("SHOW RETIRED (1)")).toBeTruthy();
    expect(screen.queryByText("Old depot")).toBeNull();
    // North is still active — its retire button is back to its first-click label.
    expect(screen.getByText("RETIRE")).toBeTruthy();
    expect(row("North").textContent).toContain("Active");
  });

  it("an accepted retire switches Show retired on, so the entry stays in view", async () => {
    const api = makeApi();
    await renderScreen(api);
    api.list.mockResolvedValue([{ ...NORTH, isActive: false }, NORTH_A, OLD]);

    fireEvent.click(screen.getByText("RETIRE"));
    fireEvent.click(screen.getByText("CONFIRM RETIRE"));

    await waitFor(() => expect(screen.getByText("HIDE RETIRED")).toBeTruthy());
    expect(screen.getByText("Old depot")).toBeTruthy();
  });
});
